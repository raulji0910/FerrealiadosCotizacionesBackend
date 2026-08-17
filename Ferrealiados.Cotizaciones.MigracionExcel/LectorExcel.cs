using ClosedXML.Excel;

namespace Ferrealiados.Cotizaciones.MigracionExcel;

public record ResultadoLecturaFila(FilaExcelCotizacion? Fila, string? MotivoDescarte);

public static class LectorExcel
{
    private const int ColFecha = 1;
    private const int ColCodigoWo = 7;
    private const int ColProducto = 8;
    private const int ColProveedor = 9;
    private const int ColCosto = 10;

    public static IEnumerable<ResultadoLecturaFila> Leer(string rutaArchivo)
    {
        using var libro = new XLWorkbook(rutaArchivo);
        var hoja = libro.Worksheets.First();
        var ultimaFila = hoja.LastRowUsed()!.RowNumber();

        for (var fila = 2; fila <= ultimaFila; fila++)
        {
            var celdaFecha = hoja.Cell(fila, ColFecha);
            var codigoWo = hoja.Cell(fila, ColCodigoWo).GetString().Trim();
            var producto = hoja.Cell(fila, ColProducto).GetString().Trim();
            var proveedor = hoja.Cell(fila, ColProveedor).GetString().Trim();
            var celdaCosto = hoja.Cell(fila, ColCosto);

            if (string.IsNullOrEmpty(codigoWo) && string.IsNullOrEmpty(producto) && string.IsNullOrEmpty(proveedor))
                continue; // fila totalmente vacía, se ignora sin contar como descarte

            if (!celdaFecha.TryGetValue(out DateTime fecha))
            {
                yield return new ResultadoLecturaFila(null, $"Fila {fila}: FECHA inválida ('{celdaFecha.GetString()}')");
                continue;
            }

            if (string.IsNullOrEmpty(codigoWo))
            {
                yield return new ResultadoLecturaFila(null, $"Fila {fila}: CODIGO WO vacío");
                continue;
            }

            if (string.IsNullOrEmpty(proveedor))
            {
                yield return new ResultadoLecturaFila(null, $"Fila {fila}: PROVEEDOR vacío");
                continue;
            }

            if (!celdaCosto.TryGetValue(out decimal costo) || costo <= 0)
            {
                yield return new ResultadoLecturaFila(null, $"Fila {fila}: COSTO SIN IVA inválido o cero");
                continue;
            }

            var filaValida = new FilaExcelCotizacion(
                fila,
                DateOnly.FromDateTime(fecha),
                codigoWo,
                producto,
                proveedor,
                costo);

            yield return new ResultadoLecturaFila(filaValida, null);
        }
    }
}
