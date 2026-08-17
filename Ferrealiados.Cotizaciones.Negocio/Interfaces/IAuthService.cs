using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(string email, string password, CancellationToken ct = default);
}
