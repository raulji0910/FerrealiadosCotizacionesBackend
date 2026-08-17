using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

public class ProductoService(AppDbContext db, TimeProvider timeProvider) : IProductoService
{
    public async Task<PaginaResultado<ProductoDto>> BuscarAsync(string? texto, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);

        var query = db.Productos.AsQueryable();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Codigo, patron) || EF.Functions.Like(p.Nombre, patron));
            // Al buscar, se prioriza lo cotizado más recientemente sobre el orden alfabético.
            query = query.OrderByDescending(p => p.Precios.Select(pr => (DateOnly?)pr.FechaCotizacion).Max());
        }
        else
        {
            query = query.OrderBy(p => p.Nombre);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(p => new ProductoDto(
                p.Id, p.Codigo, p.Nombre, p.Descripcion, p.UnidadMedida, p.Activo,
                p.Precios.Select(pr => (DateOnly?)pr.FechaCotizacion).Max()))
            .ToListAsync(ct);

        return new PaginaResultado<ProductoDto>(items, total, pagina, tamanoPagina);
    }

    public async Task<ProductoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Productos
            .Where(p => p.Id == id)
            .Select(p => new ProductoDto(
                p.Id, p.Codigo, p.Nombre, p.Descripcion, p.UnidadMedida, p.Activo,
                p.Precios.Select(pr => (DateOnly?)pr.FechaCotizacion).Max()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ProductoDto> CrearAsync(ProductoCrearDto dto, CancellationToken ct = default)
    {
        var yaExiste = await db.Productos.AnyAsync(p => p.Codigo == dto.Codigo, ct);
        if (yaExiste)
            throw new InvalidOperationException($"Ya existe un producto con el código '{dto.Codigo}'.");

        var producto = new Producto
        {
            Codigo = dto.Codigo.Trim(),
            Nombre = dto.Nombre.Trim(),
            Descripcion = dto.Descripcion?.Trim(),
            UnidadMedida = dto.UnidadMedida?.Trim(),
            Activo = true,
            FechaCreacion = timeProvider.GetUtcNow().UtcDateTime
        };

        db.Productos.Add(producto);
        await db.SaveChangesAsync(ct);

        return new ProductoDto(producto.Id, producto.Codigo, producto.Nombre, producto.Descripcion, producto.UnidadMedida, producto.Activo, null);
    }

    public async Task<ProductoDto?> ActualizarAsync(int id, ProductoActualizarDto dto, CancellationToken ct = default)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (producto is null)
            return null;

        producto.Nombre = dto.Nombre.Trim();
        producto.Descripcion = dto.Descripcion?.Trim();
        producto.UnidadMedida = dto.UnidadMedida?.Trim();
        producto.Activo = dto.Activo;

        await db.SaveChangesAsync(ct);

        var ultimaFechaCotizacion = await db.ProductoProveedorPrecios
            .Where(pr => pr.ProductoId == producto.Id)
            .Select(pr => (DateOnly?)pr.FechaCotizacion)
            .MaxAsync(ct);

        return new ProductoDto(producto.Id, producto.Codigo, producto.Nombre, producto.Descripcion, producto.UnidadMedida, producto.Activo, ultimaFechaCotizacion);
    }
}
