using System.ComponentModel.DataAnnotations;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

public enum RolUsuario
{
    Admin = 1,
    Cotizador = 2,
    // Solo entra al módulo de conciliación de facturas de proveedor.
    Conciliador = 3,
    // Solo entra al módulo de cartera (estado de cuenta a clientes y remisiones sin factura).
    Cartera = 4
}

public class Usuario
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Nombre { get; set; }

    [MaxLength(200)]
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    // Rol "principal" de antes del multirol (2026-11-19). Ya NO se usa para permisos (eso sale de
    // Roles); se sigue llenando con el más alto de los roles para que una versión anterior de la API
    // pueda volver a correr sobre la misma base si hay que devolverse.
    public RolUsuario Rol { get; set; } = RolUsuario.Cotizador;

    public bool Activo { get; set; } = true;

    // Roles del usuario (uno o varios); lo que ve es la suma de todos.
    public ICollection<UsuarioRol> Roles { get; set; } = new List<UsuarioRol>();
}

public class UsuarioRol
{
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public RolUsuario Rol { get; set; }
}
