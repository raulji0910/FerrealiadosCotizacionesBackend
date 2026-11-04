using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface IConciliacionService
{
    Task<RegistroFacturasResultadoDto> RegistrarFacturasAsync(IReadOnlyList<FacturaXmlEntradaDto> facturas, CancellationToken ct = default);
    Task<SincronizacionOrdenesResultadoDto> SincronizarOrdenesAsync(IReadOnlyList<OrdenCompraEntradaDto> ordenes, CancellationToken ct = default);
    Task<ConciliacionDto> ConciliarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default);
    Task<DetalleFacturaConciliadaDto?> ObtenerDetalleAsync(int facturaId, CancellationToken ct = default);
}
