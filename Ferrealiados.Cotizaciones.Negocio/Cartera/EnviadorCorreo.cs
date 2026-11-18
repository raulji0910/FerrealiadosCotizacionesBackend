using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Ferrealiados.Cotizaciones.Negocio.Cartera;

// Servidor de correo de salida. La clave NUNCA va en appsettings.json ni en la BD: en producción
// llega por variable de entorno (Smtp__Password, desde el .env del servidor).
public class SmtpOptions
{
    public const string SeccionConfiguracion = "Smtp";

    public string? Host { get; set; }
    public int Puerto { get; set; } = 465;
    public string? Usuario { get; set; }
    public string? Password { get; set; }
}

public record CorreoSaliente(
    string Remitente,
    string NombreRemitente,
    IReadOnlyList<string> Para,
    IReadOnlyList<string> Copias,
    string Asunto,
    string Html);

public interface IEnviadorCorreo
{
    bool EstaConfigurado { get; }
    Task EnviarAsync(CorreoSaliente correo, CancellationToken ct = default);
}

public class SmtpEnviadorCorreo(IOptions<SmtpOptions> opciones) : IEnviadorCorreo
{
    private readonly SmtpOptions _opciones = opciones.Value;

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(_opciones.Host) &&
        !string.IsNullOrWhiteSpace(_opciones.Usuario) &&
        !string.IsNullOrWhiteSpace(_opciones.Password);

    public async Task EnviarAsync(CorreoSaliente correo, CancellationToken ct = default)
    {
        if (!EstaConfigurado)
            throw new InvalidOperationException("El servidor de correo (SMTP) no está configurado en el servidor.");

        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(correo.NombreRemitente, correo.Remitente));
        mensaje.ReplyTo.Add(new MailboxAddress(correo.NombreRemitente, correo.Remitente));
        foreach (var para in correo.Para) mensaje.To.Add(MailboxAddress.Parse(para));
        foreach (var copia in correo.Copias) mensaje.Cc.Add(MailboxAddress.Parse(copia));
        mensaje.Subject = correo.Asunto;
        mensaje.Body = new BodyBuilder { HtmlBody = correo.Html }.ToMessageBody();

        using var cliente = new SmtpClient { Timeout = 30_000 };
        // 465 = SSL directo; otro puerto (587) = STARTTLS si el servidor lo ofrece.
        var seguridad = _opciones.Puerto == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await cliente.ConnectAsync(_opciones.Host!, _opciones.Puerto, seguridad, ct);
        await cliente.AuthenticateAsync(_opciones.Usuario!, _opciones.Password!, ct);
        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(true, ct);
    }
}
