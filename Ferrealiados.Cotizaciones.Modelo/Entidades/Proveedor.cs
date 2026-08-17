using System.ComponentModel.DataAnnotations;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

public class Proveedor
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Nombre { get; set; }

    [MaxLength(200)]
    public string? Contacto { get; set; }

    [MaxLength(50)]
    public string? Telefono { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Nit { get; set; }

    [MaxLength(300)]
    public string? Direccion { get; set; }

    [MaxLength(100)]
    public string? Ciudad { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<ProductoProveedorPrecio> Precios { get; set; } = new List<ProductoProveedorPrecio>();
}
