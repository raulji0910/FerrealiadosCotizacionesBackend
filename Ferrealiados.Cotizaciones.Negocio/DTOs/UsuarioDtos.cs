using Ferrealiados.Cotizaciones.Modelo.Entidades;

namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record UsuarioDto(int Id, string Nombre, string Email, RolUsuario Rol, bool Activo);

public record UsuarioCrearDto(string Nombre, string Email, string Password, RolUsuario Rol);

public record UsuarioActualizarDto(string Nombre, RolUsuario Rol, bool Activo, string? NuevaPassword);
