using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.MigracionExcel;

// Importa "historial de precios" (ej. HistoriaPreciosNuevo.xlsx): exportación de compras reales,
// una fila por compra (Producto+Proveedor+Fecha+Costo), no un catálogo curado.
// - Producto: se matchea por Código; si ya existe se refresca el Nombre con el del Excel (igual que
//   el formato Proveedores/Cotizaciones); si no existe se crea.
// - Proveedor: el NIT de este Excel viene limpio (solo dígitos), pero el NIT ya guardado en la BD de
//   proveedores viejos puede venir sucio (ej. "NIT 900551058 5", texto + dígito de verificación pegado,
//   heredado del formato anterior). Por eso el match es por NIT normalizado (solo dígitos, sin el
//   dígito de verificación final cuando el NIT guardado tiene 10 dígitos) contra el NIT crudo del
//   Excel — así no se duplican los ~93% de proveedores que ya existían con NIT sucio. Los proveedores
//   existentes NO se tocan (ni Nombre ni Nit); solo se crean los que de verdad no matchean.
// - Precio: NO se colapsan filas con el mismo Producto+Proveedor+Fecha pero costo distinto (son
//   compras reales distintas el mismo día) — por eso la idempotencia usa Producto+Proveedor+Fecha+Costo
//   en vez del triple que usa el otro formato: permite reimportar el mismo archivo sin duplicar,
//   sin perder compras legítimas a precio distinto el mismo día.
public static class MigradorHistorialPrecios
{
    public static async Task EjecutarAsync(string rutaExcel, AppDbContext db)
    {
        // --- 1. Leer y normalizar filas ---
        var filasValidas = new List<FilaHistorialPrecio>();
        var motivosDescarte = new List<string>();
        foreach (var resultado in LectorExcelHistorialPrecios.LeerFilas(rutaExcel))
        {
            if (resultado.Fila is not null)
                filasValidas.Add(resultado.Fila);
            else
                motivosDescarte.Add(resultado.MotivoDescarte!);
        }

        Console.WriteLine($"Hoja 'historial de precios' — filas válidas: {filasValidas.Count}, descartadas: {motivosDescarte.Count}");

        var filasNormalizadas = filasValidas
            .Select(f => f with
            {
                Codigo = f.Codigo.Trim().ToUpperInvariant(),
                Nombre = NormalizadorTexto.Normalizar(f.Nombre),
                Proveedor = NormalizadorTexto.Normalizar(f.Proveedor),
                // La columna Costo de la BD es decimal(18,2); el Excel trae más decimales (costo unitario
                // calculado, ej. 1867.4444444444443). Si no se redondea acá, la comparación de idempotencia
                // contra el valor ya redondeado que vuelve de la BD nunca coincide y cada re-corrida
                // duplica filas.
                Costo = Math.Round(f.Costo, 2, MidpointRounding.AwayFromZero)
            })
            .ToList();

        // --- 2. Nombre canónico de producto por código: el de la fecha más reciente en este archivo ---
        var nombreCanonicoPorCodigo = filasNormalizadas
            .GroupBy(f => f.Codigo)
            .ToDictionary(
                g => g.Key,
                g => Truncar(g.OrderByDescending(f => f.Fecha).ThenByDescending(f => f.NumeroFila).First().Nombre, 500));

        // --- 3. Proveedores: uno por NIT (1:1 con Nombre en este archivo, ya validado) ---
        var proveedorPorNit = new Dictionary<string, string>(); // nit crudo -> nombre
        foreach (var fila in filasNormalizadas)
            proveedorPorNit.TryAdd(fila.Nit, Truncar(fila.Proveedor, 200));

        var proveedoresExistentes = await db.Proveedores.ToListAsync();
        var proveedorPorNitBaseExistente = new Dictionary<string, Proveedor>();
        var proveedorPorNombreExistente = new Dictionary<string, Proveedor>();
        foreach (var proveedor in proveedoresExistentes)
        {
            var baseNit = NormalizarNitBase(proveedor.Nit);
            if (baseNit is not null)
                proveedorPorNitBaseExistente.TryAdd(baseNit, proveedor);

            proveedorPorNombreExistente.TryAdd(NormalizadorTexto.ClaveNormalizada(proveedor.Nombre), proveedor);
        }

        var proveedorPorNitCrudo = new Dictionary<string, Proveedor>();
        var proveedoresCreados = 0;
        foreach (var (nit, nombre) in proveedorPorNit)
        {
            // El NIT es el match preferido (más confiable que el nombre para este archivo), pero
            // Proveedores.Nombre tiene índice único: si ya existe un proveedor con ese nombre (aunque
            // su NIT esté vacío o no haya matcheado), hay que reutilizarlo — si no, el insert revienta
            // la restricción única en vez de crear un duplicado.
            if (proveedorPorNitBaseExistente.TryGetValue(nit, out var porNit))
            {
                proveedorPorNitCrudo[nit] = porNit;
                continue;
            }

            if (proveedorPorNombreExistente.TryGetValue(NormalizadorTexto.ClaveNormalizada(nombre), out var porNombre))
            {
                proveedorPorNitCrudo[nit] = porNombre;
                continue;
            }

            var nuevo = new Proveedor { Nombre = nombre, Nit = nit, Activo = true };
            db.Proveedores.Add(nuevo);
            proveedorPorNitCrudo[nit] = nuevo;
            proveedorPorNombreExistente[NormalizadorTexto.ClaveNormalizada(nombre)] = nuevo;
            proveedoresCreados++;
        }

        await db.SaveChangesAsync();

        // --- 4. Productos: crear o refrescar nombre ---
        var productosExistentes = await db.Productos.ToDictionaryAsync(p => p.Codigo, p => p);
        var productosCreados = 0;
        var productosActualizados = 0;

        foreach (var (codigo, nombre) in nombreCanonicoPorCodigo)
        {
            if (productosExistentes.TryGetValue(codigo, out var existente))
            {
                if (existente.Nombre != nombre)
                {
                    existente.Nombre = nombre;
                    productosActualizados++;
                }
            }
            else
            {
                var nuevo = new Producto { Codigo = codigo, Nombre = nombre, Activo = true, FechaCreacion = DateTime.UtcNow };
                db.Productos.Add(nuevo);
                productosExistentes[codigo] = nuevo;
                productosCreados++;
            }
        }

        await db.SaveChangesAsync();

        // --- 5. Precios: una fila = un registro; idempotente por Producto+Proveedor+Fecha+Costo ---
        var preciosExistentes = (await db.ProductoProveedorPrecios
                .Select(p => new { p.ProductoId, p.ProveedorId, p.FechaCotizacion, p.Costo })
                .ToListAsync())
            .ToHashSet();

        var preciosCreados = 0;
        var preciosOmitidosPorDuplicado = 0;
        foreach (var fila in filasNormalizadas)
        {
            var producto = productosExistentes[fila.Codigo];
            var proveedor = proveedorPorNitCrudo[fila.Nit];
            var clave = new { ProductoId = producto.Id, ProveedorId = proveedor.Id, FechaCotizacion = fila.Fecha, Costo = fila.Costo };

            if (preciosExistentes.Contains(clave))
            {
                preciosOmitidosPorDuplicado++;
                continue;
            }

            db.ProductoProveedorPrecios.Add(new ProductoProveedorPrecio
            {
                ProductoId = producto.Id,
                ProveedorId = proveedor.Id,
                // Este Excel no trae porcentaje de ajuste: Costo = CostoBase, PorcentajeAjuste = 0.
                // CostoBase es el costo "normal" que usa el sistema para comparar/ordenar (mejor
                // precio, alertas); Costo queda igual porque no hay ajuste que aplicar.
                Costo = fila.Costo,
                CostoBase = fila.Costo,
                PorcentajeAjuste = 0,
                FechaCotizacion = fila.Fecha,
                CreadoPor = "MigracionExcel:HistorialPrecios",
                FechaRegistro = DateTime.UtcNow
            });
            preciosExistentes.Add(clave);
            preciosCreados++;
        }

        await db.SaveChangesAsync();

        Console.WriteLine();
        Console.WriteLine("=== Resumen de migración (formato Historial de precios) ===");
        Console.WriteLine($"Proveedores creados:              {proveedoresCreados}");
        Console.WriteLine($"Proveedores reutilizados (match):  {proveedorPorNitCrudo.Count - proveedoresCreados}");
        Console.WriteLine($"Productos creados:                 {productosCreados}");
        Console.WriteLine($"Productos actualizados (nombre):   {productosActualizados}");
        Console.WriteLine($"Precios cargados:                  {preciosCreados}");
        Console.WriteLine($"Precios omitidos (ya existían):    {preciosOmitidosPorDuplicado}");
        Console.WriteLine($"Filas descartadas:                 {motivosDescarte.Count}");

        if (motivosDescarte.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Motivos de descarte:");
            foreach (var motivo in motivosDescarte.Take(20))
                Console.WriteLine($"  - {motivo}");
        }
    }

    // El Nit guardado puede venir limpio ("900551058") o sucio, heredado del formato anterior
    // ("NIT 900551058 5" = prefijo + NIT base + dígito de verificación). Se extraen solo los dígitos
    // y, si el resultado tiene 10 (base de 9 + verificación), se descarta el último para comparar
    // contra el NIT crudo (sin verificación) que trae este Excel.
    private static string? NormalizarNitBase(string? nitGuardado)
    {
        if (string.IsNullOrWhiteSpace(nitGuardado))
            return null;

        var soloDigitos = new string(nitGuardado.Where(char.IsDigit).ToArray());
        if (soloDigitos.Length == 0)
            return null;

        return soloDigitos.Length == 10 ? soloDigitos[..9] : soloDigitos;
    }

    private static string Truncar(string texto, int maxLongitud)
        => texto.Length <= maxLongitud ? texto : texto[..maxLongitud];
}
