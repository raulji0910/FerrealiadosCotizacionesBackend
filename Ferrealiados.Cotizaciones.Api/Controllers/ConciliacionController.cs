using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferrealiados.Cotizaciones.Api.Controllers;

// Conciliación XML de proveedores vs OC de World Office. Los POST los usa el Sincronizador (corre
// en la red de Ferrealiados, autenticado como un usuario Admin de servicio); el GET, la pantalla.
[ApiController]
[Authorize]
[Route("api/conciliacion")]
public class ConciliacionController(IConciliacionService conciliacionService, TimeProvider reloj) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ConciliacionDto>> Conciliar(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var hastaReal = hasta ?? hoy;
        var desdeReal = desde ?? hastaReal.AddDays(-30);
        if (desdeReal > hastaReal)
            return BadRequest("La fecha 'desde' no puede ser posterior a 'hasta'.");
        if (hastaReal.DayNumber - desdeReal.DayNumber > 366)
            return BadRequest("El rango máximo es de un año.");

        return Ok(await conciliacionService.ConciliarAsync(desdeReal, hastaReal, ct));
    }

    [HttpGet("facturas/{id:int}")]
    public async Task<ActionResult<DetalleFacturaConciliadaDto>> Detalle(int id, CancellationToken ct = default)
    {
        var detalle = await conciliacionService.ObtenerDetalleAsync(id, ct);
        return detalle is null ? NotFound() : Ok(detalle);
    }

    [HttpPost("facturas")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<RegistroFacturasResultadoDto>> RegistrarFacturas(
        [FromBody] IReadOnlyList<FacturaXmlEntradaDto> facturas,
        CancellationToken ct = default)
        => Ok(await conciliacionService.RegistrarFacturasAsync(facturas, ct));

    // ZIP/XML descargados de la DIAN, varios a la vez, desde la pantalla. La DIAN es la que tiene
    // todas las facturas: esto completa las que el proveedor no mandó a facturacion@.
    [HttpPost("cargar-archivos")]
    [Authorize(Roles = "Admin,Conciliador")]
    [RequestSizeLimit(MaximoCargaBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximoCargaBytes)]
    public async Task<ActionResult<CargaArchivosResultadoDto>> CargarArchivos(
        [FromForm] List<IFormFile> archivos,
        CancellationToken ct = default)
    {
        if (archivos.Count == 0)
            return BadRequest("Selecciona al menos un archivo .zip o .xml.");
        if (archivos.Count > MaximoArchivosPorCarga)
            return BadRequest($"Máximo {MaximoArchivosPorCarga} archivos por carga.");

        var abiertos = new List<Stream>();
        try
        {
            var cargados = new List<ArchivoCargado>();
            foreach (var archivo in archivos)
            {
                var contenido = archivo.OpenReadStream();
                abiertos.Add(contenido);
                cargados.Add(new ArchivoCargado(Path.GetFileName(archivo.FileName), contenido));
            }
            return Ok(await conciliacionService.CargarArchivosAsync(cargados, ct));
        }
        finally
        {
            foreach (var s in abiertos)
                await s.DisposeAsync();
        }
    }

    private const int MaximoCargaBytes = 60_000_000;
    private const int MaximoArchivosPorCarga = 500;

    [HttpPost("ordenes")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<SincronizacionOrdenesResultadoDto>> SincronizarOrdenes(
        [FromBody] IReadOnlyList<OrdenCompraEntradaDto> ordenes,
        CancellationToken ct = default)
        => Ok(await conciliacionService.SincronizarOrdenesAsync(ordenes, ct));

    [HttpPost("facturas-compra")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<SincronizacionFacturasCompraResultadoDto>> SincronizarFacturasCompra(
        [FromBody] SincronizacionFacturasCompraEntradaDto entrada,
        CancellationToken ct = default)
        => Ok(await conciliacionService.SincronizarFacturasCompraAsync(entrada, ct));
}
