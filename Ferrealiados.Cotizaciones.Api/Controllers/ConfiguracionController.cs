using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/configuracion")]
public class ConfiguracionController(IConfiguracionService configuracionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ConfiguracionDto>> Obtener(CancellationToken ct)
        => Ok(new ConfiguracionDto(await configuracionService.ObtenerMesesVigenciaPrecioAsync(ct)));

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ConfiguracionDto>> Actualizar(ConfiguracionDto dto, CancellationToken ct)
    {
        await configuracionService.ActualizarMesesVigenciaPrecioAsync(dto.MesesVigenciaPrecio, ct);
        return Ok(dto);
    }
}
