using System.ComponentModel.DataAnnotations;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

// Copia del encabezado de una factura de compra de World Office (prefijo "FC" en
// [CuentasContables - Asientos]) que sube el Sincronizador. Se usa como último recurso del cruce:
// Contabilidad escribe en Personalizado1 la(s) OC que cubre la factura. Cada sincronización
// reemplaza la ventana completa. Identidad: IdAsientoContable de WO.
public class FacturaCompraWorldOffice
{
    public int Id { get; set; }

    public int IdAsientoContable { get; set; }

    public int Numero { get; set; }

    public DateOnly Fecha { get; set; }

    [MaxLength(20)]
    public required string NitProveedor { get; set; }

    [MaxLength(300)]
    public required string NombreProveedor { get; set; }

    // NúmDocumentoExterno (con DocExternoPrefijo adelante si lo tiene): el número de la factura del
    // proveedor, el mismo del XML.
    [MaxLength(100)]
    public string? DocumentoExterno { get; set; }

    // Personalizado1 tal cual lo escribió Contabilidad ("1187", "1120-1187", "-").
    [MaxLength(200)]
    public string? ReferenciaOrdenes { get; set; }

    public bool Anulada { get; set; }

    public DateTime FechaSincronizacion { get; set; }
}
