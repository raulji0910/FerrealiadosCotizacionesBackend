using System.ComponentModel.DataAnnotations;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

public enum TipoObservacionOrden
{
    Observacion = 1,
    // Quien revisa marca que la OC no se va a facturar (devolución, compra anulada de hecho…). La OC
    // sigue en la lista de "OC sin factura", solo queda señalada.
    MarcaNoRequiereFactura = 2,
    QuitaMarcaNoRequiereFactura = 3
}

// Historial de lo que encontró quien revisa una OC sin factura. Nunca se edita ni se borra: lo que
// cambie se agrega como una entrada nueva. Se amarra al IdAsientoContable de WO (no al Id de la copia
// local) para que sobreviva aunque la OC se vuelva a sincronizar. Si llega la factura, la OC sale de
// la lista sola (el cruce es al vuelo) y su historial queda guardado.
public class ObservacionOrdenCompra
{
    public int Id { get; set; }

    public int IdAsientoContable { get; set; }

    public int NumeroOrden { get; set; }

    public TipoObservacionOrden Tipo { get; set; } = TipoObservacionOrden.Observacion;

    [MaxLength(2000)]
    public required string Texto { get; set; }

    [MaxLength(200)]
    public required string Usuario { get; set; }

    [MaxLength(200)]
    public required string NombreUsuario { get; set; }

    public DateTime Fecha { get; set; }
}
