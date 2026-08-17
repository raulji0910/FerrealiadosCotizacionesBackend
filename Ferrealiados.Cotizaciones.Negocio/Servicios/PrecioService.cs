using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

public class PrecioService(AppDbContext db, IConfiguracionService configuracionService, TimeProvider timeProvider) : IPrecioService
{
    public async Task<IReadOnlyList<PrecioProveedorDto>> ObtenerPreciosPorProductoAsync(int productoId, CancellationToken ct = default)
    {
        var mesesVigencia = await configuracionService.ObtenerMesesVigenciaPrecioAsync(ct);
        var hoy = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var vigentes = await ObtenerPreciosVigentesAsync(productoId, ct);

        var dtos = vigentes
            .Select(p => new PrecioProveedorDto(
                p.Id,
                p.ProveedorId,
                p.Proveedor!.Nombre,
                p.Costo,
                p.CostoBase,
                p.PorcentajeAjuste,
                p.FechaCotizacion,
                VigenciaPrecio.DiasDesde(p.FechaCotizacion, hoy),
                VigenciaPrecio.EsVencido(p.FechaCotizacion, mesesVigencia, hoy),
                EsMejorPrecio: false))
            // CostoBase es el costo normal (comparable entre proveedores); Costo ya trae el ajuste de
            // porcentaje aplicado y es solo informativo, no debe usarse para decidir el mejor precio.
            .OrderBy(p => p.CostoBase)
            .ToList();

        if (dtos.Count > 0)
        {
            var mejor = dtos[0];
            dtos[0] = mejor with { EsMejorPrecio = true };
        }

        return dtos;
    }

    public async Task<PrecioProveedorDto> RegistrarPrecioAsync(int productoId, RegistrarPrecioDto dto, CancellationToken ct = default)
    {
        var productoExiste = await db.Productos.AnyAsync(p => p.Id == productoId, ct);
        if (!productoExiste)
            throw new InvalidOperationException($"No existe el producto con id {productoId}.");

        var proveedor = await db.Proveedores.FirstOrDefaultAsync(p => p.Id == dto.ProveedorId, ct);
        if (proveedor is null)
            throw new InvalidOperationException($"No existe el proveedor con id {dto.ProveedorId}.");

        if (dto.PorcentajeAjuste < AjustePrecio.PorcentajeMinimo || dto.PorcentajeAjuste > AjustePrecio.PorcentajeMaximo)
            throw new InvalidOperationException($"El porcentaje de ajuste debe estar entre {AjustePrecio.PorcentajeMinimo} y {AjustePrecio.PorcentajeMaximo}.");

        var precio = new ProductoProveedorPrecio
        {
            ProductoId = productoId,
            ProveedorId = dto.ProveedorId,
            Costo = AjustePrecio.CalcularCostoFinal(dto.Costo, dto.PorcentajeAjuste),
            CostoBase = dto.Costo,
            PorcentajeAjuste = dto.PorcentajeAjuste,
            FechaCotizacion = dto.FechaCotizacion,
            Observaciones = dto.Observaciones?.Trim(),
            CreadoPor = dto.CreadoPor,
            FechaRegistro = timeProvider.GetUtcNow().UtcDateTime
        };

        db.ProductoProveedorPrecios.Add(precio);
        await db.SaveChangesAsync(ct);

        var mesesVigencia = await configuracionService.ObtenerMesesVigenciaPrecioAsync(ct);
        var hoy = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        return new PrecioProveedorDto(
            precio.Id,
            precio.ProveedorId,
            proveedor.Nombre,
            precio.Costo,
            precio.CostoBase,
            precio.PorcentajeAjuste,
            precio.FechaCotizacion,
            VigenciaPrecio.DiasDesde(precio.FechaCotizacion, hoy),
            VigenciaPrecio.EsVencido(precio.FechaCotizacion, mesesVigencia, hoy),
            EsMejorPrecio: false);
    }

    public async Task<PaginaResultado<AlertaPrecioDto>> ObtenerAlertasVencidasAsync(int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);

        var mesesVigencia = await configuracionService.ObtenerMesesVigenciaPrecioAsync(ct);
        var hoy = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var vigentes = await ObtenerPreciosVigentesAsync(productoId: null, ct);

        var vencidas = vigentes
            .Where(p => VigenciaPrecio.EsVencido(p.FechaCotizacion, mesesVigencia, hoy))
            .Select(p => new AlertaPrecioDto(
                p.ProductoId,
                p.Producto!.Codigo,
                p.Producto.Nombre,
                p.ProveedorId,
                p.Proveedor!.Nombre,
                p.CostoBase,
                p.FechaCotizacion,
                VigenciaPrecio.DiasDesde(p.FechaCotizacion, hoy)))
            .OrderByDescending(a => a.DiasDesdeCotizacion)
            .ToList();

        var items = vencidas.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToList();
        return new PaginaResultado<AlertaPrecioDto>(items, vencidas.Count, pagina, tamanoPagina);
    }

    // Un producto puede recotizarse varias veces con el mismo proveedor; el precio "vigente" de cada
    // par (Producto, Proveedor) es siempre el de la FechaCotizacion más reciente, nunca se sobreescribe el histórico.
    private async Task<List<ProductoProveedorPrecio>> ObtenerPreciosVigentesAsync(int? productoId, CancellationToken ct)
    {
        var query = db.ProductoProveedorPrecios.AsQueryable();
        if (productoId is not null)
            query = query.Where(p => p.ProductoId == productoId);

        var maxFechas = await query
            .GroupBy(p => new { p.ProductoId, p.ProveedorId })
            .Select(g => new { g.Key.ProductoId, g.Key.ProveedorId, MaxFecha = g.Max(x => x.FechaCotizacion) })
            .ToListAsync(ct);

        if (maxFechas.Count == 0)
            return [];

        var candidatos = await query
            .Include(p => p.Producto)
            .Include(p => p.Proveedor)
            .ToListAsync(ct);

        var vigentes = new List<ProductoProveedorPrecio>();
        foreach (var max in maxFechas)
        {
            var precio = candidatos
                .Where(p => p.ProductoId == max.ProductoId && p.ProveedorId == max.ProveedorId && p.FechaCotizacion == max.MaxFecha)
                .OrderByDescending(p => p.Id)
                .First();
            vigentes.Add(precio);
        }

        return vigentes;
    }
}
