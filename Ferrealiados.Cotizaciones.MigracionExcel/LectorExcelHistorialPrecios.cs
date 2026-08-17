using System.Globalization;
using ClosedXML.Excel;

namespace Ferrealiados.Cotizaciones.MigracionExcel;

public record ResultadoLecturaHistorialPrecio(FilaHistorialPrecio? Fila, string? MotivoDescarte);

// "historial de precios": exportado directo de una consulta SQL (columnas VALOR COMPRA/NIT PROVEE
// llegan como número, FECHA DE COMPRA llega como texto "yyyy-MM-dd HH:mm:ss.fff", no como fecha real).
public static class LectorExcelHistorialPrecios
{
    private const string HojaHistorial = "historial de precios";
    private static readonly string[] FormatosFecha = ["yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd"];

    public static bool EsFormatoHistorialPrecios(string rutaExcel)
    {
        using var libro = new XLWorkbook(rutaExcel);
        return libro.Worksheets.Contains(HojaHistorial);
    }

    // Columnas: CODIGO, PRODUCTO, FECHA DE COMPRA, NIT PROVEE, PROVEEDOR, VALOR COMPRA, COSTO.
    public static IEnumerable<ResultadoLecturaHistorialPrecio> LeerFilas(string rutaExcel)
    {
        using var libro = new XLWorkbook(rutaExcel);
        var hoja = libro.Worksheet(HojaHistorial);
        var ultimaFila = hoja.LastRowUsed()!.RowNumber();

        for (var fila = 2; fila <= ultimaFila; fila++)
        {
            var codigo = hoja.Cell(fila, 1).GetString().Trim();
            var producto = hoja.Cell(fila, 2).GetString().Trim();
            var celdaFecha = hoja.Cell(fila, 3);
            var celdaNit = hoja.Cell(fila, 4);
            var proveedor = hoja.Cell(fila, 5).GetString().Trim();
            var celdaCosto = hoja.Cell(fila, 7);

            if (string.IsNullOrEmpty(codigo) && string.IsNullOrEmpty(producto) && string.IsNullOrEmpty(proveedor))
                continue; // fila totalmente vacía

            if (string.IsNullOrEmpty(codigo))
            {
                yield return new ResultadoLecturaHistorialPrecio(null, $"Fila {fila}: CODIGO vacío");
                continue;
            }

            if (string.IsNullOrEmpty(producto))
            {
                yield return new ResultadoLecturaHistorialPrecio(null, $"Fila {fila}: PRODUCTO vacío");
                continue;
            }

            if (!TryLeerFecha(celdaFecha, out var fecha))
            {
                yield return new ResultadoLecturaHistorialPrecio(null, $"Fila {fila}: FECHA DE COMPRA inválida ('{celdaFecha.GetString()}')");
                continue;
            }

            if (!TryLeerNit(celdaNit, out var nit))
            {
                yield return new ResultadoLecturaHistorialPrecio(null, $"Fila {fila}: NIT PROVEE inválido ('{celdaNit.GetString()}')");
                continue;
            }

            if (string.IsNullOrEmpty(proveedor))
            {
                yield return new ResultadoLecturaHistorialPrecio(null, $"Fila {fila}: PROVEEDOR vacío");
                continue;
            }

            if (!celdaCosto.TryGetValue(out decimal costo) || costo <= 0)
            {
                yield return new ResultadoLecturaHistorialPrecio(null, $"Fila {fila}: COSTO inválido o cero ('{celdaCosto.GetString()}')");
                continue;
            }

            yield return new ResultadoLecturaHistorialPrecio(
                new FilaHistorialPrecio(fila, codigo, producto, fecha, nit, proveedor, costo),
                null);
        }
    }

    private static bool TryLeerFecha(IXLCell celda, out DateOnly fecha)
    {
        if (celda.TryGetValue(out DateTime valorFecha))
        {
            fecha = DateOnly.FromDateTime(valorFecha);
            return true;
        }

        var texto = celda.GetString().Trim();
        if (DateTime.TryParseExact(texto, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parseada))
        {
            fecha = DateOnly.FromDateTime(parseada);
            return true;
        }

        fecha = default;
        return false;
    }

    private static bool TryLeerNit(IXLCell celda, out string nit)
    {
        if (celda.TryGetValue(out double valorNumerico) && valorNumerico > 0)
        {
            nit = ((long)valorNumerico).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        var texto = celda.GetString().Trim();
        if (texto.Length > 0 && texto.All(char.IsDigit))
        {
            nit = texto;
            return true;
        }

        nit = string.Empty;
        return false;
    }
}
