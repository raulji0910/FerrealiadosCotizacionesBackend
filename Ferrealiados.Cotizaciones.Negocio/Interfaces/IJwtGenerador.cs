using Ferrealiados.Cotizaciones.Modelo.Entidades;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface IJwtGenerador
{
    string GenerarToken(Usuario usuario);
}
