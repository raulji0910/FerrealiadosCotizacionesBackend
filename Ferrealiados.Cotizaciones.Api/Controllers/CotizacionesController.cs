using System.Security.Claims;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cotizaciones")]
public class CotizacionesController(ICotizacionService cotizacionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginaResultado<CotizacionResumenDto>>> Buscar(
        [FromQuery] EstadoCotizacion? estado,
        [FromQuery] string? texto,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 10,
        CancellationToken ct = default)
        => Ok(await cotizacionService.BuscarAsync(estado, texto, pagina, tamanoPagina, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CotizacionDetalleDto>> ObtenerPorId(int id, CancellationToken ct)
    {
        var cotizacion = await cotizacionService.ObtenerPorIdAsync(id, ct);
        return cotizacion is null ? NotFound() : Ok(cotizacion);
    }

    [HttpGet("borrador/{codigo}")]
    public async Task<ActionResult<CotizacionDetalleDto>> ObtenerBorradorPorCodigo(string codigo, CancellationToken ct)
    {
        var cotizacion = await cotizacionService.ObtenerBorradorPorCodigoAsync(codigo, ct);
        return cotizacion is null ? NotFound() : Ok(cotizacion);
    }

    [HttpPost("marcas")]
    public async Task<ActionResult<CotizacionItemDto>> MarcarPrecio(MarcarPrecioDto dto, CancellationToken ct)
    {
        try
        {
            var item = await cotizacionService.MarcarPrecioAsync(dto, ObtenerUsuarioActual(), ct);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpDelete("items/{itemId:int}")]
    public async Task<IActionResult> QuitarItem(int itemId, CancellationToken ct)
    {
        try
        {
            var eliminado = await cotizacionService.QuitarItemAsync(itemId, ct);
            return eliminado ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPut("items/{itemId:int}")]
    public async Task<ActionResult<CotizacionItemDto>> ActualizarCantidad(int itemId, ActualizarCantidadItemDto dto, CancellationToken ct)
    {
        try
        {
            var item = await cotizacionService.ActualizarCantidadItemAsync(itemId, dto, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPut("items/{itemId:int}/precio")]
    public async Task<ActionResult<CotizacionItemDto>> ActualizarPrecio(int itemId, ActualizarPrecioItemDto dto, CancellationToken ct)
    {
        try
        {
            var item = await cotizacionService.ActualizarPrecioItemAsync(itemId, dto, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPut("items/{itemId:int}/iva")]
    public async Task<ActionResult<CotizacionItemDto>> ActualizarIva(int itemId, ActualizarIvaItemDto dto, CancellationToken ct)
    {
        try
        {
            var item = await cotizacionService.ActualizarIvaItemAsync(itemId, dto, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPost("{id:int}/emitir")]
    public async Task<ActionResult<CotizacionDetalleDto>> Emitir(int id, EmitirCotizacionDto dto, CancellationToken ct)
    {
        try
        {
            var emitida = await cotizacionService.EmitirAsync(id, dto, ct);
            return emitida is null ? NotFound() : Ok(emitida);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { mensaje = "La cotización ya fue emitida o modificada por otro proceso. Recarga e intenta de nuevo." });
        }
    }

    [HttpPost("{id:int}/reabrir")]
    public async Task<ActionResult<CotizacionDetalleDto>> Reabrir(int id, CancellationToken ct)
    {
        try
        {
            var reabierta = await cotizacionService.ReabrirAsync(id, ct);
            return reabierta is null ? NotFound() : Ok(reabierta);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> DescargarPdf(int id, CancellationToken ct)
    {
        try
        {
            var pdf = await cotizacionService.GenerarPdfAsync(id, ct);
            return pdf is null ? NotFound() : File(pdf, "application/pdf", $"cotizacion-{id}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    private string? ObtenerUsuarioActual()
        => User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
}
