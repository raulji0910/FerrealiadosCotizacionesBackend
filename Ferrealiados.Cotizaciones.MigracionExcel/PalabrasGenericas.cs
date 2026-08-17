namespace Ferrealiados.Cotizaciones.MigracionExcel;

// Códigos WO reutilizados en el Excel para servicios repetidos (no productos físicos únicos).
// Se excluyen de la migración inicial; se pueden cargar manualmente después si hace falta.
public static class PalabrasGenericas
{
    public static readonly string[] Lista =
    [
        "bordado",
        "bordados",
        "calibracion",
        "calibración"
    ];

    public static bool EsGenerico(string nombreProducto)
    {
        var normalizado = nombreProducto.ToLowerInvariant();
        return Lista.Any(palabra => normalizado.Contains(palabra, StringComparison.OrdinalIgnoreCase));
    }
}
