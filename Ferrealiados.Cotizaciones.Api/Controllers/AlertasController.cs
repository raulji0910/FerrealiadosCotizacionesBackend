using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/alertas")]
public class AlertasController(IPrecioService precioService) : ControllerBase
{
    [HttpGet("precios-vencidos")]
    public async Task<ActionResult<PaginaResultado<AlertaPrecioDto>>> PreciosVencidos(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 10,
        CancellationToken ct = default)
        => Ok(await precioService.ObtenerAlertasVencidasAsync(pagina, tamanoPagina, ct));
}
