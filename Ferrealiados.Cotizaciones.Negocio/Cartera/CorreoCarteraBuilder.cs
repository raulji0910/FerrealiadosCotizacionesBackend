using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Cartera;

/// <summary>
/// Arma el correo de estado de cuenta (asunto + HTML) de un cliente. Lógica pura, sin BD ni red,
/// para poder probarla y para que la vista previa sea exactamente lo que se envía. El HTML usa
/// estilos en línea y tablas porque es lo único que respetan Outlook y Gmail.
/// </summary>
public static class CorreoCarteraBuilder
{
    private const string ColorMarca = "#043760";
    private const string ColorVencido = "#c62828";

    public static string Asunto(string plantilla, string cliente, string nit, DateOnly fechaCorte, decimal total, decimal vencido)
        => Reemplazar(plantilla, cliente, nit, fechaCorte, total, vencido).ReplaceLineEndings(" ").Trim();

    public static string Html(
        string plantillaCuerpo,
        string? datosPago,
        string firma,
        string cliente,
        string nit,
        DateOnly fechaCorte,
        IReadOnlyList<DocumentoCarteraDto> documentos,
        EdadesCarteraDto edades)
    {
        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#222;max-width:760px\">");

        html.Append("<p style=\"margin:0 0 16px\">")
            .Append(Parrafos(Reemplazar(plantillaCuerpo, cliente, nit, fechaCorte, edades.Total, edades.Vencido)))
            .Append("</p>");

        // Documentos pendientes
        html.Append("<table cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;width:100%;font-size:13px\">");
        html.Append("<tr style=\"background:").Append(ColorMarca).Append(";color:#fff\">");
        foreach (var (titulo, derecha) in new[] { ("Documento", false), ("Fecha", false), ("Vencimiento", false), ("Días vencido", true), ("Saldo", true) })
            html.Append("<th style=\"text-align:").Append(derecha ? "right" : "left").Append(";border:1px solid ").Append(ColorMarca).Append('"').Append('>').Append(titulo).Append("</th>");
        html.Append("</tr>");

        foreach (var d in documentos)
        {
            var vencido = d.DiasVencido > 0 && d.Saldo > 0;
            var color = vencido ? ColorVencido : "#222";
            html.Append("<tr>")
                .Append(Celda($"{Enc(d.Prefijo)} {d.Numero}", false, color))
                .Append(Celda(Fecha(d.Fecha), false, color))
                .Append(Celda(Fecha(d.Vencimiento), false, color))
                .Append(Celda(d.DiasVencido > 0 ? d.DiasVencido.ToString(CultureInfo.InvariantCulture) : "Al día", true, color))
                .Append(Celda(Moneda(d.Saldo), true, color))
                .Append("</tr>");
        }

        html.Append("<tr style=\"font-weight:bold;background:#f2f2f2\">")
            .Append("<td colspan=\"4\" style=\"text-align:right;border:1px solid #ddd\">Total a pagar</td>")
            .Append(Celda(Moneda(edades.Total), true, "#222"))
            .Append("</tr></table>");

        // Resumen por edades
        html.Append("<table cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;margin-top:16px;font-size:13px\">");
        html.Append("<tr style=\"background:#f2f2f2\">");
        foreach (var titulo in new[] { "Por vencer", "1 a 30 días", "31 a 60 días", "61 a 90 días", "Más de 90 días" })
            html.Append("<th style=\"text-align:right;border:1px solid #ddd\">").Append(titulo).Append("</th>");
        html.Append("</tr><tr>");
        foreach (var valor in new[] { edades.PorVencer, edades.De1A30, edades.De31A60, edades.De61A90, edades.MasDe90 })
            html.Append(Celda(Moneda(valor), true, "#222"));
        html.Append("</tr></table>");

        if (!string.IsNullOrWhiteSpace(datosPago))
            html.Append("<p style=\"margin:16px 0 0\">").Append(Parrafos(datosPago)).Append("</p>");

        html.Append("<p style=\"margin:16px 0 0\">Cordialmente,<br><strong>").Append(Enc(firma)).Append("</strong></p>");
        html.Append("<p style=\"margin:16px 0 0;font-size:11px;color:#777\">Estado de cuenta con corte al ")
            .Append(Fecha(fechaCorte)).Append(". Si encuentra alguna diferencia, por favor responda este correo.</p>");
        html.Append("</div>");
        return html.ToString();
    }

    // "a@x.com; b@y.com, c@z.com" → lista limpia y sin repetidos. Lo que no es un correo válido va a
    // invalidos (para avisarlo al guardar, en vez de que falle al enviar).
    public static List<string> SepararCorreos(string? texto, List<string>? invalidos = null)
    {
        var correos = new List<string>();
        if (string.IsNullOrWhiteSpace(texto)) return correos;

        foreach (var parte in texto.Split([';', ',', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (EsCorreoValido(parte))
            {
                if (!correos.Contains(parte, StringComparer.OrdinalIgnoreCase))
                    correos.Add(parte);
            }
            else
            {
                invalidos?.Add(parte);
            }
        }
        return correos;
    }

    public static bool EsCorreoValido(string correo)
    {
        if (!correo.Contains('@') || correo.StartsWith('@') || correo.EndsWith('@')) return false;
        try
        {
            var direccion = new MailAddress(correo);
            return direccion.Address.Equals(correo, StringComparison.OrdinalIgnoreCase) && direccion.Host.Contains('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string Moneda(decimal valor)
    {
        var texto = Math.Abs(Math.Round(valor, 0)).ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.');
        return valor < 0 ? $"-${texto}" : $"${texto}";
    }

    public static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Reemplazar(string plantilla, string cliente, string nit, DateOnly fechaCorte, decimal total, decimal vencido)
        => plantilla
            .Replace("{cliente}", cliente, StringComparison.OrdinalIgnoreCase)
            .Replace("{nit}", nit, StringComparison.OrdinalIgnoreCase)
            .Replace("{fechaCorte}", Fecha(fechaCorte), StringComparison.OrdinalIgnoreCase)
            .Replace("{total}", Moneda(total), StringComparison.OrdinalIgnoreCase)
            .Replace("{vencido}", Moneda(vencido), StringComparison.OrdinalIgnoreCase);

    // Texto libre → HTML seguro, respetando los saltos de línea.
    private static string Parrafos(string texto) => Enc(texto.ReplaceLineEndings("\n")).Replace("\n", "<br>");

    private static string Enc(string texto) => WebUtility.HtmlEncode(texto);

    private static string Celda(string texto, bool derecha, string color)
        => $"<td style=\"text-align:{(derecha ? "right" : "left")};border:1px solid #ddd;color:{color};white-space:nowrap\">{Enc(texto)}</td>";
}
