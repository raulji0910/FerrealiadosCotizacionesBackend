using System.Security.Claims;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/usuarios")]
public class UsuariosController(IUsuarioService usuarioService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar(CancellationToken ct)
        => Ok(await usuarioService.ListarAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UsuarioDto>> ObtenerPorId(int id, CancellationToken ct)
    {
        var usuario = await usuarioService.ObtenerPorIdAsync(id, ct);
        return usuario is null ? NotFound() : Ok(usuario);
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Crear(UsuarioCrearDto dto, CancellationToken ct)
    {
        try
        {
            var creado = await usuarioService.CrearAsync(dto, ct);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UsuarioDto>> Actualizar(int id, UsuarioActualizarDto dto, CancellationToken ct)
    {
        var idUsuarioActual = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var actualizado = await usuarioService.ActualizarAsync(id, dto, idUsuarioActual, ct);
            return actualizado is null ? NotFound() : Ok(actualizado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }
}
