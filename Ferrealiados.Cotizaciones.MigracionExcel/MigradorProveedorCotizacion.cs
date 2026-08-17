using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.MigracionExcel;

// Importa "COSTO DE PRODUCTOS_PROVEEDOR.xlsx": hoja "Proveedores" (ficha completa: NIT, dirección,
// teléfono, ciudad) + hoja "Cotizaciones" (fecha, proveedor, código+nombre de producto, costo).
// El cruce entre hojas es por nombre de proveedor normalizado. A diferencia del formato histórico,
// aquí NO se colapsa al precio más reciente por par: cada fila de Cotizaciones es una cotización
// real y distinta dentro de una ventana corta, así que se carga cada una como su propio registro
// de precio (idempotente: no duplica si ya existe la misma fila producto+proveedor+fecha).
public static class MigradorProveedorCotizacion
{
    public static async Task EjecutarAsync(string rutaExcel, AppDbContext db)
    {
        // --- 1. Leer ambas hojas ---
        var proveedoresValidos = new List<FilaProveedorMaestro>();
        var motivosDescarteProveedores = new List<string>();
        foreach (var resultado in LectorExcelProveedorCotizacion.LeerProveedores(rutaExcel))
        {
            if (resultado.Fila is not null)
                proveedoresValidos.Add(resultado.Fila);
            else
                motivosDescarteProveedores.Add(resultado.MotivoDescarte!);
        }

        var cotizacionesValidas = new List<FilaCotizacionProveedor>();
        var motivosDescarteCotizaciones = new List<string>();
        foreach (var resultado in LectorExcelProveedorCotizacion.LeerCotizaciones(rutaExcel))
        {
            if (resultado.Fila is not null)
                cotizacionesValidas.Add(resultado.Fila);
            else
                motivosDescarteCotizaciones.Add(resultado.MotivoDescarte!);
        }

        Console.WriteLine($"Hoja Proveedores — filas válidas: {proveedoresValidos.Count}, descartadas: {motivosDescarteProveedores.Count}");
        Console.WriteLine($"Hoja Cotizaciones — filas válidas: {cotizacionesValidas.Count}, descartadas: {motivosDescarteCotizaciones.Count}");

        // --- 2. Normalizar texto y excluir códigos genéricos en Cotizaciones ---
        var cotizacionesNormalizadas = new List<FilaCotizacionProveedor>();
        var excluidasGenericas = 0;
        foreach (var fila in cotizacionesValidas)
        {
            var nombreNormalizado = NormalizadorTexto.Normalizar(fila.Nombre);

            if (PalabrasGenericas.EsGenerico(nombreNormalizado))
            {
                excluidasGenericas++;
                continue;
            }

            cotizacionesNormalizadas.Add(fila with
            {
                Nombre = nombreNormalizado,
                Proveedor = NormalizadorTexto.Normalizar(fila.Proveedor),
                Codigo = fila.Codigo.Trim().ToUpperInvariant()
            });
        }

        Console.WriteLine($"Filas excluidas por código genérico: {excluidasGenericas}");

        // --- 3. Proveedores: diccionario por nombre normalizado, hoja Proveedores primero (gana la primera fila) ---
        var proveedoresPorClave = new Dictionary<string, FilaProveedorMaestro>();
        foreach (var fila in proveedoresValidos)
        {
            var normalizado = fila with
            {
                Nombre = NormalizadorTexto.Normalizar(fila.Nombre),
                // Algunos valores de "Identificación" son texto largo tipo "Documento de Identificación
                // extranjero..." en vez de un NIT/CC real — se truncan defensivamente al límite de la columna.
                Nit = fila.Nit is null ? null : Truncar(fila.Nit, 50),
                Direccion = fila.Direccion is null ? null : Truncar(NormalizadorTexto.Normalizar(fila.Direccion), 300),
                Ciudad = fila.Ciudad is null ? null : Truncar(NormalizadorTexto.Normalizar(fila.Ciudad), 100)
            };
            var clave = NormalizadorTexto.ClaveNormalizada(normalizado.Nombre);
            proveedoresPorClave.TryAdd(clave, normalizado);
        }

        // Proveedores que solo aparecen en Cotizaciones (sin ficha): se agregan con nombre únicamente.
        var proveedoresSoloEnCotizaciones = 0;
        foreach (var fila in cotizacionesNormalizadas)
        {
            var clave = NormalizadorTexto.ClaveNormalizada(fila.Proveedor);
            if (!proveedoresPorClave.ContainsKey(clave))
            {
                proveedoresPorClave[clave] = new FilaProveedorMaestro(fila.Proveedor, null, null, null, null);
                proveedoresSoloEnCotizaciones++;
            }
        }

        Console.WriteLine($"Proveedores sin ficha en la hoja Proveedores (creados solo con nombre): {proveedoresSoloEnCotizaciones}");

        // --- 4. Nombre canónico de producto por código: el de la fecha más reciente en este archivo ---
        var nombreCanonicoPorCodigo = cotizacionesNormalizadas
            .GroupBy(f => f.Codigo)
            .ToDictionary(
                g => g.Key,
                g => Truncar(g.OrderByDescending(f => f.Fecha).ThenByDescending(f => f.NumeroFila).First().Nombre, 500));

        // --- 5. Cargar proveedores (crear o enriquecer) ---
        var proveedoresExistentes = await db.Proveedores.ToDictionaryAsync(p => NormalizadorTexto.ClaveNormalizada(p.Nombre), p => p);
        var proveedoresCreados = 0;
        var proveedoresActualizados = 0;

        foreach (var (clave, datos) in proveedoresPorClave)
        {
            if (proveedoresExistentes.TryGetValue(clave, out var existente))
            {
                var seActualizo = false;
                if (datos.Nit is not null && existente.Nit != datos.Nit) { existente.Nit = datos.Nit; seActualizo = true; }
                if (datos.Direccion is not null && existente.Direccion != datos.Direccion) { existente.Direccion = datos.Direccion; seActualizo = true; }
                if (datos.Telefono is not null && existente.Telefono != datos.Telefono) { existente.Telefono = datos.Telefono; seActualizo = true; }
                if (datos.Ciudad is not null && existente.Ciudad != datos.Ciudad) { existente.Ciudad = datos.Ciudad; seActualizo = true; }
                if (seActualizo)
                    proveedoresActualizados++;
            }
            else
            {
                var nuevo = new Proveedor
                {
                    Nombre = Truncar(datos.Nombre, 200),
                    Nit = datos.Nit,
                    Direccion = datos.Direccion,
                    Telefono = datos.Telefono,
                    Ciudad = datos.Ciudad,
                    Activo = true
                };
                db.Proveedores.Add(nuevo);
                proveedoresExistentes[clave] = nuevo;
                proveedoresCreados++;
            }
        }

        await db.SaveChangesAsync();

        // --- 6. Cargar productos (crear o actualizar nombre) ---
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
                var nuevo = new Producto
                {
                    Codigo = codigo,
                    Nombre = nombre,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };
                db.Productos.Add(nuevo);
                productosExistentes[codigo] = nuevo;
                productosCreados++;
            }
        }

        await db.SaveChangesAsync();

        // --- 7. Cargar precios: una fila = un registro (no se colapsa), idempotente por triple exacto ---
        var preciosExistentes = (await db.ProductoProveedorPrecios
                .Select(p => new { p.ProductoId, p.ProveedorId, p.FechaCotizacion })
                .ToListAsync())
            .ToHashSet();

        var preciosCreados = 0;
        foreach (var fila in cotizacionesNormalizadas)
        {
            var producto = productosExistentes[fila.Codigo];
            var proveedor = proveedoresExistentes[NormalizadorTexto.ClaveNormalizada(fila.Proveedor)];
            var clave = new { ProductoId = producto.Id, ProveedorId = proveedor.Id, FechaCotizacion = fila.Fecha };

            if (preciosExistentes.Contains(clave))
                continue;

            db.ProductoProveedorPrecios.Add(new ProductoProveedorPrecio
            {
                ProductoId = producto.Id,
                ProveedorId = proveedor.Id,
                Costo = fila.Costo,
                FechaCotizacion = fila.Fecha,
                CreadoPor = "MigracionExcel:ProveedorCotizacion",
                FechaRegistro = DateTime.UtcNow
            });
            preciosExistentes.Add(clave);
            preciosCreados++;
        }

        await db.SaveChangesAsync();

        Console.WriteLine();
        Console.WriteLine("=== Resumen de migración (formato Proveedores/Cotizaciones) ===");
        Console.WriteLine($"Proveedores creados:     {proveedoresCreados}");
        Console.WriteLine($"Proveedores actualizados: {proveedoresActualizados}");
        Console.WriteLine($"Productos creados:       {productosCreados}");
        Console.WriteLine($"Productos actualizados:  {productosActualizados}");
        Console.WriteLine($"Precios cargados:        {preciosCreados}");
        Console.WriteLine($"Filas descartadas (Proveedores): {motivosDescarteProveedores.Count}");
        Console.WriteLine($"Filas descartadas (Cotizaciones): {motivosDescarteCotizaciones.Count}");
        Console.WriteLine($"Filas excluidas por código genérico: {excluidasGenericas}");

        var todosLosMotivos = motivosDescarteProveedores.Concat(motivosDescarteCotizaciones).ToList();
        if (todosLosMotivos.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Primeros motivos de descarte:");
            foreach (var motivo in todosLosMotivos.Take(20))
                Console.WriteLine($"  - {motivo}");
        }
    }

    private static string Truncar(string texto, int maxLongitud)
        => texto.Length <= maxLongitud ? texto : texto[..maxLongitud];
}
