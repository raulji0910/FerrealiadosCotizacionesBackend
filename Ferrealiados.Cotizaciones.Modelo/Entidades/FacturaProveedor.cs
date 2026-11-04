using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

// Factura electrónica (XML DIAN) que un proveedor mandó a facturacion@ferrealiadosjv.com. La sube
// el Sincronizador; los campos salen del XML (LectorFacturaElectronica) y el XML se guarda completo
// porque la fase 2 (generar la factura de compra en World Office) va a necesitarlo.
// Identidad: el CUFE — el mismo correo puede llegar dos veces y no debe duplicarse.
public class FacturaProveedor
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Cufe { get; set; }

    [MaxLength(50)]
    public required string Numero { get; set; }

    // Código DIAN: "01" factura, "91" nota crédito, "92" nota débito.
    [MaxLength(5)]
    public required string TipoDocumento { get; set; }

    public DateOnly FechaEmision { get; set; }

    [MaxLength(20)]
    public required string NitEmisor { get; set; }

    [MaxLength(300)]
    public required string NombreEmisor { get; set; }

    [MaxLength(20)]
    public required string NitReceptor { get; set; }

    // Sin IVA y ya con descuentos a nivel factura (TaxExclusiveAmount del XML).
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Iva { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    // cac:OrderReference tal cual vino (ej. "OC_1275"); la mayoría de proveedores no lo mandan.
    [MaxLength(100)]
    public string? ReferenciaOrden { get; set; }

    public required string Xml { get; set; }

    [MaxLength(300)]
    public string? NombreArchivo { get; set; }

    [MaxLength(500)]
    public string? CorreoAsunto { get; set; }

    public DateTime? CorreoFecha { get; set; }

    public DateTime FechaRegistro { get; set; }

    public ICollection<FacturaProveedorRenglon> Renglones { get; set; } = new List<FacturaProveedorRenglon>();
}

public class FacturaProveedorRenglon
{
    public int Id { get; set; }

    public int FacturaProveedorId { get; set; }
    public FacturaProveedor? FacturaProveedor { get; set; }

    public int Orden { get; set; }

    [MaxLength(500)]
    public required string Descripcion { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? PorcentajeIva { get; set; }
}
