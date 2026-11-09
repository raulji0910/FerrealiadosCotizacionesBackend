using System.ComponentModel.DataAnnotations;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

public class ConfiguracionSistema
{
    public int Id { get; set; }

    [MaxLength(100)]
    public required string Clave { get; set; }

    [MaxLength(500)]
    public required string Valor { get; set; }

    [MaxLength(500)]
    public string? Descripcion { get; set; }
}

public static class ClavesConfiguracion
{
    public const string MesesVigenciaPrecio = "MESES_VIGENCIA_PRECIO";

    // Hora UTC (ISO 8601) de la última vez que el Sincronizador subió las OC de World Office. Se
    // guarda aparte porque las OC que no cambiaron ya no se reescriben en cada corrida.
    public const string UltimaSincronizacionOrdenes = "CONCILIACION_ULTIMA_SINCRONIZACION_OC";
}
