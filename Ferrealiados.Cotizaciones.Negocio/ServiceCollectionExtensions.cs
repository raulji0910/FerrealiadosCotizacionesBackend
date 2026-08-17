using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Ferrealiados.Cotizaciones.Negocio.Servicios;
using Microsoft.Extensions.DependencyInjection;

namespace Ferrealiados.Cotizaciones.Negocio;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNegocio(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<IProveedorService, ProveedorService>();
        services.AddScoped<IPrecioService, PrecioService>();
        services.AddScoped<IConfiguracionService, ConfiguracionService>();
        services.AddScoped<IJwtGenerador, JwtGenerador>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        return services;
    }
}
