using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

public class AuthService(AppDbContext db, IJwtGenerador jwtGenerador) : IAuthService
{
    public async Task<LoginResponseDto?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Email == email && u.Activo, ct);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash))
            return null;

        var token = jwtGenerador.GenerarToken(usuario);
        return new LoginResponseDto(token, usuario.Nombre, usuario.Email, usuario.Rol.ToString());
    }
}
