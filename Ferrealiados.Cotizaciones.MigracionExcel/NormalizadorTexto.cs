using System.Text;
using System.Text.RegularExpressions;

namespace Ferrealiados.Cotizaciones.MigracionExcel;

public static partial class NormalizadorTexto
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        // El Excel origen tiene tildes rotas (guardadas como el caracter de reemplazo Unicode).
        // La mayoría de los casos detectados corresponden a una "Ñ" faltante
        // (PEQUE?O, TAMA?O). No es 100% preciso pero es la mejor aproximación posible sin el dato original.
        var limpio = texto.Replace('�', 'Ñ').Trim();
        limpio = RepararDobleUtf8(limpio);
        limpio = EspaciosMultiples().Replace(limpio, " ");
        return limpio;
    }

    public static string ClaveNormalizada(string? texto)
        => Normalizar(texto).ToUpperInvariant();

    // Parte del Excel quedó guardado con doble codificación UTF-8 (ej. "uña" -> "uÃ±a"):
    // los bytes UTF-8 originales se reinterpretaron como Latin-1/Windows-1252 en algún momento
    // de la historia del archivo. Es reversible: se re-codifica como Latin-1 y se vuelve a
    // decodificar como UTF-8; si el resultado es válido y distinto, se usa el reparado.
    private static string RepararDobleUtf8(string texto)
    {
        if (!TieneCaracteresSospechosos(texto))
            return texto;

        try
        {
            var bytes = new byte[texto.Length];
            for (var i = 0; i < texto.Length; i++)
            {
                if (texto[i] > 0xFF)
                    return texto; // no es Latin-1 puro, no se puede reparar así

                bytes[i] = (byte)texto[i];
            }

            var utf8Estricto = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var reparado = utf8Estricto.GetString(bytes);
            return reparado;
        }
        catch (DecoderFallbackException)
        {
            return texto;
        }
    }

    // "Ã" (U+00C3) y "Â" (U+00C2) son el indicio típico de UTF-8 mal reinterpretado como Latin-1.
    private static bool TieneCaracteresSospechosos(string texto)
        => texto.Contains('Ã') || texto.Contains('Â');

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosMultiples();
}
