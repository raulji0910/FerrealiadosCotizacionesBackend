namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record PrecioProveedorDto(
    int PrecioId,
    int ProveedorId,
    string ProveedorNombre,
    decimal Costo,
    decimal CostoBase,
    int PorcentajeAjuste,
    int? Iva,
    decimal? CostoConIva,
    DateOnly FechaCotizacion,
    int DiasDesdeCotizacion,
    bool Vencido,
    bool EsMejorPrecio);

// Costo: lo que informó el proveedor, antes de ajuste. PorcentajeAjuste: -100 a 100, entero.
// Iva: 19, 5, o null si no aplica/no se indicó.
public record RegistrarPrecioDto(
    int ProveedorId,
    decimal Costo,
    int PorcentajeAjuste,
    int? Iva,
    DateOnly FechaCotizacion,
    string? Observaciones,
    string? CreadoPor);

// Edición rápida del % de ajuste desde la grilla — solo toca PorcentajeAjuste/Costo, nunca CostoBase.
public record ActualizarPorcentajeDto(int PorcentajeAjuste);

public record PrecioActualizadoDto(int PrecioId, decimal Costo, int PorcentajeAjuste);

// Edición rápida del IVA desde la grilla — independiente del PorcentajeAjuste, no toca CostoBase/Costo.
public record ActualizarIvaDto(int? Iva);

public record IvaActualizadoDto(int PrecioId, int? Iva, decimal? CostoConIva);

public record AlertaPrecioDto(
    int ProductoId,
    string? ProductoCodigo,
    string ProductoNombre,
    int ProveedorId,
    string ProveedorNombre,
    decimal Costo,
    DateOnly FechaCotizacion,
    int DiasDesdeCotizacion);
