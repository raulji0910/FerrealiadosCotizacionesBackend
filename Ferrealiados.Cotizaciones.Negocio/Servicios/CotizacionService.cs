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

        // Se trae primero como proyección plana (paginada en SQL) y se formatea el consecutivo
        // en memoria después: la interpolación con formato ("D5") no es traducible a SQL.
        var filas = await query
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(c => new
            {
                c.Id,
                c.Codigo,
                c.Estado,
                c.Consecutivo,
                c.ClienteId,
                c.ClienteNombreSnapshot,
                c.FechaEmision,
                c.FechaCreacion,
                CantidadItems = c.Items.Count,
                Total = c.Items.Sum(i => i.PrecioUnitario * i.Cantidad)
            })
            .ToListAsync(ct);

        var items = filas
            .Select(c => new CotizacionResumenDto(
                c.Id,
                c.Codigo,
                c.Estado,
                c.Consecutivo,
                FormatearConsecutivo(c.Consecutivo),
                c.ClienteId,
                c.ClienteNombreSnapshot,
                c.FechaEmision,
                c.FechaCreacion,
                c.CantidadItems,
                c.Total))
            .ToList();

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
    // la cantidad al ítem existente en vez de duplicar la línea. El precio y el IVA quedan
    // congelados (PrecioUnitario = Costo, "costo con % de ajuste" ya calculado, sin IVA; IvaSnapshot
    // = tarifa del precio en ese momento) tal como estén al marcar — no se actualizan al fusionar.
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
                CostoBaseSnapshot = precio.CostoBase,
                IvaSnapshot = precio.Iva,
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

    // Edición manual del precio unitario antes de emitir — solo toca este ítem (snapshot), nunca
    // el ProductoProveedorPrecio del catálogo del que se marcó originalmente.
    public async Task<CotizacionItemDto?> ActualizarPrecioItemAsync(int itemId, ActualizarPrecioItemDto dto, CancellationToken ct = default)
    {
        if (dto.PrecioUnitario < 0)
            throw new InvalidOperationException("El precio unitario no puede ser negativo.");

        var item = await db.CotizacionItems.Include(i => i.Cotizacion).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null)
            return null;

        if (item.Cotizacion!.Estado != EstadoCotizacion.Borrador)
            throw new InvalidOperationException("No se puede modificar una cotización ya emitida.");

        item.PrecioUnitario = dto.PrecioUnitario;
        await db.SaveChangesAsync(ct);

        return MapItem(item);
    }

    // Igual que ActualizarPrecioItemAsync, pero para la tarifa de IVA informativa del ítem.
    public async Task<CotizacionItemDto?> ActualizarIvaItemAsync(int itemId, ActualizarIvaItemDto dto, CancellationToken ct = default)
    {
        if (dto.Iva is not null && (dto.Iva < 0 || dto.Iva > 100))
            throw new InvalidOperationException("El IVA debe estar entre 0 y 100, o vacío.");

        var item = await db.CotizacionItems.Include(i => i.Cotizacion).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null)
            return null;

        if (item.Cotizacion!.Estado != EstadoCotizacion.Borrador)
            throw new InvalidOperationException("No se puede modificar una cotización ya emitida.");

        item.IvaSnapshot = dto.Iva;
        await db.SaveChangesAsync(ct);

        return MapItem(item);
    }

    // Transición Borrador -> Emitida: asigna Cliente + Consecutivo (atómico, ver
    // IConsecutivoCotizacionProvider) + FechaEmision + Descuento, y congela snapshot del cliente
    // (incluye Contacto/Email, no solo Nombre/Nit). A partir de acá la cotización queda fija —
    // QuitarItemAsync/ActualizarCantidadItemAsync ya no la tocan. DbUpdateConcurrencyException
    // (por RowVersion) se deja propagar tal cual: la maneja el controller como 409, para el caso
    // de que el mismo borrador se intente emitir dos veces en paralelo.
    public async Task<CotizacionDetalleDto?> EmitirAsync(int id, EmitirCotizacionDto dto, CancellationToken ct = default)
    {
        var cotizacion = await db.Cotizaciones.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cotizacion is null)
            return null;

        if (cotizacion.Estado != EstadoCotizacion.Borrador)
            throw new InvalidOperationException("Esta cotización ya fue emitida.");

        if (cotizacion.Items.Count == 0)
            throw new InvalidOperationException("La cotización no tiene ítems para emitir.");

        var subtotalItems = cotizacion.Items.Sum(i => i.PrecioUnitario * i.Cantidad);
        var descuento = dto.Descuento ?? 0;
        if (descuento < 0)
            throw new InvalidOperationException("El descuento no puede ser negativo.");
        if (descuento > subtotalItems)
            throw new InvalidOperationException("El descuento no puede ser mayor al subtotal de la cotización.");

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == dto.ClienteId, ct);
        if (cliente is null || !cliente.Activo)
            throw new InvalidOperationException("El cliente indicado no existe o no está activo.");

        // Si ya tenía un consecutivo (viene de un Reabrir), lo conserva — es la misma cotización
        // oficial actualizada, no una nueva. Solo se consume la secuencia la primera vez.
        cotizacion.Consecutivo ??= await consecutivoProvider.ObtenerSiguienteAsync(ct);

        cotizacion.ClienteId = cliente.Id;
        cotizacion.ClienteNombreSnapshot = cliente.Nombre;
        cotizacion.ClienteNitSnapshot = cliente.Nit;
        cotizacion.ClienteContactoSnapshot = cliente.Contacto;
        cotizacion.ClienteEmailSnapshot = cliente.Email;
        cotizacion.ClienteDireccionSnapshot = cliente.Direccion;
        cotizacion.ClienteCiudadSnapshot = cliente.Ciudad;
        cotizacion.FormaPago = dto.FormaPago?.Trim();
        cotizacion.Nota = dto.Nota?.Trim();
        cotizacion.Descuento = descuento;
        cotizacion.Estado = EstadoCotizacion.Emitida;
        cotizacion.FechaEmision = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        await db.SaveChangesAsync(ct);

        return MapDetalle(cotizacion);
    }

    // Vuelve una cotización Emitida a Borrador para poder seguirle agregando ítems (a pedido del
    // cliente). Conserva el Consecutivo — al volver a emitirla, EmitirAsync lo reutiliza en vez
    // de pedir uno nuevo, porque sigue siendo la misma cotización oficial, solo actualizada.
    public async Task<CotizacionDetalleDto?> ReabrirAsync(int id, CancellationToken ct = default)
    {
        var cotizacion = await db.Cotizaciones.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cotizacion is null)
            return null;

        if (cotizacion.Estado != EstadoCotizacion.Emitida)
            throw new InvalidOperationException("Solo se puede reabrir una cotización ya emitida.");

        // El código es solo único mientras hay un Borrador activo con ese texto — si alguien más
        // ya está usando el mismo código de trabajo para un borrador nuevo, no se puede reabrir
        // esta hasta que ese otro se resuelva (se emita o se descarte).
        var colision = await db.Cotizaciones
            .AnyAsync(c => c.Id != id && c.Codigo == cotizacion.Codigo && c.Estado == EstadoCotizacion.Borrador, ct);
        if (colision)
            throw new InvalidOperationException($"Ya existe otro borrador en construcción con el código '{cotizacion.Codigo}'. No se puede reabrir hasta que ese código quede libre.");

        cotizacion.Estado = EstadoCotizacion.Borrador;
        cotizacion.ClienteId = null;
        cotizacion.ClienteNombreSnapshot = null;
        cotizacion.ClienteNitSnapshot = null;
        cotizacion.ClienteContactoSnapshot = null;
        cotizacion.ClienteEmailSnapshot = null;
        cotizacion.ClienteDireccionSnapshot = null;
        cotizacion.ClienteCiudadSnapshot = null;
        cotizacion.FormaPago = null;
        cotizacion.Nota = null;
        cotizacion.Descuento = 0;
        cotizacion.FechaEmision = null;

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

    private static string? FormatearConsecutivo(int? consecutivo) => consecutivo is null ? null : $"COT-FA-{consecutivo.Value:D5}";

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
        item.IvaSnapshot,
        item.Cantidad,
        item.PrecioUnitario * item.Cantidad,
        CalcularPorcentajeGanancia(item.PrecioUnitario, item.CostoBaseSnapshot));

    // Mismo criterio que AjustePrecio: % de ganancia = cuánto por encima del costo base quedó el
    // precio unitario (PrecioUnitario = CostoBase * (1 + %/100) en el momento de marcar, pero
    // PrecioUnitario se puede editar después — por eso se recalcula siempre desde los valores
    // actuales en vez de guardar el porcentaje). Solo informativo para la app, no participa en
    // ningún cálculo de negocio ni se muestra en el PDF.
    private static decimal? CalcularPorcentajeGanancia(decimal precioUnitario, decimal? costoBase)
        => costoBase is null or 0
            ? null
            : Math.Round((precioUnitario - costoBase.Value) / costoBase.Value * 100, 2);

    // El descuento (global, en pesos) se reparte proporcionalmente entre las tarifas de IVA
    // presentes en los ítems, según la participación de cada tramo en el subtotal — así, si una
    // cotización mezcla productos con 19%/5%/0% de IVA, cada tramo paga IVA sobre su propia base
    // ya descontada, en vez de aplicar una sola tarifa al total completo (que sería incorrecto si
    // los ítems no comparten la misma tarifa).
    private static CotizacionDetalleDto MapDetalle(Cotizacion cotizacion)
    {
        // Orden estable (por Id de creación) — sin esto, EF/SQL Server no garantiza el orden de
        // la colección Items entre una consulta y otra, y las filas podían "saltar" de posición
        // en la grilla del front cada vez que se recargaba la cotización tras editar un ítem.
        var items = cotizacion.Items.OrderBy(i => i.Id).Select(MapItem).ToList();
        var subtotalItems = items.Sum(i => i.Subtotal);
        var descuento = cotizacion.Descuento;
        var subtotalConDescuento = subtotalItems - descuento;

        var ivaDesglose = items
            .GroupBy(i => i.IvaSnapshot ?? 0)
            .Where(g => g.Key > 0)
            .Select(g =>
            {
                var subtotalGrupo = g.Sum(i => i.Subtotal);
                var proporcion = subtotalItems > 0 ? subtotalGrupo / subtotalItems : 0;
                var baseGravable = Math.Round(subtotalGrupo - (descuento * proporcion), 2);
                var valorIva = Math.Round(baseGravable * g.Key / 100m, 2);
                return new IvaDesgloseDto(g.Key, baseGravable, valorIva);
            })
            .OrderByDescending(d => d.Tarifa)
            .ToList();

        var totalGeneral = subtotalConDescuento + ivaDesglose.Sum(d => d.Valor);

        return new CotizacionDetalleDto(
            cotizacion.Id,
            cotizacion.Codigo,
            cotizacion.Estado,
            cotizacion.Consecutivo,
            FormatearConsecutivo(cotizacion.Consecutivo),
            cotizacion.ClienteId,
            cotizacion.ClienteNombreSnapshot,
            cotizacion.ClienteNitSnapshot,
            cotizacion.ClienteContactoSnapshot,
            cotizacion.ClienteEmailSnapshot,
            cotizacion.ClienteDireccionSnapshot,
            cotizacion.ClienteCiudadSnapshot,
            cotizacion.FormaPago,
            cotizacion.Nota,
            cotizacion.FechaEmision,
            cotizacion.FechaCreacion,
            cotizacion.CreadoPor,
            items,
            subtotalItems,
            descuento,
            subtotalConDescuento,
            ivaDesglose,
            totalGeneral);
    }
}
