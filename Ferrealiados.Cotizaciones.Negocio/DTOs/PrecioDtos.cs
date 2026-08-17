namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record PrecioProveedorDto(
    int PrecioId,
    int ProveedorId,
    string ProveedorNombre,
    decimal Costo,
    decimal CostoBase,
    int PorcentajeAjuste,
    DateOnly FechaCotizacion,
    int DiasDesdeCotizacion,
    bool Vencido,
    bool EsMejorPrecio);

// Costo: lo que informó el proveedor, antes de ajuste. PorcentajeAjuste: -100 a 100, entero.
public record RegistrarPrecioDto(
    int ProveedorId,
    decimal Costo,
    int PorcentajeAjuste,
    DateOnly FechaCotizacion,
    string? Observaciones,
    string? CreadoPor);

public record AlertaPrecioDto(
    int ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    int ProveedorId,
    string ProveedorNombre,
    decimal Costo,
    DateOnly FechaCotizacion,
    int DiasDesdeCotizacion);
