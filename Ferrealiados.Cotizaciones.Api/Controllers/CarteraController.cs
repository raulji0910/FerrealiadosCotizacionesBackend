using System.Security.Claims;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

// Cartera de clientes: estado de cuenta por correo y remisiones sin factura. La pantalla la usan
// Admin y el rol Cartera; el POST de sincronizar lo usa el Sincronizador (usuario de servicio Admin).
[ApiController]
[Authorize(Roles = "Admin,Cartera")]
[Route("api/cartera")]
public class CarteraController(ICarteraService carteraService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CarteraDto>> Obtener(CancellationToken ct)
        => Ok(await carteraService.ObtenerAsync(ct));

    [HttpGet("clientes/{nit}")]
    public async Task<ActionResult<ClienteCarteraDetalleDto>> Cliente(string nit, CancellationToken ct)
    {
        var detalle = await carteraService.ObtenerClienteAsync(nit, ct);
        return detalle is null ? NotFound() : Ok(detalle);
    }

    [HttpGet("clientes/{nit}/correo")]
    public async Task<ActionResult<CorreoCarteraDto>> VistaPrevia(string nit, CancellationToken ct)
    {
        var correo = await carteraService.ConstruirCorreoAsync(nit, ct);
        return correo is null ? NotFound() : Ok(correo);
    }

    // Un cliente por petición: la pantalla los recorre uno a uno (con pausa entre correos para que
    // el servidor de correo no lo tome como spam) y así muestra el avance y no choca con los
    // tiempos máximos del proxy en un envío masivo.
    [HttpPost("clientes/{nit}/enviar")]
    public async Task<ActionResult<ResultadoEnvioCarteraDto>> Enviar(string nit, CancellationToken ct)
    {
        var usuario = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "desconocido";
        try
        {
            var resultado = await carteraService.EnviarAsync(nit, usuario, ct);
            return resultado is null ? NotFound() : Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("clientes/{nit}/contacto")]
    public async Task<ActionResult<ClienteCarteraDetalleDto>> ActualizarContacto(string nit, ContactoCobroDto contacto, CancellationToken ct)
    {
        try
        {
            var detalle = await carteraService.ActualizarContactoAsync(nit, contacto, ct);
            return detalle is null ? NotFound() : Ok(detalle);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("contactos")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CargaContactosResultadoDto>> CargarContactos(IReadOnlyList<ContactoCobroCargaDto> contactos, CancellationToken ct)
        => Ok(await carteraService.CargarContactosAsync(contactos, ct));

    [HttpGet("remisiones")]
    public async Task<ActionResult<IReadOnlyList<RemisionSinFacturaDto>>> Remisiones(CancellationToken ct)
        => Ok(await carteraService.ObtenerRemisionesSinFacturaAsync(ct));

    [HttpGet("envios")]
    public async Task<ActionResult<IReadOnlyList<EnvioCarteraDto>>> Envios([FromQuery] int maximo = 300, CancellationToken ct = default)
        => Ok(await carteraService.ObtenerEnviosAsync(maximo, ct));

    [HttpGet("configuracion")]
    public async Task<ActionResult<ConfiguracionCarteraDto>> Configuracion(CancellationToken ct)
        => Ok(await carteraService.ObtenerConfiguracionAsync(ct));

    [HttpPut("configuracion")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ConfiguracionCarteraDto>> ActualizarConfiguracion(ConfiguracionCarteraDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await carteraService.ActualizarConfiguracionAsync(dto, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // La clave del buzón remitente: se comprueba contra el servidor de correo y se guarda cifrada.
    // Nunca se devuelve (ni aquí ni en GET configuracion).
    [HttpPut("configuracion/clave-correo")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ConfiguracionCarteraDto>> GuardarClaveCorreo(GuardarClaveCorreoDto dto, CancellationToken ct)
    {
        var usuario = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "desconocido";
        try
        {
            return Ok(await carteraService.GuardarClaveCorreoAsync(dto.Clave, usuario, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("configuracion/probar-correo")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PruebaCorreoDto>> ProbarCorreo(CancellationToken ct)
        => Ok(await carteraService.ProbarCorreoAsync(ct));

    [HttpPost("sincronizar")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<SincronizacionCarteraResultadoDto>> Sincronizar(SincronizacionCarteraEntradaDto entrada, CancellationToken ct)
        => Ok(await carteraService.SincronizarAsync(entrada, ct));
}
