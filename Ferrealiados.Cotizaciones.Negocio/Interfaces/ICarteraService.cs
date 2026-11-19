using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface ICarteraService
{
    Task<SincronizacionCarteraResultadoDto> SincronizarAsync(SincronizacionCarteraEntradaDto entrada, CancellationToken ct = default);
    Task<CarteraDto> ObtenerAsync(CancellationToken ct = default);
    Task<ClienteCarteraDetalleDto?> ObtenerClienteAsync(string nit, CancellationToken ct = default);
    Task<CorreoCarteraDto?> ConstruirCorreoAsync(string nit, CancellationToken ct = default);
    Task<ResultadoEnvioCarteraDto?> EnviarAsync(string nit, string usuario, CancellationToken ct = default);
    Task<ClienteCarteraDetalleDto?> ActualizarContactoAsync(string nit, ContactoCobroDto contacto, CancellationToken ct = default);
    Task<CargaContactosResultadoDto> CargarContactosAsync(IReadOnlyList<ContactoCobroCargaDto> contactos, CancellationToken ct = default);
    Task<IReadOnlyList<RemisionSinFacturaDto>> ObtenerRemisionesSinFacturaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EnvioCarteraDto>> ObtenerEnviosAsync(int maximo, CancellationToken ct = default);
    Task<ConfiguracionCarteraDto> ObtenerConfiguracionAsync(CancellationToken ct = default);
    Task<ConfiguracionCarteraDto> ActualizarConfiguracionAsync(ConfiguracionCarteraDto dto, CancellationToken ct = default);
    Task<ConfiguracionCarteraDto> GuardarClaveCorreoAsync(string clave, string usuario, CancellationToken ct = default);
    Task<PruebaCorreoDto> ProbarCorreoAsync(CancellationToken ct = default);
}
