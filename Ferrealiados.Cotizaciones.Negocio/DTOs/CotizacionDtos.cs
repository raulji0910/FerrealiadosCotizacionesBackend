using Ferrealiados.Cotizaciones.Modelo.Entidades;

namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

// Una "marca" es un ítem de cotización visto desde el precio al que pertenece — se usa para
// pintar, en la grilla de precios de un producto, en qué cotizaciones (borrador o ya emitidas)
// está incluido ese precio en este momento. Un mismo precio puede tener varias marcas activas
// simultáneas (una por cada cotización en construcción que lo incluya).
public record MarcaCotizacionDto(int CotizacionItemId, int CotizacionId, string Codigo, EstadoCotizacion Estado, int Cantidad);

public record MarcarPrecioDto(int PrecioId, string Codigo, int Cantidad);

public record CotizacionItemDto(
    int Id,
    int CotizacionId,
    int? ProductoProveedorPrecioId,
    int ProductoId,
    string ProductoNombre,
    string? ProductoCodigo,
    int ProveedorId,
    string ProveedorNombre,
    decimal PrecioUnitario,
    int Cantidad,
    decimal Subtotal);

public record ActualizarCantidadItemDto(int Cantidad);

public record CotizacionResumenDto(
    int Id,
    string Codigo,
    EstadoCotizacion Estado,
    int? Consecutivo,
    int? ClienteId,
    string? ClienteNombre,
    DateOnly? FechaEmision,
    DateTime FechaCreacion,
    int CantidadItems,
    decimal Total);

public record CotizacionDetalleDto(
    int Id,
    string Codigo,
    EstadoCotizacion Estado,
    int? Consecutivo,
    int? ClienteId,
    string? ClienteNombre,
    string? ClienteNit,
    string? FormaPago,
    string? Nota,
    DateOnly? FechaEmision,
    DateTime FechaCreacion,
    string? CreadoPor,
    IReadOnlyList<CotizacionItemDto> Items,
    decimal Total);

public record EmitirCotizacionDto(int ClienteId, string? FormaPago, string? Nota);
