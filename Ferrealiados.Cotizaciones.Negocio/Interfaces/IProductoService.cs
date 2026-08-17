using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface IProductoService
{
    Task<PaginaResultado<ProductoDto>> BuscarAsync(string? texto, int pagina, int tamanoPagina, CancellationToken ct = default);
    Task<ProductoDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<ProductoDto> CrearAsync(ProductoCrearDto dto, CancellationToken ct = default);
    Task<ProductoDto?> ActualizarAsync(int id, ProductoActualizarDto dto, CancellationToken ct = default);
}
