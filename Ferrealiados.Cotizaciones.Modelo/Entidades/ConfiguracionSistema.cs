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
}
