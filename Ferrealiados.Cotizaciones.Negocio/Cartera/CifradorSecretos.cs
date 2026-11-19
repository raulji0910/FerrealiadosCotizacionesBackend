using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Ferrealiados.Cotizaciones.Negocio.Cartera;

// Llave para cifrar secretos que se guardan en la BD (hoy: la clave del correo de cartera).
// Vive SOLO en el servidor (Cifrado__Clave, desde CIFRADO_CLAVE del .env: 32 bytes en base64,
// generada con `openssl rand -base64 32`). Un backup de la BD sin esta llave no deja leer la clave.
public class CifradoOptions
{
    public const string SeccionConfiguracion = "Cifrado";

    public string? Clave { get; set; }
}

public interface ICifradorSecretos
{
    bool EstaDisponible { get; }
    string Cifrar(string texto);

    // Null si el dato no se puede descifrar (llave cambiada o dato dañado).
    string? Descifrar(string cifrado);
}

// AES-256-GCM. Formato guardado: base64(nonce 12 bytes | tag 16 bytes | datos cifrados).
public class CifradorSecretosAes(IOptions<CifradoOptions> opciones) : ICifradorSecretos
{
    private const int TamanoNonce = 12;
    private const int TamanoTag = 16;

    private readonly byte[]? _llave = LeerLlave(opciones.Value.Clave);

    public bool EstaDisponible => _llave is not null;

    public string Cifrar(string texto)
    {
        var llave = _llave ?? throw new InvalidOperationException(
            "Falta la llave de cifrado en el servidor (CIFRADO_CLAVE en el .env).");

        var datos = Encoding.UTF8.GetBytes(texto);
        var resultado = new byte[TamanoNonce + TamanoTag + datos.Length];
        var nonce = resultado.AsSpan(0, TamanoNonce);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(llave, TamanoTag);
        aes.Encrypt(nonce, datos, resultado.AsSpan(TamanoNonce + TamanoTag), resultado.AsSpan(TamanoNonce, TamanoTag));
        return Convert.ToBase64String(resultado);
    }

    public string? Descifrar(string cifrado)
    {
        if (_llave is null) return null;
        try
        {
            var bytes = Convert.FromBase64String(cifrado);
            if (bytes.Length < TamanoNonce + TamanoTag) return null;

            var datos = new byte[bytes.Length - TamanoNonce - TamanoTag];
            using var aes = new AesGcm(_llave, TamanoTag);
            aes.Decrypt(bytes.AsSpan(0, TamanoNonce), bytes.AsSpan(TamanoNonce + TamanoTag), bytes.AsSpan(TamanoNonce, TamanoTag), datos);
            return Encoding.UTF8.GetString(datos);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }

    private static byte[]? LeerLlave(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave)) return null;
        try
        {
            var bytes = Convert.FromBase64String(clave.Trim());
            return bytes.Length == 32 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
