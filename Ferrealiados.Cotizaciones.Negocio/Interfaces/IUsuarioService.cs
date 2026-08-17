using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct = default);
    Task<UsuarioDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<UsuarioDto> CrearAsync(UsuarioCrearDto dto, CancellationToken ct = default);
    Task<UsuarioDto?> ActualizarAsync(int id, UsuarioActualizarDto dto, int idUsuarioActual, CancellationToken ct = default);
}
