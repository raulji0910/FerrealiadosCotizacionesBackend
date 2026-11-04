using Dapper;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Microsoft.Data.SqlClient;

namespace Ferrealiados.Cotizaciones.Sincronizador;

/// <summary>
/// Lecturas de solo lectura contra la base de World Office de Ferrealiados (FERRALIADOS2026),
/// mismo esquema que Jimaco (ver Jimaco.Aprobaciones.Sincronizador/WorldOfficeReader.cs y
/// ProyectosJimaco/ConciliacionFerrealiados/exploracion-wo-ferrealiados.sql para cómo se confirmó).
/// </summary>
public class WorldOfficeReader(string connectionString)
{
    private record CabeceraRow(int IdAsientoContable, decimal DocumentoNumero, DateTime Fecha, string? Identificacion,
        string? Nombre, string? Apellidos, bool Anulada);

    private record RenglonRow(int IdAsientoContable, string? Codigo, string? Descripcion, decimal? Cantidad,
        decimal? ValorUnitario, decimal? Iva);

    /// <summary>
    /// Todas las OC desde <paramref name="desde"/>, anuladas incluidas (marcadas), para que la API
    /// deje de usar una OC que se anuló después de haberla subido. Se manda la ventana completa en
    /// cada corrida (unas pocas centenas de OC) porque WO no tiene una fecha de modificación
    /// confiable y una OC se puede seguir editando después de creada.
    /// </summary>
    public async Task<IReadOnlyList<OrdenCompraEntradaDto>> ObtenerOrdenesCompraAsync(string prefijo, DateOnly desde, CancellationToken ct = default)
    {
        const string sqlCabeceras = """
            SELECT
                a.IdAsientoContable,
                a.DocumentoNúmero AS DocumentoNumero,
                a.Fecha,
                t.Identificacion,
                t.Nombre,
                t.Apellidos,
                CAST(CASE WHEN a.senAnulado = -1 THEN 1 ELSE 0 END AS bit) AS Anulada
            FROM [CuentasContables - Asientos] a
            LEFT JOIN Terceros t ON t.IdTercero = a.IdTerceroExterno
            WHERE a.prefijo = @Prefijo AND a.Fecha >= @Desde;
            """;

        // Costo = Cantidad x MontoMonetarioUnitario; NO TotalRenglon (en Ferrealiados trae un valor
        // con margen, ~1.16-1.25x el costo). El IVA viene del producto (Inventarios.Iva, fracción:
        // 0.19), porque la línea no trae su propio porcentaje.
        const string sqlRenglones = """
            SELECT
                c.IdAsientoContable,
                inv.CódigoInventario       AS Codigo,
                inv.Descripción            AS Descripcion,
                c.Cantidad,
                c.MontoMonetarioUnitario   AS ValorUnitario,
                inv.Iva
            FROM CCA_M_Inventarios c
            JOIN [CuentasContables - Asientos] a ON a.IdAsientoContable = c.IdAsientoContable
            LEFT JOIN Inventarios inv ON inv.IdInventario = c.IdInventario
            WHERE a.prefijo = @Prefijo AND a.Fecha >= @Desde
            ORDER BY c.IdAsientoContable, c.[Renglón];
            """;

        await using var conexion = new SqlConnection(connectionString);
        var parametros = new { Prefijo = prefijo, Desde = desde.ToDateTime(TimeOnly.MinValue) };
        var cabeceras = await conexion.QueryAsync<CabeceraRow>(new CommandDefinition(sqlCabeceras, parametros, cancellationToken: ct));
        var renglones = (await conexion.QueryAsync<RenglonRow>(new CommandDefinition(sqlRenglones, parametros, cancellationToken: ct)))
            .ToLookup(r => r.IdAsientoContable);

        return cabeceras.Select(c => new OrdenCompraEntradaDto(
                c.IdAsientoContable,
                (int)c.DocumentoNumero,
                DateOnly.FromDateTime(c.Fecha),
                c.Identificacion?.Trim() ?? "",
                $"{c.Nombre} {c.Apellidos}".Trim(),
                c.Anulada,
                renglones[c.IdAsientoContable]
                    .Select(r => new OrdenCompraRenglonEntradaDto(
                        r.Codigo?.Trim(),
                        r.Descripcion?.Trim() ?? "",
                        r.Cantidad ?? 0m,
                        r.ValorUnitario ?? 0m,
                        (r.Iva ?? 0m) * 100m))
                    .ToList()))
            .ToList();
    }
}
