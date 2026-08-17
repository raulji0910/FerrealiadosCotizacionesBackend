namespace Ferrealiados.Cotizaciones.MigracionExcel;

public record FilaExcelCotizacion(
    int NumeroFila,
    DateOnly Fecha,
    string CodigoWo,
    string Producto,
    string Proveedor,
    decimal Costo);
