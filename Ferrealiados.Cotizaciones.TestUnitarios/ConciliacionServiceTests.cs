using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Negocio.Conciliacion;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Servicios;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ferrealiados.Cotizaciones.TestUnitarios;

public class ConciliacionServiceTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 30);

    private static AppDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opciones);
    }

    private static ConciliacionService CrearServicio(AppDbContext db)
        => new(db, new TiempoFijo(Hoy.ToDateTime(TimeOnly.MinValue)));

    private static FacturaXmlEntradaDto Xml(string numero = "FE5611847", string fecha = "2026-10-21")
        => new(LectorFacturaElectronicaTests.AttachedDocument(LectorFacturaElectronicaTests.Invoice(numero: numero, fecha: fecha)),
            "ad.xml", "890900652;INVESA S.A;FE5611847;01", null);

    private static OrdenCompraEntradaDto Oc(int idAsiento, int numero, decimal cantidad, decimal valorUnitario, bool anulada = false,
        DateOnly? fecha = null)
        => new(idAsiento, numero, fecha ?? new DateOnly(2026, 10, 20), "890.900.652", "INVESA S.A", anulada,
            [new OrdenCompraRenglonEntradaDto("COD-22451", "INVECRYL 500 X 750 GR", cantidad, valorUnitario, 19m)]);

    [Fact]
    public async Task RegistrarFacturas_MismoCufeDosVeces_SoloGuardaUna()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);

        var primera = await servicio.RegistrarFacturasAsync([Xml(), Xml()]);
        var segunda = await servicio.RegistrarFacturasAsync([Xml()]);

        Assert.Equal(new RegistroFacturasResultadoDto(1, 1, 0), primera);
        Assert.Equal(new RegistroFacturasResultadoDto(0, 1, 0), segunda);
        Assert.Equal(1, await db.FacturasProveedor.CountAsync());
    }

    [Fact]
    public async Task RegistrarFacturas_XmlInvalido_CuentaComoNoReconocida()
    {
        using var db = CrearContexto();

        var resultado = await CrearServicio(db).RegistrarFacturasAsync([new FacturaXmlEntradaDto("esto no es xml", null, null, null)]);

        Assert.Equal(new RegistroFacturasResultadoDto(0, 0, 1), resultado);
    }

    [Fact]
    public async Task SincronizarOrdenes_CalculaSubtotalConCantidadPorValorUnitarioEIva()
    {
        using var db = CrearContexto();

        await CrearServicio(db).SincronizarOrdenesAsync([Oc(7830, 1332, 10m, 7500m)]);

        var oc = await db.OrdenesCompraWorldOffice.SingleAsync();
        Assert.Equal(75000m, oc.Subtotal);
        Assert.Equal(14250m, oc.Iva);
        Assert.Equal("890900652", oc.NitProveedor);
    }

    [Fact]
    public async Task SincronizarOrdenes_OcYaExistente_ReemplazaRenglonesSinDuplicar()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);

        await servicio.SincronizarOrdenesAsync([Oc(7830, 1332, 10m, 7500m)]);
        var resultado = await servicio.SincronizarOrdenesAsync([Oc(7830, 1332, 8m, 7500m)]);

        Assert.Equal(new SincronizacionOrdenesResultadoDto(0, 1), resultado);
        var oc = await db.OrdenesCompraWorldOffice.Include(o => o.Renglones).SingleAsync();
        Assert.Single(oc.Renglones);
        Assert.Equal(60000m, oc.Subtotal);
    }

    [Fact]
    public async Task Conciliar_FacturaYOcIguales_Cuadra()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.RegistrarFacturasAsync([Xml()]);
        await servicio.SincronizarOrdenesAsync([Oc(7830, 1332, 10m, 7500m)]);

        var conciliacion = await servicio.ConciliarAsync(Hoy.AddDays(-30), Hoy);

        var factura = Assert.Single(conciliacion.Facturas);
        Assert.Equal(EstadoConciliacion.Cuadra, factura.Estado);
        Assert.Equal(1332, factura.OrdenCompraNumero);
        Assert.Equal(1, conciliacion.Resumen.Cuadran);
        Assert.Empty(conciliacion.OrdenesPendientes);
    }

    [Fact]
    public async Task Conciliar_OcAnulada_NoSeUsa()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.RegistrarFacturasAsync([Xml()]);
        await servicio.SincronizarOrdenesAsync([Oc(7830, 1332, 10m, 7500m, anulada: true)]);

        var conciliacion = await servicio.ConciliarAsync(Hoy.AddDays(-30), Hoy);

        Assert.Equal(EstadoConciliacion.SinOrdenCompra, conciliacion.Facturas[0].Estado);
    }

    [Fact]
    public async Task Conciliar_OcSinFacturaAnteriorAlRango_NoSeMuestraComoPendiente()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarOrdenesAsync([
            Oc(7001, 1100, 10m, 7500m, fecha: new DateOnly(2026, 9, 15)),
            Oc(7002, 1101, 10m, 7500m, fecha: new DateOnly(2026, 10, 15))
        ]);

        var conciliacion = await servicio.ConciliarAsync(new DateOnly(2026, 10, 1), Hoy);

        Assert.Equal(1101, Assert.Single(conciliacion.OrdenesPendientes).Numero);
    }

    private static FacturaCompraEntradaDto Fc(int idAsiento, int numero, string? referencia, string nit = "890.900.652",
        DateOnly? fecha = null)
        => new(idAsiento, numero, fecha ?? new DateOnly(2026, 10, 22), nit, "INVESA S.A", "FE999", referencia, false);

    [Fact]
    public async Task SincronizarFacturasCompra_ReemplazaLaVentanaYBorraLasQueYaNoVienen()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        var desde = new DateOnly(2026, 6, 1);
        await servicio.SincronizarFacturasCompraAsync(new(desde, [Fc(1, 10, "1332"), Fc(2, 11, "1333")]));

        var resultado = await servicio.SincronizarFacturasCompraAsync(new(desde, [Fc(1, 10, "1332-1334")]));

        Assert.Equal(new SincronizacionFacturasCompraResultadoDto(0, 1, 1), resultado);
        var fc = Assert.Single(await db.FacturasCompraWorldOffice.ToListAsync());
        Assert.Equal("1332-1334", fc.ReferenciaOrdenes);
        Assert.Equal("890900652", fc.NitProveedor);
    }

    [Fact]
    public async Task Conciliar_OcCitadaEnFcSinXml_VaAFacturadasSinXmlYNoAPendientes()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarOrdenesAsync([Oc(7001, 1332, 10m, 7500m), Oc(7002, 1333, 1m, 5000m)]);
        await servicio.SincronizarFacturasCompraAsync(new(new DateOnly(2026, 6, 1), [Fc(1, 1069, "1332-1120")]));

        var conciliacion = await servicio.ConciliarAsync(new DateOnly(2026, 10, 1), Hoy);

        var facturada = Assert.Single(conciliacion.OrdenesFacturadasSinXml);
        Assert.Equal((1332, 1069), (facturada.Numero, facturada.FacturaCompraNumero));
        Assert.Equal(1333, Assert.Single(conciliacion.OrdenesPendientes).Numero);
        Assert.Equal(1120, Assert.Single(conciliacion.ReferenciasInvalidas).OrdenCitada);
        Assert.Equal((1, 1, 1), (conciliacion.Resumen.OrdenesPendientes, conciliacion.Resumen.OrdenesFacturadasSinXml,
            conciliacion.Resumen.ReferenciasInvalidas));
    }

    [Fact]
    public async Task ObtenerDetalle_TraeRenglonesDeFacturaYOc()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.RegistrarFacturasAsync([Xml()]);
        await servicio.SincronizarOrdenesAsync([Oc(7830, 1332, 10m, 7500m)]);
        var id = (await db.FacturasProveedor.SingleAsync()).Id;

        var detalle = await servicio.ObtenerDetalleAsync(id);

        Assert.NotNull(detalle);
        Assert.Single(detalle.RenglonesFactura);
        Assert.Equal("INVECRYL 500 X 750 GR", Assert.Single(detalle.RenglonesOrdenCompra).Descripcion);
    }

    private sealed class TiempoFijo(DateTime ahora) : TimeProvider
    {
        private readonly DateTimeOffset _ahora = new(ahora, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _ahora;
    }
}
