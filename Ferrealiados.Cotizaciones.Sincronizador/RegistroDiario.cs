using System.Text;

namespace Ferrealiados.Cotizaciones.Sincronizador;

/// <summary>
/// Log del Sincronizador: un archivo por día en la carpeta <c>logs</c> junto al .exe
/// (sincronizador-2026-11-09.log) y se borran solos los de más de N días. Antes la Tarea Programada
/// redirigía la salida a un solo sincronizador.log que crecía sin límite (una corrida cada 10 min).
///
/// Todo lo que el programa escribe con Console.WriteLine va al archivo del día. Si la salida además
/// está redirigida (la tarea vieja con "&gt;&gt; sincronizador.log"), no se duplica ahí: así ese archivo
/// deja de crecer aunque nadie cambie la tarea. Corriendo a mano en PowerShell se sigue viendo en
/// pantalla.
/// </summary>
public static class RegistroDiario
{
    public static void Iniciar(string carpetaBase, int diasRetencion)
    {
        try
        {
            var carpeta = Path.Combine(carpetaBase, "logs");
            Directory.CreateDirectory(carpeta);
            BorrarAntiguos(carpeta, diasRetencion);

            var ruta = Path.Combine(carpeta, $"sincronizador-{DateTime.Now:yyyy-MM-dd}.log");
            var archivo = new StreamWriter(new FileStream(ruta, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
            {
                AutoFlush = true,
            };

            Console.SetOut(Console.IsOutputRedirected
                ? archivo
                : new EscritorDoble(Console.Out, archivo));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sin log en archivo el programa igual tiene que sincronizar.
            Console.WriteLine($"[{DateTime.Now:s}] No se pudo abrir el log diario: {ex.Message}");
        }
    }

    private static void BorrarAntiguos(string carpeta, int diasRetencion)
    {
        var limite = DateTime.Now.Date.AddDays(-Math.Max(diasRetencion, 1));
        foreach (var archivo in Directory.EnumerateFiles(carpeta, "sincronizador-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(archivo) < limite)
                    File.Delete(archivo);
            }
            catch (IOException)
            {
                // Abierto por otra corrida o por alguien leyéndolo: se intenta la próxima vez.
            }
        }
    }

    private sealed class EscritorDoble(TextWriter pantalla, TextWriter archivo) : TextWriter
    {
        public override Encoding Encoding => pantalla.Encoding;

        public override void Write(char value)
        {
            pantalla.Write(value);
            archivo.Write(value);
        }

        public override void Write(string? value)
        {
            pantalla.Write(value);
            archivo.Write(value);
        }

        public override void WriteLine(string? value)
        {
            pantalla.WriteLine(value);
            archivo.WriteLine(value);
        }

        public override void Flush()
        {
            pantalla.Flush();
            archivo.Flush();
        }
    }
}
