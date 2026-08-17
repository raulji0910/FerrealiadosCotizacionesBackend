using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/productos")]
public class ProductosController(IProductoService productoService, IPrecioService precioService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginaResultado<ProductoDto>>> Buscar(
        [FromQuery] string? buscar,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 10,
        CancellationToken ct = default)
        => Ok(await productoService.BuscarAsync(buscar, pagina, tamanoPagina, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductoDto>> ObtenerPorId(int id, CancellationToken ct)
    {
        var producto = await productoService.ObtenerPorIdAsync(id, ct);
        return producto is null ? NotFound() : Ok(producto);
    }

    [HttpPost]
    public async Task<ActionResult<ProductoDto>> Crear(ProductoCrearDto dto, CancellationToken ct)
    {
        try
        {
            var creado = await productoService.CrearAsync(dto, ct);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, ProductoActualizarDto dto, CancellationToken ct)
    {
        var actualizado = await productoService.ActualizarAsync(id, dto, ct);
        return actualizado is null ? NotFound() : Ok(actualizado);
    }

    [HttpGet("{id:int}/precios")]
    public async Task<ActionResult<IReadOnlyList<PrecioProveedorDto>>> ObtenerPrecios(int id, CancellationToken ct)
    {
        var producto = await productoService.ObtenerPorIdAsync(id, ct);
        if (producto is null)
            return NotFound();

        return Ok(await precioService.ObtenerPreciosPorProductoAsync(id, ct));
    }

    [HttpPost("{id:int}/precios")]
    public async Task<ActionResult<PrecioProveedorDto>> RegistrarPrecio(int id, RegistrarPrecioDto dto, CancellationToken ct)
    {
        try
        {
            var precio = await precioService.RegistrarPrecioAsync(id, dto, ct);
            return Ok(precio);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }
}
