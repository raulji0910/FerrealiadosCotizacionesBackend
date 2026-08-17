namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface IConfiguracionService
{
    Task<int> ObtenerMesesVigenciaPrecioAsync(CancellationToken ct = default);
    Task ActualizarMesesVigenciaPrecioAsync(int meses, CancellationToken ct = default);
}
