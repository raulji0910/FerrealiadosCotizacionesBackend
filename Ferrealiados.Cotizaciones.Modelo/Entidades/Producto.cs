using System.ComponentModel.DataAnnotations;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

public class Producto
{
    public int Id { get; set; }

    [MaxLength(50)]
    public required string Codigo { get; set; }

    [MaxLength(500)]
    public required string Nombre { get; set; }

    [MaxLength(1000)]
    public string? Descripcion { get; set; }

    [MaxLength(50)]
    public string? UnidadMedida { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public ICollection<ProductoProveedorPrecio> Precios { get; set; } = new List<ProductoProveedorPrecio>();
}
