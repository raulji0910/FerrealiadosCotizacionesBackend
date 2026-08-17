using Ferrealiados.Cotizaciones.Negocio.Servicios;
using Xunit;

namespace Ferrealiados.Cotizaciones.TestUnitarios;

public class VigenciaPrecioTests
{
    [Fact]
    public void EsVencido_PrecioRecienCotizado_NoEstaVencido()
    {
        var hoy = new DateOnly(2026, 8, 4);
        var fechaCotizacion = hoy;

        var vencido = VigenciaPrecio.EsVencido(fechaCotizacion, mesesVigencia: 3, hoy);

        Assert.False(vencido);
    }

    [Fact]
    public void EsVencido_PrecioDeHace4MesesConUmbral3Meses_EstaVencido()
    {
        var hoy = new DateOnly(2026, 8, 4);
        var fechaCotizacion = hoy.AddMonths(-4);

        var vencido = VigenciaPrecio.EsVencido(fechaCotizacion, mesesVigencia: 3, hoy);

        Assert.True(vencido);
    }

    [Fact]
    public void EsVencido_PrecioDeHace2MesesConUmbral3Meses_NoEstaVencido()
    {
        var hoy = new DateOnly(2026, 8, 4);
        var fechaCotizacion = hoy.AddMonths(-2);

        var vencido = VigenciaPrecio.EsVencido(fechaCotizacion, mesesVigencia: 3, hoy);

        Assert.False(vencido);
    }

    [Fact]
    public void EsVencido_PrecioExactoEnElLimiteDeLosMeses_NoEstaVencido()
    {
        var hoy = new DateOnly(2026, 8, 4);
        var fechaCotizacion = hoy.AddMonths(-3);

        var vencido = VigenciaPrecio.EsVencido(fechaCotizacion, mesesVigencia: 3, hoy);

        Assert.False(vencido);
    }

    [Fact]
    public void EsVencido_UnDiaPasadoElLimite_EstaVencido()
    {
        var hoy = new DateOnly(2026, 8, 4);
        var fechaCotizacion = hoy.AddMonths(-3).AddDays(-1);

        var vencido = VigenciaPrecio.EsVencido(fechaCotizacion, mesesVigencia: 3, hoy);

        Assert.True(vencido);
    }

    [Fact]
    public void DiasDesde_CalculaLaDiferenciaEnDiasCorrectamente()
    {
        var hoy = new DateOnly(2026, 8, 4);
        var fechaCotizacion = new DateOnly(2026, 7, 25);

        var dias = VigenciaPrecio.DiasDesde(fechaCotizacion, hoy);

        Assert.Equal(10, dias);
    }
}
