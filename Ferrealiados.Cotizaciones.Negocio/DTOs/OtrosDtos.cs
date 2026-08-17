namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record ConfiguracionDto(int MesesVigenciaPrecio);

public record LoginRequestDto(string Email, string Password);

public record LoginResponseDto(string Token, string Nombre, string Email, string Rol);
