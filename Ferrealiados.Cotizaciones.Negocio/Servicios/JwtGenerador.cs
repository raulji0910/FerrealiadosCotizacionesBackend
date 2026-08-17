using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

public class JwtGenerador(IConfiguration configuration) : IJwtGenerador
{
    public string GenerarToken(Usuario usuario)
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Falta configurar Jwt:Secret.");
        var issuer = configuration["Jwt:Issuer"] ?? "Ferrealiados.Cotizaciones";
        var audience = configuration["Jwt:Audience"] ?? "Ferrealiados.Cotizaciones";
        var expiracionMinutos = int.TryParse(configuration["Jwt:ExpiracionMinutos"], out var m) ? m : 480;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Role, usuario.Rol.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiracionMinutos),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
