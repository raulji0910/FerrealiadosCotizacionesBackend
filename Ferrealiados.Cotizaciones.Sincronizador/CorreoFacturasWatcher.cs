using Ferrealiados.Cotizaciones.Negocio.Conciliacion;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace Ferrealiados.Cotizaciones.Sincronizador;

/// <summary>Un correo de la bandeja con los XML que traía adjuntos (uno por ZIP normalmente).</summary>
public record CorreoConFacturas(UniqueId Uid, IReadOnlyList<FacturaXmlEntradaDto> Xmls);

/// <summary>
/// Lee la bandeja de facturacion@ferrealiadosjv.com por IMAP y saca los XML de los ZIP adjuntos.
/// A diferencia del Sincronizador de Jimaco, NO marca nada como leído: esa bandeja la usa
/// Contabilidad a mano todos los días (y el estado leído/no leído es suyo). En vez de eso se lleva
/// la marca del último UID procesado (<see cref="MarcaCorreoStore"/>) y la API descarta duplicados
/// por CUFE. La carpeta se abre en solo lectura y MailKit descarga con BODY.PEEK, así que leer no
/// cambia nada en la bandeja.
/// </summary>
public class CorreoFacturasWatcher(string host, int puerto, string usuario, string password, string? carpeta = null)
{
    public async Task<(uint UidValidity, IReadOnlyList<CorreoConFacturas> Correos)> ObtenerNuevosAsync(
        uint? uidValidityAnterior, uint ultimoUid, DateOnly desdeSinMarca, int maximo, CancellationToken ct = default)
    {
        using var cliente = new ImapClient();
        await cliente.ConnectAsync(host, puerto, SecureSocketOptions.SslOnConnect, ct);
        await cliente.AuthenticateAsync(usuario, password, ct);

        var bandeja = string.IsNullOrWhiteSpace(carpeta) ? cliente.Inbox : await cliente.GetFolderAsync(carpeta, ct);
        await bandeja.OpenAsync(FolderAccess.ReadOnly, ct);

        // Si el servidor renumeró la carpeta (UIDVALIDITY cambió) o es la primera corrida, la marca
        // no sirve: se arranca por fecha. Repetir correos no es problema (la API deduplica por CUFE).
        IList<UniqueId> uids = uidValidityAnterior == bandeja.UidValidity && ultimoUid > 0
            ? await bandeja.SearchAsync(SearchQuery.Uids(new UniqueIdRange(new UniqueId(ultimoUid + 1), UniqueId.MaxValue)), ct)
            : await bandeja.SearchAsync(SearchQuery.DeliveredAfter(desdeSinMarca.ToDateTime(TimeOnly.MinValue).AddDays(-1)), ct);

        var resultado = new List<CorreoConFacturas>();
        foreach (var uid in uids.Where(u => u.Id > ultimoUid || uidValidityAnterior != bandeja.UidValidity).OrderBy(u => u.Id).Take(maximo))
        {
            var mensaje = await bandeja.GetMessageAsync(uid, ct);
            resultado.Add(new CorreoConFacturas(uid, ExtraerXmls(mensaje)));
        }

        await cliente.DisconnectAsync(true, ct);
        return (bandeja.UidValidity, resultado);
    }

    // Los proveedores mandan un ZIP con XML + PDF (anexo técnico DIAN); algunos adjuntan el XML
    // suelto. Correos sin nada de eso (publicidad, tokens DIAN, certificados) devuelven lista vacía.
    internal static List<FacturaXmlEntradaDto> ExtraerXmls(MimeMessage mensaje)
    {
        var xmls = new List<FacturaXmlEntradaDto>();
        var fecha = mensaje.Date == DateTimeOffset.MinValue ? (DateTime?)null : mensaje.Date.UtcDateTime;

        foreach (var adjunto in mensaje.Attachments.OfType<MimePart>().Where(a => a.Content is not null))
        {
            using var contenido = new MemoryStream();
            adjunto.Content!.DecodeTo(contenido);
            contenido.Position = 0;
            ExtraerDeArchivo(adjunto.FileName ?? "", contenido, mensaje.Subject, fecha, xmls);
        }

        return xmls;
    }

    // Un XML suelto o un ZIP (que puede traer otros ZIP adentro, como el "Descargar todo" de
    // Outlook, que empaca los ZIP de los proveedores en uno solo). La lectura es la misma que usa la
    // API para los ZIP de la DIAN que se suben desde la pantalla.
    internal static void ExtraerDeArchivo(string nombre, Stream contenido, string? asunto, DateTime? fecha, List<FacturaXmlEntradaDto> xmls)
    {
        var avisos = new List<string>();
        ExtractorXmlFacturas.Extraer(nombre, contenido, asunto, fecha, xmls, avisos);
        foreach (var aviso in avisos)
            Console.WriteLine($"[{DateTime.Now:s}]   {aviso} (\"{asunto}\")");
    }
}
