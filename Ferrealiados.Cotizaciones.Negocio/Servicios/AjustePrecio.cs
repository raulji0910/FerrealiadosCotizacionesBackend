namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

// Regla de negocio: al registrar una cotización, el usuario puede aplicar un porcentaje de ajuste
// (positivo o negativo) sobre el costo que informó el proveedor.
public static class AjustePrecio
{
    public const int PorcentajeMinimo = -100;
    public const int PorcentajeMaximo = 100;

    public static decimal CalcularCostoFinal(decimal costoBase, int porcentajeAjuste)
        => Math.Round(costoBase * (1 + porcentajeAjuste / 100m), 2, MidpointRounding.AwayFromZero);

    // IVA es independiente del porcentaje de ajuste (impuesto vs. margen) — se calcula siempre
    // sobre CostoBase, nunca sobre Costo. Null cuando no se ha elegido tarifa de IVA.
    public static decimal? CalcularCostoConIva(decimal costoBase, int? iva)
        => iva is null ? null : Math.Round(costoBase * (1 + iva.Value / 100m), 2, MidpointRounding.AwayFromZero);
}
