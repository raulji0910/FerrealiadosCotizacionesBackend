using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/proveedores")]
public class ProveedoresController(IProveedorService proveedorService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginaResultado<ProveedorDto>>> Buscar(
        [FromQuery] string? buscar,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 10,
        CancellationToken ct = default)
        => Ok(await proveedorService.BuscarAsync(buscar, pagina, tamanoPagina, ct));

    // Sin paginar: usado para poblar el select de proveedores al registrar un precio.
    [HttpGet("activos")]
    public async Task<ActionResult<IReadOnlyList<ProveedorDto>>> ListarActivos(CancellationToken ct)
        => Ok(await proveedorService.ListarActivosAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProveedorDto>> ObtenerPorId(int id, CancellationToken ct)
    {
        var proveedor = await proveedorService.ObtenerPorIdAsync(id, ct);
        return proveedor is null ? NotFound() : Ok(proveedor);
    }

    [HttpPost]
    public async Task<ActionResult<ProveedorDto>> Crear(ProveedorCrearDto dto, CancellationToken ct)
    {
        try
        {
            var creado = await proveedorService.CrearAsync(dto, ct);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProveedorDto>> Actualizar(int id, ProveedorActualizarDto dto, CancellationToken ct)
    {
        var actualizado = await proveedorService.ActualizarAsync(id, dto, ct);
        return actualizado is null ? NotFound() : Ok(actualizado);
    }
}
