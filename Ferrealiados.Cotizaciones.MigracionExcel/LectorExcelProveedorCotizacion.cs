using ClosedXML.Excel;

namespace Ferrealiados.Cotizaciones.MigracionExcel;

public record ResultadoLecturaProveedor(FilaProveedorMaestro? Fila, string? MotivoDescarte);

public record ResultadoLecturaCotizacionProveedor(FilaCotizacionProveedor? Fila, string? MotivoDescarte);

public static class LectorExcelProveedorCotizacion
{
    private const string HojaProveedores = "Proveedores";
    private const string HojaCotizaciones = "Cotizaciones";

    public static bool EsFormatoProveedorCotizacion(string rutaExcel)
    {
        using var libro = new XLWorkbook(rutaExcel);
        return libro.Worksheets.Contains(HojaProveedores) && libro.Worksheets.Contains(HojaCotizaciones);
    }

    // Hoja "Proveedores": Nombre, Identificacion, Dirección, Teléfonos, Ciudad.
    public static IEnumerable<ResultadoLecturaProveedor> LeerProveedores(string rutaExcel)
    {
        using var libro = new XLWorkbook(rutaExcel);
        var hoja = libro.Worksheet(HojaProveedores);
        var ultimaFila = hoja.LastRowUsed()!.RowNumber();

        for (var fila = 2; fila <= ultimaFila; fila++)
        {
            var nombre = hoja.Cell(fila, 1).GetString().Trim();
            var identificacion = hoja.Cell(fila, 2).GetString().Trim();
            var direccion = hoja.Cell(fila, 3).GetString().Trim();
            var telefonoCrudo = hoja.Cell(fila, 4).GetString();
            var ciudad = hoja.Cell(fila, 5).GetString().Trim();

            if (string.IsNullOrEmpty(nombre) && string.IsNullOrEmpty(identificacion))
                continue; // fila totalmente vacía

            if (string.IsNullOrEmpty(nombre))
            {
                yield return new ResultadoLecturaProveedor(null, $"Fila {fila}: Nombre de proveedor vacío");
                continue;
            }

            yield return new ResultadoLecturaProveedor(
                new FilaProveedorMaestro(
                    nombre,
                    string.IsNullOrEmpty(identificacion) ? null : identificacion,
                    string.IsNullOrEmpty(direccion) ? null : direccion,
                    LimpiarTelefono(telefonoCrudo),
                    string.IsNullOrEmpty(ciudad) ? null : ciudad),
                null);
        }
    }

    // Hoja "Cotizaciones": Fecha, Proveedor, Descripción ("CODIGO resto del nombre"), CostoUnidad.
    public static IEnumerable<ResultadoLecturaCotizacionProveedor> LeerCotizaciones(string rutaExcel)
    {
        using var libro = new XLWorkbook(rutaExcel);
        var hoja = libro.Worksheet(HojaCotizaciones);
        var ultimaFila = hoja.LastRowUsed()!.RowNumber();

        for (var fila = 2; fila <= ultimaFila; fila++)
        {
            var celdaFecha = hoja.Cell(fila, 1);
            var proveedor = hoja.Cell(fila, 2).GetString().Trim();
            var descripcion = hoja.Cell(fila, 3).GetString().Trim();
            var celdaCosto = hoja.Cell(fila, 4);

            if (string.IsNullOrEmpty(proveedor) && string.IsNullOrEmpty(descripcion))
                continue; // fila totalmente vacía

            if (!celdaFecha.TryGetValue(out DateTime fecha))
            {
                yield return new ResultadoLecturaCotizacionProveedor(null, $"Fila {fila}: Fecha inválida ('{celdaFecha.GetString()}')");
                continue;
            }

            if (string.IsNullOrEmpty(proveedor))
            {
                yield return new ResultadoLecturaCotizacionProveedor(null, $"Fila {fila}: Proveedor vacío");
                continue;
            }

            var partesDescripcion = descripcion.Split(' ', 2);
            if (partesDescripcion.Length < 2 || string.IsNullOrWhiteSpace(partesDescripcion[1]))
            {
                yield return new ResultadoLecturaCotizacionProveedor(null, $"Fila {fila}: Descripción sin código y nombre separables ('{descripcion}')");
                continue;
            }

            if (!celdaCosto.TryGetValue(out decimal costo) || costo <= 0)
            {
                yield return new ResultadoLecturaCotizacionProveedor(null, $"Fila {fila}: CostoUnidad inválido o cero");
                continue;
            }

            yield return new ResultadoLecturaCotizacionProveedor(
                new FilaCotizacionProveedor(
                    fila,
                    DateOnly.FromDateTime(fecha),
                    proveedor,
                    partesDescripcion[0].Trim(),
                    partesDescripcion[1].Trim(),
                    costo),
                null);
        }
    }

    // El Excel trae números repetidos separados por un salto de línea "roto" de Word (_x000D_).
    // Se deduplican y se conserva solo el/los número(s) distinto(s); "0" se trata como "sin dato".
    private static string? LimpiarTelefono(string? crudo)
    {
        if (string.IsNullOrWhiteSpace(crudo))
            return null;

        var partes = crudo.Replace("_x000D_", "\n")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p != "0")
            .Distinct()
            .ToList();

        if (partes.Count == 0)
            return null;

        var resultado = string.Join(", ", partes);
        return resultado.Length <= 50 ? resultado : resultado[..50];
    }
}
