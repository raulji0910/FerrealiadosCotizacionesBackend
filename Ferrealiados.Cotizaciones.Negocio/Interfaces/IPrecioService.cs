using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface IPrecioService
{
    Task<IReadOnlyList<PrecioProveedorDto>> ObtenerPreciosPorProductoAsync(int productoId, CancellationToken ct = default);
    Task<PrecioProveedorDto> RegistrarPrecioAsync(int productoId, RegistrarPrecioDto dto, CancellationToken ct = default);
    Task<PrecioActualizadoDto?> ActualizarPorcentajeAsync(int productoId, int precioId, ActualizarPorcentajeDto dto, CancellationToken ct = default);
    Task<IvaActualizadoDto?> ActualizarIvaAsync(int productoId, int precioId, ActualizarIvaDto dto, CancellationToken ct = default);
    Task<PaginaResultado<AlertaPrecioDto>> ObtenerAlertasVencidasAsync(int pagina, int tamanoPagina, CancellationToken ct = default);
}
