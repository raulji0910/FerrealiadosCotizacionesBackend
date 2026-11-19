namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record ConfiguracionDto(int MesesVigenciaPrecio);

public record LoginRequestDto(string Email, string Password);

// Roles: todos los del usuario (multirol). Rol: el principal, solo por compatibilidad con sesiones viejas.
public record LoginResponseDto(string Token, string Nombre, string Email, IReadOnlyList<string> Roles, string Rol);
