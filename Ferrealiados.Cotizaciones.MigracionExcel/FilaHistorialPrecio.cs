namespace Ferrealiados.Cotizaciones.MigracionExcel;

public record FilaHistorialPrecio(
    int NumeroFila,
    string Codigo,
    string Nombre,
    DateOnly Fecha,
    string Nit,
    string Proveedor,
    decimal Costo);
