namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record ProductoDto(int Id, string? Codigo, string Nombre, string? UnidadMedida, bool Activo, DateOnly? UltimaFechaCotizacion);

public record ProductoCrearDto(string? Codigo, string Nombre, string? UnidadMedida);

public record ProductoActualizarDto(string? Codigo, string Nombre, string? UnidadMedida, bool Activo);
