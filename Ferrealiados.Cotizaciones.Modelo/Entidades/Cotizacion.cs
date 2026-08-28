using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

// Cabecera de una cotización a cliente. Nace como Borrador con un Codigo (etiqueta de trabajo
// temporal que escribe el cotizador al marcar ítems, ej. "PJ-5087") y sin Cliente ni Consecutivo.
// Al "cargarla a un cliente" (EmitirAsync) pasa a Emitida: se le asigna Cliente + Consecutivo
// (generado por una SQL SEQUENCE, ver AppDbContext) + FechaEmision, y queda fija para siempre.
public class Cotizacion
{
    public int Id { get; set; }

    [MaxLength(50)]
    public required string Codigo { get; set; }

    public EstadoCotizacion Estado { get; set; } = EstadoCotizacion.Borrador;

    // Null mientras es Borrador. Se asigna atómicamente (SEQUENCE) solo al emitir.
    public int? Consecutivo { get; set; }

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // Snapshot del cliente al momento de emitir: si el cliente se edita después, esta
    // cotización histórica no cambia de texto silenciosamente.
    [MaxLength(200)]
    public string? ClienteNombreSnapshot { get; set; }

    [MaxLength(50)]
    public string? ClienteNitSnapshot { get; set; }

    [MaxLength(200)]
    public string? ClienteContactoSnapshot { get; set; }

    [MaxLength(200)]
    public string? ClienteEmailSnapshot { get; set; }

    [MaxLength(300)]
    public string? ClienteDireccionSnapshot { get; set; }

    [MaxLength(100)]
    public string? ClienteCiudadSnapshot { get; set; }

    [MaxLength(50)]
    public string? FormaPago { get; set; }

    [MaxLength(500)]
    public string? Nota { get; set; }

    // Descuento global en pesos sobre el subtotal de ítems, capturado al emitir. 0 mientras es
    // Borrador. Se reparte proporcionalmente entre las tarifas de IVA presentes en los ítems
    // (ver CotizacionService.MapDetalle) para calcular el IVA correcto de cada tramo.
    [Column(TypeName = "decimal(18,2)")]
    public decimal Descuento { get; set; }

    public DateOnly? FechaEmision { get; set; }

    public DateTime FechaCreacion { get; set; }

    [MaxLength(200)]
    public string? CreadoPor { get; set; }

    // Concurrencia optimista: evita que el mismo borrador se emita dos veces en paralelo.
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<CotizacionItem> Items { get; set; } = new List<CotizacionItem>();
}
