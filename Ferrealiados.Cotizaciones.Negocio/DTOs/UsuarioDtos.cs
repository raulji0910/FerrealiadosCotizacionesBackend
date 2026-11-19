using Ferrealiados.Cotizaciones.Modelo.Entidades;

namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

// Multirol (2026-11-19): un usuario tiene uno o varios roles de la lista fija y ve la suma de todos.
public record UsuarioDto(int Id, string Nombre, string Email, IReadOnlyList<RolUsuario> Roles, bool Activo);

public record UsuarioCrearDto(string Nombre, string Email, string Password, IReadOnlyList<RolUsuario> Roles);

public record UsuarioActualizarDto(string Nombre, IReadOnlyList<RolUsuario> Roles, bool Activo, string? NuevaPassword);