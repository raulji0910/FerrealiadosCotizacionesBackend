using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

// Línea de una cotización. No tiene FK real hacia Producto/Proveedor/ProductoProveedorPrecio:
// solo guarda los IDs (para trazabilidad y para poder desmarcar mientras es Borrador) y un
// snapshot completo de los valores relevantes al momento de marcar. Así la cotización queda
// fija en el tiempo aunque el producto, el proveedor o el precio cambien después.
public class CotizacionItem
{
    public int Id { get; set; }

    public int CotizacionId { get; set; }
    public Cotizacion? Cotizacion { get; set; }

    // Solo se usa mientras la cotización es Borrador (para ubicar y quitar la marca). No se
    // valida contra la tabla de precios: si el precio se borra después, este Id simplemente
    // deja de resolver a nada y el ítem sigue existiendo con su snapshot intacto.
    public int? ProductoProveedorPrecioId { get; set; }

    public int ProductoId { get; set; }

    [MaxLength(500)]
    public required string ProductoNombreSnapshot { get; set; }

    [MaxLength(50)]
    public string? ProductoCodigoSnapshot { get; set; }

    public int ProveedorId { get; set; }

    [MaxLength(200)]
    public required string ProveedorNombreSnapshot { get; set; }

    // = ProductoProveedorPrecio.Costo (costo con % de ajuste ya aplicado, sin IVA) al momento
    // de marcar. Es el precio que ve el cliente; congelado, no se recalcula después. Editable
    // luego mientras la cotización es Borrador (ver CotizacionService.ActualizarPrecioItemAsync).
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrecioUnitario { get; set; }

    // = ProductoProveedorPrecio.CostoBase al momento de marcar (lo que de verdad cuesta el
    // producto, sin el % de ajuste). Congelado — nunca se actualiza, ni al fusionar una marca
    // repetida ni al editar PrecioUnitario. Sirve solo para calcular el % de ganancia informativo
    // (ver CotizacionService.MapItem); nullable porque los ítems marcados antes de este campo no
    // lo tienen.
    [Column(TypeName = "decimal(18,2)")]
    public decimal? CostoBaseSnapshot { get; set; }

    // = ProductoProveedorPrecio.Iva al momento de marcar (19, 5, 0 o null si no se había
    // indicado). Distintos ítems de una misma cotización pueden traer tarifas distintas — el PDF
    // desglosa el IVA del total por cada tarifa presente (ver CotizacionService.MapDetalle).
    public int? IvaSnapshot { get; set; }

    public int Cantidad { get; set; } = 1;

    public DateTime FechaMarcado { get; set; }

    [MaxLength(200)]
    public string? MarcadoPor { get; set; }
}
