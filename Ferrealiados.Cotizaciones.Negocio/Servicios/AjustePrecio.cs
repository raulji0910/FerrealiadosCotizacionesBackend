namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

// Regla de negocio: al registrar una cotización, el usuario puede aplicar un porcentaje de ajuste
// (positivo o negativo) sobre el costo que informó el proveedor.
public static class AjustePrecio
{
    public const int PorcentajeMinimo = -100;
    public const int PorcentajeMaximo = 100;

    public static decimal CalcularCostoFinal(decimal costoBase, int porcentajeAjuste)
        => Math.Round(costoBase * (1 + porcentajeAjuste / 100m), 2, MidpointRounding.AwayFromZero);
}
