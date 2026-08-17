using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

// Cada cotización de un proveedor para un producto queda como un registro nuevo (no se sobreescribe),
// así se conserva el histórico. El precio "vigente" de un par (Producto, Proveedor) es el de FechaCotizacion más reciente.
public class ProductoProveedorPrecio
{
    public int Id { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    // Costo final vigente (ya con el ajuste de porcentaje aplicado). Es el que usan alertas,
    // comparación de "mejor precio" y el resto del sistema.
    [Column(TypeName = "decimal(18,2)")]
    public decimal Costo { get; set; }

    // Costo tal como lo cotizó el proveedor, antes de aplicar PorcentajeAjuste. Se conserva para trazabilidad.
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostoBase { get; set; }

    // Porcentaje (positivo o negativo, entero) que se sumó/restó al CostoBase para obtener Costo.
    public int PorcentajeAjuste { get; set; }

    public DateOnly FechaCotizacion { get; set; }

    [MaxLength(500)]
    public string? Observaciones { get; set; }

    [MaxLength(200)]
    public string? CreadoPor { get; set; }

    public DateTime FechaRegistro { get; set; }
}
