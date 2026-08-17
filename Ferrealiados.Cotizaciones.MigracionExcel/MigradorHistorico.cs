using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.MigracionExcel;

// Importa el Excel histórico original (una sola hoja plana: FECHA, COTIZACION, CLIENTE, ...,
// CODIGO WO, PRODUCTO, PROVEEDOR, COSTO SIN IVA, ...). Colapsa al precio más reciente por par
// (Producto, Proveedor) porque ese archivo trae años de historial repetido y ruidoso.
public static class MigradorHistorico
{
    public static async Task EjecutarAsync(string rutaExcel, AppDbContext db)
    {
        // --- 1. Leer el Excel, separando filas válidas de descartadas ---
        var filasValidas = new List<FilaExcelCotizacion>();
        var motivosDescartadas = new List<string>();

        foreach (var resultado in LectorExcel.Leer(rutaExcel))
        {
            if (resultado.Fila is not null)
                filasValidas.Add(resultado.Fila);
            else
                motivosDescartadas.Add(resultado.MotivoDescarte!);
        }

        Console.WriteLine($"Filas leídas válidas: {filasValidas.Count}");
        Console.WriteLine($"Filas descartadas (fecha/datos inválidos): {motivosDescartadas.Count}");

        // --- 2. Normalizar texto y excluir códigos genéricos ---
        var filasNormalizadas = new List<FilaExcelCotizacion>();
        var excluidasGenericas = 0;

        foreach (var fila in filasValidas)
        {
            var productoNormalizado = NormalizadorTexto.Normalizar(fila.Producto);

            if (PalabrasGenericas.EsGenerico(productoNormalizado))
            {
                excluidasGenericas++;
                continue;
            }

            // CodigoWo se normaliza a mayúsculas: la base de datos usa un índice único case-insensitive
            // (collation por defecto de SQL Server), y el Excel tiene el mismo código en distinta capitalización
            // (ej. "PJ-17230" y "pj-17230") que de otro modo se tratarían como productos distintos en memoria.
            filasNormalizadas.Add(fila with
            {
                Producto = productoNormalizado,
                Proveedor = NormalizadorTexto.Normalizar(fila.Proveedor),
                CodigoWo = fila.CodigoWo.Trim().ToUpperInvariant()
            });
        }

        Console.WriteLine($"Filas excluidas por código genérico (bordados/calibración/etc.): {excluidasGenericas}");

        // --- 3. Nombre canónico de producto por código: el de la fecha más reciente ---
        // Se trunca defensivamente al límite de la columna (Producto.Nombre MaxLength 500):
        // algunas descripciones del Excel son más largas que cualquier nombre de producto razonable.
        var nombreCanonicoPorCodigo = filasNormalizadas
            .GroupBy(f => f.CodigoWo)
            .ToDictionary(
                g => g.Key,
                g => Truncar(g.OrderByDescending(f => f.Fecha).ThenByDescending(f => f.NumeroFila).First().Producto, 500));

        // --- 4. Nombre de display de proveedor por clave normalizada (primero visto) ---
        var proveedorDisplayPorClave = new Dictionary<string, string>();
        foreach (var fila in filasNormalizadas)
        {
            var clave = NormalizadorTexto.ClaveNormalizada(fila.Proveedor);
            if (!proveedorDisplayPorClave.ContainsKey(clave))
                proveedorDisplayPorClave[clave] = Truncar(fila.Proveedor, 200);
        }

        // --- 5. Precio "vigente" por par (Código, Proveedor): el de la fecha más reciente ---
        var preciosVigentes = filasNormalizadas
            .GroupBy(f => (f.CodigoWo, ClaveProveedor: NormalizadorTexto.ClaveNormalizada(f.Proveedor)))
            .Select(g => g.OrderByDescending(f => f.Fecha).ThenByDescending(f => f.NumeroFila).First())
            .ToList();

        Console.WriteLine($"Productos distintos a migrar: {nombreCanonicoPorCodigo.Count}");
        Console.WriteLine($"Proveedores distintos a migrar: {proveedorDisplayPorClave.Count}");
        Console.WriteLine($"Precios (pares producto-proveedor) a migrar: {preciosVigentes.Count}");

        // --- 6. Cargar a la base de datos (idempotente: reutiliza lo que ya exista) ---
        var productosExistentes = await db.Productos.ToDictionaryAsync(p => p.Codigo, p => p);
        var proveedoresExistentes = await db.Proveedores.ToDictionaryAsync(p => NormalizadorTexto.ClaveNormalizada(p.Nombre), p => p);

        var productosCreados = 0;
        var proveedoresCreados = 0;

        foreach (var (codigo, nombre) in nombreCanonicoPorCodigo)
        {
            if (productosExistentes.ContainsKey(codigo))
                continue;

            var producto = new Producto
            {
                Codigo = codigo,
                Nombre = nombre,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            db.Productos.Add(producto);
            productosExistentes[codigo] = producto;
            productosCreados++;
        }

        foreach (var (clave, nombreDisplay) in proveedorDisplayPorClave)
        {
            if (proveedoresExistentes.ContainsKey(clave))
                continue;

            var proveedor = new Proveedor
            {
                Nombre = nombreDisplay,
                Activo = true
            };
            db.Proveedores.Add(proveedor);
            proveedoresExistentes[clave] = proveedor;
            proveedoresCreados++;
        }

        await db.SaveChangesAsync();

        var preciosExistentes = (await db.ProductoProveedorPrecios
                .Select(p => new { p.ProductoId, p.ProveedorId, p.FechaCotizacion })
                .ToListAsync())
            .ToHashSet();

        var preciosCreados = 0;
        foreach (var fila in preciosVigentes)
        {
            var producto = productosExistentes[fila.CodigoWo];
            var proveedor = proveedoresExistentes[NormalizadorTexto.ClaveNormalizada(fila.Proveedor)];

            if (preciosExistentes.Contains(new { ProductoId = producto.Id, ProveedorId = proveedor.Id, FechaCotizacion = fila.Fecha }))
                continue;

            db.ProductoProveedorPrecios.Add(new ProductoProveedorPrecio
            {
                ProductoId = producto.Id,
                ProveedorId = proveedor.Id,
                Costo = fila.Costo,
                FechaCotizacion = fila.Fecha,
                CreadoPor = "MigracionExcel:Historico",
                FechaRegistro = DateTime.UtcNow
            });
            preciosCreados++;
        }

        await db.SaveChangesAsync();

        Console.WriteLine();
        Console.WriteLine("=== Resumen de migración (formato histórico) ===");
        Console.WriteLine($"Productos creados:  {productosCreados}");
        Console.WriteLine($"Proveedores creados: {proveedoresCreados}");
        Console.WriteLine($"Precios cargados:   {preciosCreados}");
        Console.WriteLine($"Filas descartadas:  {motivosDescartadas.Count}");
        Console.WriteLine($"Filas excluidas por código genérico: {excluidasGenericas}");

        if (motivosDescartadas.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Primeros motivos de descarte:");
            foreach (var motivo in motivosDescartadas.Take(20))
                Console.WriteLine($"  - {motivo}");
        }
    }

    private static string Truncar(string texto, int maxLongitud)
        => texto.Length <= maxLongitud ? texto : texto[..maxLongitud];
}
