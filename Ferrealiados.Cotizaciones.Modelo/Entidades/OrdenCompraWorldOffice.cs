using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

// Copia de una OC de World Office (prefijo "OC" en [CuentasContables - Asientos]) que sube el
// Sincronizador para cruzarla contra las facturas de los proveedores. WO es la fuente de verdad:
// cada sincronización reemplaza cabecera y renglones. Identidad: IdAsientoContable de WO.
public class OrdenCompraWorldOffice
{
    public int Id { get; set; }

    public int IdAsientoContable { get; set; }

    public int Numero { get; set; }

    public DateOnly Fecha { get; set; }

    [MaxLength(20)]
    public required string NitProveedor { get; set; }

    [MaxLength(300)]
    public required string NombreProveedor { get; set; }

    // SUM(Cantidad * MontoMonetarioUnitario) — NO SUM(TotalRenglon), que en Ferrealiados trae un
    // valor con margen y no el costo que se le paga al proveedor.
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Iva { get; set; }

    public bool Anulada { get; set; }

    public DateTime FechaSincronizacion { get; set; }

    public ICollection<OrdenCompraWorldOfficeRenglon> Renglones { get; set; } = new List<OrdenCompraWorldOfficeRenglon>();
}

public class OrdenCompraWorldOfficeRenglon
{
    public int Id { get; set; }

    public int OrdenCompraWorldOfficeId { get; set; }
    public OrdenCompraWorldOffice? OrdenCompraWorldOffice { get; set; }

    public int Orden { get; set; }

    [MaxLength(100)]
    public string? Codigo { get; set; }

    [MaxLength(500)]
    public required string Descripcion { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Cantidad { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ValorUnitario { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal PorcentajeIva { get; set; }
}
