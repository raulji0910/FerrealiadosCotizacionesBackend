using System.ComponentModel.DataAnnotations;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

public class Producto
{
    public int Id { get; set; }

    // A diferencia de Jimaco, en Ferrealiados el código no es obligatorio: muchos ítems se
    // cotizan sin un código de catálogo asignado todavía. Se puede completar más adelante.
    [MaxLength(50)]
    public string? Codigo { get; set; }

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
