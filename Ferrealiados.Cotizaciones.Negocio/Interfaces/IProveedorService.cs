using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface IProveedorService
{
    Task<PaginaResultado<ProveedorDto>> BuscarAsync(string? texto, int pagina, int tamanoPagina, CancellationToken ct = default);

    // Sin paginar: para poblar selects (ej. registrar precio de proveedor), no para listados navegables.
    Task<IReadOnlyList<ProveedorDto>> ListarActivosAsync(CancellationToken ct = default);
    Task<ProveedorDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<ProveedorDto> CrearAsync(ProveedorCrearDto dto, CancellationToken ct = default);
    Task<ProveedorDto?> ActualizarAsync(int id, ProveedorActualizarDto dto, CancellationToken ct = default);
}
