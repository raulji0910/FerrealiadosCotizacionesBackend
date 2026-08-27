using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clientes")]
public class ClientesController(IClienteService clienteService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginaResultado<ClienteDto>>> Buscar(
        [FromQuery] string? buscar,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 10,
        CancellationToken ct = default)
        => Ok(await clienteService.BuscarAsync(buscar, pagina, tamanoPagina, ct));

    // Sin paginar: usado para poblar el select de clientes al cargar una cotización.
    [HttpGet("activos")]
    public async Task<ActionResult<IReadOnlyList<ClienteDto>>> ListarActivos(CancellationToken ct)
        => Ok(await clienteService.ListarActivosAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClienteDto>> ObtenerPorId(int id, CancellationToken ct)
    {
        var cliente = await clienteService.ObtenerPorIdAsync(id, ct);
        return cliente is null ? NotFound() : Ok(cliente);
    }

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Crear(ClienteCrearDto dto, CancellationToken ct)
    {
        try
        {
            var creado = await clienteService.CrearAsync(dto, ct);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClienteDto>> Actualizar(int id, ClienteActualizarDto dto, CancellationToken ct)
    {
        var actualizado = await clienteService.ActualizarAsync(id, dto, ct);
        return actualizado is null ? NotFound() : Ok(actualizado);
    }
}
