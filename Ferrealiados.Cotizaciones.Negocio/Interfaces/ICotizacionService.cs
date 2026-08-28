using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface ICotizacionService
{
    Task<PaginaResultado<CotizacionResumenDto>> BuscarAsync(EstadoCotizacion? estado, string? texto, int pagina, int tamanoPagina, CancellationToken ct = default);
    Task<CotizacionDetalleDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<CotizacionDetalleDto?> ObtenerBorradorPorCodigoAsync(string codigo, CancellationToken ct = default);

    Task<CotizacionItemDto> MarcarPrecioAsync(MarcarPrecioDto dto, string? usuario, CancellationToken ct = default);
    Task<bool> QuitarItemAsync(int itemId, CancellationToken ct = default);
    Task<CotizacionItemDto?> ActualizarCantidadItemAsync(int itemId, ActualizarCantidadItemDto dto, CancellationToken ct = default);

    Task<CotizacionDetalleDto?> EmitirAsync(int id, EmitirCotizacionDto dto, CancellationToken ct = default);
    Task<CotizacionDetalleDto?> ReabrirAsync(int id, CancellationToken ct = default);
    Task<byte[]?> GenerarPdfAsync(int id, CancellationToken ct = default);
}
