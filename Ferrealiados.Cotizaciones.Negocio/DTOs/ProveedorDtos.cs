namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record ProveedorDto(int Id, string Nombre, string? Nit, string? Direccion, string? Telefono, string? Ciudad, bool Activo);

public record ProveedorCrearDto(string Nombre, string? Nit, string? Direccion, string? Telefono, string? Ciudad);

public record ProveedorActualizarDto(string Nombre, string? Nit, string? Direccion, string? Telefono, string? Ciudad, bool Activo);
