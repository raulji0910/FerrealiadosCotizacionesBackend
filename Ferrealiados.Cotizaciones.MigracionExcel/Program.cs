using Ferrealiados.Cotizaciones.MigracionExcel;
using Ferrealiados.Cotizaciones.Modelo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

if (args.Length == 0)
{
    Console.WriteLine("Uso: Ferrealiados.Cotizaciones.MigracionExcel <ruta-al-excel>");
    Console.WriteLine("Soporta tres formatos, detectados automáticamente:");
    Console.WriteLine("  - Histórico: una sola hoja (ej. COTIZACIONES.xlsx)");
    Console.WriteLine("  - Proveedores/Cotizaciones: hojas \"Proveedores\" + \"Cotizaciones\" (ej. COSTO DE PRODUCTOS_PROVEEDOR.xlsx)");
    Console.WriteLine("  - Historial de precios: hoja \"historial de precios\" (ej. HistoriaPreciosNuevo.xlsx)");
    return 1;
}

var rutaExcel = args[0];
if (!File.Exists(rutaExcel))
{
    Console.WriteLine($"No se encontró el archivo: {rutaExcel}");
    return 1;
}

var configuracion = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var cadenaConexion = configuracion.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json.");

var opciones = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(cadenaConexion, sql => sql.EnableRetryOnFailure(maxRetryCount: 5))
    .Options;

await using var db = new AppDbContext(opciones);
await db.Database.MigrateAsync();

if (LectorExcelHistorialPrecios.EsFormatoHistorialPrecios(rutaExcel))
{
    Console.WriteLine("Formato detectado: Historial de precios");
    Console.WriteLine();
    await MigradorHistorialPrecios.EjecutarAsync(rutaExcel, db);
}
else if (LectorExcelProveedorCotizacion.EsFormatoProveedorCotizacion(rutaExcel))
{
    Console.WriteLine("Formato detectado: Proveedores/Cotizaciones");
    Console.WriteLine();
    await MigradorProveedorCotizacion.EjecutarAsync(rutaExcel, db);
}
else
{
    Console.WriteLine("Formato detectado: histórico (hoja única)");
    Console.WriteLine();
    await MigradorHistorico.EjecutarAsync(rutaExcel, db);
}

return 0;
