using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface IClienteService
{
    Task<PaginaResultado<ClienteDto>> BuscarAsync(string? texto, int pagina, int tamanoPagina, CancellationToken ct = default);

    // Sin paginar: para poblar selects (ej. cargar una cotización a un cliente), no para listados navegables.
    Task<IReadOnlyList<ClienteDto>> ListarActivosAsync(CancellationToken ct = default);
    Task<ClienteDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<ClienteDto> CrearAsync(ClienteCrearDto dto, CancellationToken ct = default);
    Task<ClienteDto?> ActualizarAsync(int id, ClienteActualizarDto dto, CancellationToken ct = default);
}
