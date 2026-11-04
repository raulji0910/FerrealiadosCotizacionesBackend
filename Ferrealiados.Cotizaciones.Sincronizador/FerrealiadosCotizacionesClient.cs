using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Sincronizador;

/// <summary>
/// Cliente HTTP contra la API de Ferrealiados Cotizaciones, autenticado como un usuario de servicio
/// normal (rol Admin, creado desde Usuarios en la app) — mismo patrón que el Sincronizador de
/// Jimaco Aprobaciones: entra por la puerta de siempre, sin endpoints con atajos especiales.
/// </summary>
public class FerrealiadosCotizacionesClient(HttpClient http, string email, string password)
{
    private static readonly JsonSerializerOptions JsonOpciones = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private string? _token;
    private DateTimeOffset _tokenExpira = DateTimeOffset.MinValue;

    public Task<RegistroFacturasResultadoDto> RegistrarFacturasAsync(IReadOnlyList<FacturaXmlEntradaDto> facturas, CancellationToken ct = default)
        => PostAsync<RegistroFacturasResultadoDto>("api/conciliacion/facturas", facturas, ct);

    public Task<SincronizacionOrdenesResultadoDto> SincronizarOrdenesAsync(IReadOnlyList<OrdenCompraEntradaDto> ordenes, CancellationToken ct = default)
        => PostAsync<SincronizacionOrdenesResultadoDto>("api/conciliacion/ordenes", ordenes, ct);

    private async Task<T> PostAsync<T>(string ruta, object cuerpo, CancellationToken ct)
    {
        await AsegurarTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = JsonContent.Create(cuerpo, options: JsonOpciones)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

        using var response = await http.SendAsync(request, ct);
        await LanzarSiErrorAsync(response, ct);

        return (await response.Content.ReadFromJsonAsync<T>(JsonOpciones, ct))!;
    }

    private async Task AsegurarTokenAsync(CancellationToken ct)
    {
        // Un margen de 1 minuto antes de que expire, para no arrancar una petición con un token
        // que vence a mitad de camino.
        if (_token is not null && DateTimeOffset.UtcNow < _tokenExpira.AddMinutes(-1))
            return;

        using var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequestDto(email, password), JsonOpciones, ct);
        await LanzarSiErrorAsync(response, ct);

        var login = (await response.Content.ReadFromJsonAsync<LoginResponseDto>(JsonOpciones, ct))!;
        _token = login.Token;
        _tokenExpira = ObtenerExpiracion(login.Token);
    }

    // El token ya trae su propia fecha de expiración (claim "exp") — se lee de ahí en vez de
    // hardcodear la duración configurada del lado de la Api.
    private static DateTimeOffset ObtenerExpiracion(string token)
    {
        try
        {
            var payload = token.Split('.')[1];
            var base64 = payload.Replace('-', '+').Replace('_', '/').PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = JsonDocument.Parse(json);
            return DateTimeOffset.FromUnixTimeSeconds(doc.RootElement.GetProperty("exp").GetInt64());
        }
        catch
        {
            return DateTimeOffset.UtcNow;
        }
    }

    private static async Task LanzarSiErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var cuerpo = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase} en {response.RequestMessage?.RequestUri}: {cuerpo}");
    }
}
