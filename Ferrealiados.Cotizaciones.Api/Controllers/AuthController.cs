using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto dto, CancellationToken ct)
    {
        var resultado = await authService.LoginAsync(dto.Email, dto.Password, ct);
        if (resultado is null)
            return Unauthorized(new { mensaje = "Credenciales inválidas." });

        return Ok(resultado);
    }
}
