using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

public class ConfiguracionService(AppDbContext db) : IConfiguracionService
{
    private const int MesesVigenciaPorDefecto = 3;

    public async Task<int> ObtenerMesesVigenciaPrecioAsync(CancellationToken ct = default)
    {
        var config = await db.ConfiguracionSistema
            .FirstOrDefaultAsync(c => c.Clave == ClavesConfiguracion.MesesVigenciaPrecio, ct);

        if (config is null || !int.TryParse(config.Valor, out var meses))
            return MesesVigenciaPorDefecto;

        return meses;
    }

    public async Task ActualizarMesesVigenciaPrecioAsync(int meses, CancellationToken ct = default)
    {
        if (meses <= 0)
            throw new ArgumentOutOfRangeException(nameof(meses), "Los meses de vigencia deben ser mayores a cero.");

        var config = await db.ConfiguracionSistema
            .FirstOrDefaultAsync(c => c.Clave == ClavesConfiguracion.MesesVigenciaPrecio, ct);

        if (config is null)
        {
            db.ConfiguracionSistema.Add(new ConfiguracionSistema
            {
                Clave = ClavesConfiguracion.MesesVigenciaPrecio,
                Valor = meses.ToString(),
                Descripcion = "Meses tras los cuales un precio cotizado se considera desactualizado."
            });
        }
        else
        {
            config.Valor = meses.ToString();
        }

        await db.SaveChangesAsync(ct);
    }
}
