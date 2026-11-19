using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Ferrealiados.Cotizaciones.Negocio.Cartera;

// Servidor de correo de salida (host y puerto, del .env del servidor: Smtp__Host / Smtp__Puerto).
// La clave puede venir de dos lados: Smtp__Password del .env (tiene prioridad) o la que un Admin
// guarda desde Configuración → Envío de cartera (cifrada en la BD con ICifradorSecretos).
// Nunca va en appsettings.json ni viaja de vuelta al navegador.
public class SmtpOptions
{
    public const string SeccionConfiguracion = "Smtp";

    public string? Host { get; set; }
    public int Puerto { get; set; } = 465;
    public string? Usuario { get; set; }
    public string? Password { get; set; }
}

public record CredencialesSmtp(string Host, int Puerto, string Usuario, string Password);

public record CorreoSaliente(
    string Remitente,
    string NombreRemitente,
    IReadOnlyList<string> Para,
    IReadOnlyList<string> Copias,
    string Asunto,
    string Html);

public interface IEnviadorCorreo
{
    Task EnviarAsync(CorreoSaliente correo, CredencialesSmtp credenciales, CancellationToken ct = default);

    // Solo se conecta y se autentica (no envía nada): para validar la clave antes de guardarla.
    Task ProbarAsync(CredencialesSmtp credenciales, CancellationToken ct = default);
}

public class SmtpEnviadorCorreo : IEnviadorCorreo
{
    public async Task EnviarAsync(CorreoSaliente correo, CredencialesSmtp credenciales, CancellationToken ct = default)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(correo.NombreRemitente, correo.Remitente));
        mensaje.ReplyTo.Add(new MailboxAddress(correo.NombreRemitente, correo.Remitente));
        foreach (var para in correo.Para) mensaje.To.Add(MailboxAddress.Parse(para));
        foreach (var copia in correo.Copias) mensaje.Cc.Add(MailboxAddress.Parse(copia));
        mensaje.Subject = correo.Asunto;
        mensaje.Body = new BodyBuilder { HtmlBody = correo.Html }.ToMessageBody();

        using var cliente = await ConectarAsync(credenciales, ct);
        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(true, ct);
    }

    public async Task ProbarAsync(CredencialesSmtp credenciales, CancellationToken ct = default)
    {
        using var cliente = await ConectarAsync(credenciales, ct);
        await cliente.DisconnectAsync(true, ct);
    }

    private static async Task<SmtpClient> ConectarAsync(CredencialesSmtp credenciales, CancellationToken ct)
    {
        var cliente = new SmtpClient { Timeout = 30_000 };
        try
        {
            // 465 = SSL directo; otro puerto (587) = STARTTLS si el servidor lo ofrece.
            var seguridad = credenciales.Puerto == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await cliente.ConnectAsync(credenciales.Host, credenciales.Puerto, seguridad, ct);
            // Mailpit (pruebas locales) no pide autenticación.
            if (cliente.Capabilities.HasFlag(SmtpCapabilities.Authentication))
                await cliente.AuthenticateAsync(credenciales.Usuario, credenciales.Password, ct);
            return cliente;
        }
        catch
        {
            cliente.Dispose();
            throw;
        }
    }
}
