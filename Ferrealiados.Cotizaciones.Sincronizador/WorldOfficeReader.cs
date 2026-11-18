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

    private record FacturaCompraRow(int IdAsientoContable, decimal DocumentoNumero, DateTime Fecha, string? Identificacion,
        string? Nombre, string? Apellidos, string? DocExternoPrefijo, string? DocumentoExterno, string? Personalizado1, bool Anulada);

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

    /// <summary>
    /// Encabezados de las FC desde <paramref name="desde"/> (anuladas marcadas). Contabilidad escribe
    /// en Personalizado1 la(s) OC que cubre la factura y en NúmDocumentoExterno el número de la
    /// factura del proveedor: con eso la API cruza como último recurso (ver PASO 14-15 del script de
    /// exploración).
    /// </summary>
    public async Task<IReadOnlyList<FacturaCompraEntradaDto>> ObtenerFacturasCompraAsync(string prefijo, DateOnly desde, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                a.IdAsientoContable,
                a.DocumentoNúmero          AS DocumentoNumero,
                a.Fecha,
                t.Identificacion,
                t.Nombre,
                t.Apellidos,
                a.DocExternoPrefijo,
                a.NúmDocumentoExterno      AS DocumentoExterno,
                a.Personalizado1,
                CAST(CASE WHEN a.senAnulado = -1 THEN 1 ELSE 0 END AS bit) AS Anulada
            FROM [CuentasContables - Asientos] a
            LEFT JOIN Terceros t ON t.IdTercero = a.IdTerceroExterno
            WHERE a.prefijo = @Prefijo AND a.Fecha >= @Desde;
            """;

        await using var conexion = new SqlConnection(connectionString);
        var filas = await conexion.QueryAsync<FacturaCompraRow>(new CommandDefinition(sql,
            new { Prefijo = prefijo, Desde = desde.ToDateTime(TimeOnly.MinValue) }, cancellationToken: ct));

        return filas.Select(f => new FacturaCompraEntradaDto(
                f.IdAsientoContable,
                (int)f.DocumentoNumero,
                DateOnly.FromDateTime(f.Fecha),
                f.Identificacion?.Trim() ?? "",
                $"{f.Nombre} {f.Apellidos}".Trim(),
                $"{f.DocExternoPrefijo?.Trim()}{f.DocumentoExterno?.Trim()}",
                f.Personalizado1?.Trim(),
                f.Anulada))
            .ToList();
    }

    private record PartidaCarteraRow(int? AbonaADocumento, string? Identificacion, string? NombresTerceros, string? Documento,
        string? Prefijo, decimal? DocumentoNumero, DateTime? Fecha, DateTime? Vencimiento, decimal? Saldo, string? CodigoCuentaContable);

    private record CorreoTerceroRow(string? Identificacion, string? Nombre, string? Apellidos, string? EMail, string? EMail2, string? EMail3);

    /// <summary>
    /// Cartera de clientes abierta desde <c>Vista_CuentasPorCobrar_Detallada</c> (la vista de WO que ya
    /// descuenta los abonos). La vista trae también anticipos a proveedores (1330) y préstamos a
    /// empleados/socios (1325): solo se toman las cuentas que empiezan por
    /// <paramref name="prefijoCuenta"/> (1305 clientes). Incluye las remisiones (en Ferrealiados se
    /// contabilizan en cartera y la FVE las cruza al facturar): la API las separa.
    /// </summary>
    public async Task<IReadOnlyList<PartidaCarteraEntradaDto>> ObtenerCarteraClientesAsync(string prefijoCuenta, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                AbonaADocumento,
                Identificacion,
                Nombres_terceros     AS NombresTerceros,
                Documento,
                prefijo              AS Prefijo,
                DocumentoNumero,
                Fecha,
                Vencimiento,
                Saldo,
                CodigoCuentaContable
            FROM Vista_CuentasPorCobrar_Detallada
            WHERE Saldo <> 0 AND CodigoCuentaContable LIKE @Cuenta;
            """;

        await using var conexion = new SqlConnection(connectionString);
        var filas = await conexion.QueryAsync<PartidaCarteraRow>(new CommandDefinition(sql, new { Cuenta = prefijoCuenta + "%" }, cancellationToken: ct));

        return filas
            .Where(f => f.AbonaADocumento is not null && !string.IsNullOrWhiteSpace(f.Identificacion))
            .Select(f => new PartidaCarteraEntradaDto(
                f.AbonaADocumento!.Value,
                f.CodigoCuentaContable?.Trim() ?? "",
                f.Identificacion!.Trim(),
                f.NombresTerceros?.Trim() ?? "",
                f.Documento?.Trim() ?? "",
                f.Prefijo?.Trim() ?? "",
                (long)(f.DocumentoNumero ?? 0m),
                DateOnly.FromDateTime(f.Fecha ?? DateTime.Today),
                DateOnly.FromDateTime(f.Vencimiento ?? f.Fecha ?? DateTime.Today),
                f.Saldo ?? 0m))
            .ToList();
    }

    /// <summary>
    /// Email 1/2/3 de las direcciones de los clientes en WO (<c>[Terceros - Direcciones]</c>). Son los
    /// de facturación: la app los muestra solo como referencia para armar los contactos de cobro.
    /// </summary>
    public async Task<IReadOnlyList<CorreosClienteWorldOfficeDto>> ObtenerCorreosClientesAsync(IReadOnlyCollection<string> nits, CancellationToken ct = default)
    {
        if (nits.Count == 0) return [];

        const string sql = """
            SELECT
                LTRIM(RTRIM(t.Identificacion)) AS Identificacion,
                t.Nombre,
                t.Apellidos,
                d.EMail,
                d.EMail2,
                d.EMail3
            FROM Terceros t
            JOIN [Terceros - Direcciones] d ON d.IdTercero = t.IdTercero
            WHERE LTRIM(RTRIM(t.Identificacion)) IN @Nits;
            """;

        await using var conexion = new SqlConnection(connectionString);
        var filas = await conexion.QueryAsync<CorreoTerceroRow>(new CommandDefinition(sql, new { Nits = nits.ToArray() }, cancellationToken: ct));

        return filas
            .Where(f => !string.IsNullOrWhiteSpace(f.Identificacion))
            .GroupBy(f => f.Identificacion!.Trim())
            .Select(g => new CorreosClienteWorldOfficeDto(
                g.Key,
                $"{g.First().Nombre} {g.First().Apellidos}".Trim(),
                g.SelectMany(f => new[] { f.EMail, f.EMail2, f.EMail3 })
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();
    }
}
