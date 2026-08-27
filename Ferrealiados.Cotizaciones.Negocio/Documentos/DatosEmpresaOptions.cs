namespace Ferrealiados.Cotizaciones.Negocio.Documentos;

// Datos de la empresa que se muestran en el pie del PDF de cotización. Configurables vía
// appsettings.json / variables de entorno (sección "DatosEmpresa"), no hardcodeados, para poder
// ajustarlos sin recompilar (mismo patrón que Jwt__Secret / Cors__AllowedOrigins__0).
public class DatosEmpresaOptions
{
    public const string SeccionConfiguracion = "DatosEmpresa";

    public string Nombre { get; set; } = "FERREALIADOS JV";
    public string? Nit { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Ciudad { get; set; }
}
