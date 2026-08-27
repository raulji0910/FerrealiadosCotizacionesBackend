using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

public class CotizacionService(
    AppDbContext db,
    IConsecutivoCotizacionProvider consecutivoProvider,
    ICotizacionPdfBuilder pdfBuilder,
    TimeProvider timeProvider) : ICotizacionService
{
    public async Task<PaginaResultado<CotizacionResumenDto>> BuscarAsync(EstadoCotizacion? estado, string? texto, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);

        var query = db.Cotizaciones.AsQueryable();

        if (estado is not null)
            query = query.Where(c => c.Estado == estado);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.Codigo, patron) ||
                (c.ClienteNombreSnapshot != null && EF.Functions.Like(c.ClienteNombreSnapshot, patron)));
        }

        query = query.OrderByDescending(c => c.FechaCreacion);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(c => new CotizacionResumenDto(
                c.Id,
                c.Codigo,
                c.Estado,
                c.Consecutivo,
                c.ClienteId,
                c.ClienteNombreSnapshot,
                c.FechaEmision,
                c.FechaCreacion,
                c.Items.Count,
                c.Items.Sum(i => i.PrecioUnitario * i.Cantidad)))
            .ToListAsync(ct);

        return new PaginaResultado<CotizacionResumenDto>(items, total, pagina, tamanoPagina);
    }

    public async Task<CotizacionDetalleDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var cotizacion = await db.Cotizaciones.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, ct);
        return cotizacion is null ? null : MapDetalle(cotizacion);
    }

    public async Task<CotizacionDetalleDto?> ObtenerBorradorPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var normalizado = NormalizarCodigo(codigo);
        var cotizacion = await db.Cotizaciones.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Codigo == normalizado && c.Estado == EstadoCotizacion.Borrador, ct);
        return cotizacion is null ? null : MapDetalle(cotizacion);
    }

    // Marca un precio para una cotización en construcción: reutiliza el borrador con ese código si
    // ya existe, o crea uno nuevo. Si el mismo precio ya estaba marcado en ese mismo borrador, suma
    // la cantidad al ítem existente en vez de duplicar la línea. El precio queda congelado
    // (PrecioUnitario = Costo, "costo con % de ajuste" ya calculado, sin IVA) tal como esté en ese momento.
    public async Task<CotizacionItemDto> MarcarPrecioAsync(MarcarPrecioDto dto, string? usuario, CancellationToken ct = default)
    {
        if (dto.Cantidad < 1)
            throw new InvalidOperationException("La cantidad debe ser mayor a cero.");

        var codigo = NormalizarCodigo(dto.Codigo);
        if (string.IsNullOrWhiteSpace(codigo))
            throw new InvalidOperationException("El código es obligatorio.");

        var precio = await db.ProductoProveedorPrecios
            .Include(p => p.Producto)
            .Include(p => p.Proveedor)
            .FirstOrDefaultAsync(p => p.Id == dto.PrecioId, ct);
        if (precio is null)
            throw new InvalidOperationException($"No existe el precio con id {dto.PrecioId}.");

        var cotizacion = await ObtenerOCrearBorradorAsync(codigo, usuario, ct);

        var itemExistente = await db.CotizacionItems
            .FirstOrDefaultAsync(i => i.CotizacionId == cotizacion.Id && i.ProductoProveedorPrecioId == precio.Id, ct);

        CotizacionItem item;
        if (itemExistente is not null)
        {
            itemExistente.Cantidad += dto.Cantidad;
            item = itemExistente;
        }
        else
        {
            item = new CotizacionItem
            {
                CotizacionId = cotizacion.Id,
                ProductoProveedorPrecioId = precio.Id,
                ProductoId = precio.ProductoId,
                ProductoNombreSnapshot = precio.Producto!.Nombre,
                ProductoCodigoSnapshot = precio.Producto.Codigo,
                ProveedorId = precio.ProveedorId,
                ProveedorNombreSnapshot = precio.Proveedor!.Nombre,
                PrecioUnitario = precio.Costo,
                Cantidad = dto.Cantidad,
                FechaMarcado = timeProvider.GetUtcNow().UtcDateTime,
                MarcadoPor = usuario
            };
            db.CotizacionItems.Add(item);
        }

        await db.SaveChangesAsync(ct);

        return MapItem(item);
    }

    public async Task<bool> QuitarItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await db.CotizacionItems
            .Include(i => i.Cotizacion)
            .ThenInclude(c => c!.Items)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null)
            return false;

        if (item.Cotizacion!.Estado != EstadoCotizacion.Borrador)
            throw new InvalidOperationException("No se puede modificar una cotización ya emitida.");

        db.CotizacionItems.Remove(item);

        // Un borrador que queda sin ítems no aporta nada — se borra también, así el código
        // queda libre de inmediato para un borrador futuro.
        var quedanOtros = item.Cotizacion.Items.Any(i => i.Id != itemId);
        if (!quedanOtros)
            db.Cotizaciones.Remove(item.Cotizacion);

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CotizacionItemDto?> ActualizarCantidadItemAsync(int itemId, ActualizarCantidadItemDto dto, CancellationToken ct = default)
    {
        if (dto.Cantidad < 1)
            throw new InvalidOperationException("La cantidad debe ser mayor a cero.");

        var item = await db.CotizacionItems.Include(i => i.Cotizacion).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null)
            return null;

        if (item.Cotizacion!.Estado != EstadoCotizacion.Borrador)
            throw new InvalidOperationException("No se puede modificar una cotización ya emitida.");

        item.Cantidad = dto.Cantidad;
        await db.SaveChangesAsync(ct);

        return MapItem(item);
    }

    // Transición Borrador -> Emitida: asigna Cliente + Consecutivo (atómico, ver
    // IConsecutivoCotizacionProvider) + FechaEmision, y congela snapshot del cliente. A partir de
    // acá la cotización queda fija — QuitarItemAsync/ActualizarCantidadItemAsync ya no la tocan.
    // DbUpdateConcurrencyException (por RowVersion) se deja propagar tal cual: la maneja el
    // controller como 409, para el caso de que el mismo borrador se intente emitir dos veces en paralelo.
    public async Task<CotizacionDetalleDto?> EmitirAsync(int id, EmitirCotizacionDto dto, CancellationToken ct = default)
    {
        var cotizacion = await db.Cotizaciones.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cotizacion is null)
            return null;

        if (cotizacion.Estado != EstadoCotizacion.Borrador)
            throw new InvalidOperationException("Esta cotización ya fue emitida.");

        if (cotizacion.Items.Count == 0)
            throw new InvalidOperationException("La cotización no tiene ítems para emitir.");

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == dto.ClienteId, ct);
        if (cliente is null || !cliente.Activo)
            throw new InvalidOperationException("El cliente indicado no existe o no está activo.");

        var consecutivo = await consecutivoProvider.ObtenerSiguienteAsync(ct);

        cotizacion.Consecutivo = consecutivo;
        cotizacion.ClienteId = cliente.Id;
        cotizacion.ClienteNombreSnapshot = cliente.Nombre;
        cotizacion.ClienteNitSnapshot = cliente.Nit;
        cotizacion.FormaPago = dto.FormaPago?.Trim();
        cotizacion.Nota = dto.Nota?.Trim();
        cotizacion.Estado = EstadoCotizacion.Emitida;
        cotizacion.FechaEmision = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        await db.SaveChangesAsync(ct);

        return MapDetalle(cotizacion);
    }

    public async Task<byte[]?> GenerarPdfAsync(int id, CancellationToken ct = default)
    {
        var cotizacion = await db.Cotizaciones.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cotizacion is null)
            return null;

        if (cotizacion.Estado != EstadoCotizacion.Emitida)
            throw new InvalidOperationException("Solo se puede generar el PDF de una cotización ya emitida.");

        return pdfBuilder.Generar(MapDetalle(cotizacion));
    }

    private async Task<Cotizacion> ObtenerOCrearBorradorAsync(string codigo, string? usuario, CancellationToken ct)
    {
        var existente = await db.Cotizaciones.FirstOrDefaultAsync(c => c.Codigo == codigo && c.Estado == EstadoCotizacion.Borrador, ct);
        if (existente is not null)
            return existente;

        var nueva = new Cotizacion
        {
            Codigo = codigo,
            Estado = EstadoCotizacion.Borrador,
            FechaCreacion = timeProvider.GetUtcNow().UtcDateTime,
            CreadoPor = usuario
        };
        db.Cotizaciones.Add(nueva);

        try
        {
            await db.SaveChangesAsync(ct);
            return nueva;
        }
        catch (DbUpdateException)
        {
            // Otro cotizador creó un borrador con el mismo código en la misma fracción de segundo
            // (choque contra el índice único filtrado por Estado=Borrador) — se reutiliza el que ganó.
            db.Entry(nueva).State = EntityState.Detached;
            var ganador = await db.Cotizaciones.FirstOrDefaultAsync(c => c.Codigo == codigo && c.Estado == EstadoCotizacion.Borrador, ct);
            if (ganador is null)
                throw;
            return ganador;
        }
    }

    private static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();

    private static CotizacionItemDto MapItem(CotizacionItem item) => new(
        item.Id,
        item.CotizacionId,
        item.ProductoProveedorPrecioId,
        item.ProductoId,
        item.ProductoNombreSnapshot,
        item.ProductoCodigoSnapshot,
        item.ProveedorId,
        item.ProveedorNombreSnapshot,
        item.PrecioUnitario,
        item.Cantidad,
        item.PrecioUnitario * item.Cantidad);

    private static CotizacionDetalleDto MapDetalle(Cotizacion cotizacion)
    {
        var items = cotizacion.Items.Select(MapItem).ToList();
        return new CotizacionDetalleDto(
            cotizacion.Id,
            cotizacion.Codigo,
            cotizacion.Estado,
            cotizacion.Consecutivo,
            cotizacion.ClienteId,
            cotizacion.ClienteNombreSnapshot,
            cotizacion.ClienteNitSnapshot,
            cotizacion.FormaPago,
            cotizacion.Nota,
            cotizacion.FechaEmision,
            cotizacion.FechaCreacion,
            cotizacion.CreadoPor,
            items,
            items.Sum(i => i.Subtotal));
    }
}
