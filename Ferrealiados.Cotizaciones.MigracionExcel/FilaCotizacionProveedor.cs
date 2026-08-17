namespace Ferrealiados.Cotizaciones.MigracionExcel;

public record FilaCotizacionProveedor(
    int NumeroFila,
    DateOnly Fecha,
    string Proveedor,
    string Codigo,
    string Nombre,
    decimal Costo);
