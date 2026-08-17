using System.ComponentModel.DataAnnotations;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

public enum RolUsuario
{
    Admin = 1,
    Cotizador = 2
}

public class Usuario
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Nombre { get; set; }

    [MaxLength(200)]
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public RolUsuario Rol { get; set; } = RolUsuario.Cotizador;

    public bool Activo { get; set; } = true;
}
