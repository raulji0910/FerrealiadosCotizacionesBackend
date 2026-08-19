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

// Edición rápida del % de ajuste desde la grilla — solo toca PorcentajeAjuste/Costo, nunca CostoBase.
public record ActualizarPorcentajeDto(int PorcentajeAjuste);

public record PrecioActualizadoDto(int PrecioId, decimal Costo, int PorcentajeAjuste);

public record AlertaPrecioDto(
    int ProductoId,
    string? ProductoCodigo,
    string ProductoNombre,
    int ProveedorId,
    string ProveedorNombre,
    decimal Costo,
    DateOnly FechaCotizacion,
    int DiasDesdeCotizacion);
