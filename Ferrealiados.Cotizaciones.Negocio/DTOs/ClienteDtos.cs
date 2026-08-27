namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record ClienteDto(int Id, string Nombre, string? Nit, string? Direccion, string? Telefono, string? Ciudad, bool Activo);

public record ClienteCrearDto(string Nombre, string? Nit, string? Direccion, string? Telefono, string? Ciudad);

public record ClienteActualizarDto(string Nombre, string? Nit, string? Direccion, string? Telefono, string? Ciudad, bool Activo);
