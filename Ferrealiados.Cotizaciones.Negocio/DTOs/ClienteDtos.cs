namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record ClienteDto(int Id, string Nombre, string? Nit, string? Direccion, string? Telefono, string? Ciudad, string? Contacto, string? Email, bool Activo);

public record ClienteCrearDto(string Nombre, string? Nit, string? Direccion, string? Telefono, string? Ciudad, string? Contacto, string? Email);

public record ClienteActualizarDto(string Nombre, string? Nit, string? Direccion, string? Telefono, string? Ciudad, string? Contacto, string? Email, bool Activo);
