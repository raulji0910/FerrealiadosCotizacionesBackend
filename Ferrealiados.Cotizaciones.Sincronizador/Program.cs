using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Sincronizador;
using Microsoft.Extensions.Configuration;

// Pensado para correr como una Tarea Programada de Windows en la red local de World Office de
// Ferrealiados (cada N minutos) — una corrida = un pase completo y salir, igual que el
// Sincronizador de Jimaco Aprobaciones. La API pública (Lightsail) no puede llegar a WO; por eso
// este programa lee WO y el correo de facturación desde adentro de la red y los sube.

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Local.json", optional: true) // credenciales reales, nunca commiteado
    .AddEnvironmentVariables()
    .Build();

string Requerido(string clave) => config[clave] ?? throw new InvalidOperationException($"Falta configurar {clave}.");

using var http = new HttpClient { BaseAddress = new Uri(Requerido("Ferrealiados:ApiBaseUrl")), Timeout = TimeSpan.FromMinutes(5) };
var api = new FerrealiadosCotizacionesClient(http, Requerido("Ferrealiados:UsuarioServicio:Email"), Requerido("Ferrealiados:UsuarioServicio:Password"));

// ---- Carga única desde carpeta: Sincronizador.exe --carpeta "C:\ruta" ----
// Solo sube las facturas de esa carpeta y sale; no toca las OC ni la marca del correo.
var indiceCarpeta = Array.IndexOf(args, "--carpeta");
if (indiceCarpeta >= 0)
{
    if (indiceCarpeta + 1 >= args.Length || !Directory.Exists(args[indiceCarpeta + 1]))
    {
        Console.WriteLine("Uso: Ferrealiados.Cotizaciones.Sincronizador.exe --carpeta \"C:\\ruta\\de\\la\\carpeta\"");
        return 1;
    }

    try
    {
        var carpeta = args[indiceCarpeta + 1];
        Console.WriteLine($"[{DateTime.Now:s}] Leyendo facturas de la carpeta {carpeta}...");
        var xmls = CarpetaFacturasReader.Leer(carpeta);
        Console.WriteLine($"[{DateTime.Now:s}] {xmls.Count} XML encontrados.");

        int nuevas = 0, duplicadas = 0, noReconocidas = 0, fallidas = 0;
        foreach (var lote in LotesPorTamano(xmls, 900_000))
        {
            try
            {
                var r = await api.RegistrarFacturasAsync(lote);
                nuevas += r.Nuevas;
                duplicadas += r.Duplicadas;
                noReconocidas += r.NoReconocidas;
            }
            catch (HttpRequestException ex) when (lote.Count == 1)
            {
                // Un XML solo que el servidor no recibe (p. ej. más grande que el límite de nginx).
                fallidas++;
                Console.WriteLine($"[{DateTime.Now:s}]   {lote[0].NombreArchivo} no se pudo subir: {ex.Message.Split('\n')[0]}");
            }
        }
        Console.WriteLine($"[{DateTime.Now:s}] Facturas: {nuevas} nuevas, {duplicadas} ya existían, {noReconocidas} XML no reconocidos" +
                          (fallidas > 0 ? $", {fallidas} no se pudieron subir." : "."));
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:s}] ERROR cargando la carpeta: {ex.Message}");
        return 1;
    }
}

var huboError = false;

// ---- Fase 1: OC de World Office ----
// Se manda la ventana completa en cada corrida: WO no tiene fecha de modificación confiable y una
// OC se puede editar o anular después de creada. La API reemplaza lo que ya tenía.
try
{
    var prefijo = config["WorldOffice:PrefijoOrdenCompra"] ?? "OC";
    var diasAtras = int.TryParse(config["WorldOffice:DiasOrdenesCompra"], out var d) ? d : 150;
    var desde = DateOnly.FromDateTime(DateTime.Today).AddDays(-diasAtras);

    var lector = new WorldOfficeReader(Requerido("WorldOffice:ConnectionString"));
    Console.WriteLine($"[{DateTime.Now:s}] Leyendo OC (prefijo {prefijo}) de World Office desde {desde:yyyy-MM-dd}...");
    var ordenes = await lector.ObtenerOrdenesCompraAsync(prefijo, desde);
    Console.WriteLine($"[{DateTime.Now:s}] {ordenes.Count} OC leídas ({ordenes.Count(o => o.Anulada)} anuladas).");

    foreach (var lote in ordenes.Chunk(200))
    {
        var r = await api.SincronizarOrdenesAsync(lote);
        Console.WriteLine($"[{DateTime.Now:s}]   Lote de {lote.Length}: {r.Nuevas} nuevas, {r.Actualizadas} actualizadas.");
    }
}
catch (Exception ex)
{
    huboError = true;
    Console.WriteLine($"[{DateTime.Now:s}] ERROR sincronizando OC: {ex.Message}");
}

// ---- Fase 1b: FC de World Office (la OC que escribe Contabilidad en Personalizado1) ----
// Último recurso del cruce. Igual que las OC: ventana completa en cada corrida, la API reemplaza.
try
{
    var prefijo = config["WorldOffice:PrefijoFacturaCompra"] ?? "FC";
    var diasAtras = int.TryParse(config["WorldOffice:DiasOrdenesCompra"], out var d) ? d : 150;
    var desde = DateOnly.FromDateTime(DateTime.Today).AddDays(-diasAtras);

    var lector = new WorldOfficeReader(Requerido("WorldOffice:ConnectionString"));
    var facturasCompra = await lector.ObtenerFacturasCompraAsync(prefijo, desde);
    var r = await api.SincronizarFacturasCompraAsync(new SincronizacionFacturasCompraEntradaDto(desde, facturasCompra));
    Console.WriteLine($"[{DateTime.Now:s}] {facturasCompra.Count} FC leídas ({facturasCompra.Count(f => !string.IsNullOrWhiteSpace(f.ReferenciaOrdenes))} con OC): " +
                      $"{r.Nuevas} nuevas, {r.Actualizadas} actualizadas, {r.Eliminadas} eliminadas.");
}
catch (Exception ex)
{
    huboError = true;
    Console.WriteLine($"[{DateTime.Now:s}] ERROR sincronizando FC: {ex.Message}");
}

// ---- Fase 2: facturas electrónicas del correo de facturación ----
// Se avanza la marca correo por correo, solo después de que la API confirmó: si algo falla a
// mitad, la próxima corrida retoma desde el último correo confirmado (y la API deduplica por CUFE).
try
{
    var correo = new CorreoFacturasWatcher(
        Requerido("Correo:Host"),
        int.TryParse(config["Correo:Puerto"], out var p) ? p : 993,
        Requerido("Correo:Usuario"),
        Requerido("Correo:Password"),
        config["Correo:Carpeta"]);
    var marcas = new MarcaCorreoStore(Path.Combine(AppContext.BaseDirectory, config["Sincronizador:RutaMarcaCorreo"] ?? "marca-correo.json"));
    var desdeSinMarca = DateOnly.TryParse(config["Correo:DesdeFechaInicial"], out var df) ? df : DateOnly.FromDateTime(DateTime.Today).AddDays(-30);
    var maximo = int.TryParse(config["Correo:MaximoCorreosPorCorrida"], out var m) ? m : 300;

    var marca = marcas.Leer();
    Console.WriteLine($"[{DateTime.Now:s}] Revisando bandeja de {config["Correo:Usuario"]} " +
                      (marca is null ? $"desde {desdeSinMarca:yyyy-MM-dd} (primera corrida)..." : $"desde el UID {marca.UltimoUid}..."));

    var (uidValidity, correos) = await correo.ObtenerNuevosAsync(marca?.UidValidity, marca?.UltimoUid ?? 0, desdeSinMarca, maximo);
    Console.WriteLine($"[{DateTime.Now:s}] {correos.Count} correo(s) nuevo(s), {correos.Sum(c => c.Xmls.Count)} XML adjunto(s).");

    int nuevas = 0, duplicadas = 0, noReconocidas = 0;
    foreach (var c in correos)
    {
        if (c.Xmls.Count > 0)
        {
            var r = await api.RegistrarFacturasAsync(c.Xmls);
            nuevas += r.Nuevas;
            duplicadas += r.Duplicadas;
            noReconocidas += r.NoReconocidas;
        }
        marcas.Guardar(new MarcaCorreoStore.Marca(uidValidity, c.Uid.Id));
    }
    Console.WriteLine($"[{DateTime.Now:s}] Facturas: {nuevas} nuevas, {duplicadas} ya existían, {noReconocidas} XML no reconocidos.");
}
catch (Exception ex)
{
    huboError = true;
    Console.WriteLine($"[{DateTime.Now:s}] ERROR leyendo el correo de facturación: {ex.Message}");
}

return huboError ? 1 : 0;

// nginx de producción rechaza cuerpos de más de 1 MB. En el JSON cada <, > y comilla del XML sale
// escapado como \u003C (6 caracteres), así que se cuenta el triple del XML para no pasarse.
static IEnumerable<List<FacturaXmlEntradaDto>> LotesPorTamano(IReadOnlyList<FacturaXmlEntradaDto> xmls, int maximoBytes)
{
    var lote = new List<FacturaXmlEntradaDto>();
    var tamano = 0;
    foreach (var x in xmls)
    {
        var estimado = x.Xml.Length * 3;
        if (lote.Count > 0 && tamano + estimado > maximoBytes)
        {
            yield return lote;
            lote = [];
            tamano = 0;
        }
        lote.Add(x);
        tamano += estimado;
    }
    if (lote.Count > 0) yield return lote;
}
