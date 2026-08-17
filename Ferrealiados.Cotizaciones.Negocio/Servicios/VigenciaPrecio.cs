namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

// Regla de negocio central: un precio cotizado deja de ser confiable pasados N meses sin recotizar.
public static class VigenciaPrecio
{
    public static bool EsVencido(DateOnly fechaCotizacion, int mesesVigencia, DateOnly hoy)
        => fechaCotizacion < hoy.AddMonths(-mesesVigencia);

    public static int DiasDesde(DateOnly fechaCotizacion, DateOnly hoy)
        => hoy.DayNumber - fechaCotizacion.DayNumber;
}
