using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Ferrealiados.Cotizaciones.Negocio.Servicios;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Ferrealiados.Cotizaciones.TestUnitarios;

public class PrecioServiceTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 4);

    private static AppDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opciones);
    }

    private static PrecioService CrearServicio(AppDbContext db, int mesesVigencia = 3)
    {
        var configuracionMock = new Mock<IConfiguracionService>();
        configuracionMock
            .Setup(c => c.ObtenerMesesVigenciaPrecioAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mesesVigencia);

        var tiempoFijo = new TiempoFijo(Hoy.ToDateTime(TimeOnly.MinValue));

        return new PrecioService(db, configuracionMock.Object, tiempoFijo);
    }

    private static async Task<(Producto producto, Proveedor barato, Proveedor caro)> SembrarProductoConDosProveedoresAsync(AppDbContext db)
    {
        var producto = new Producto { Codigo = "P-1", Nombre = "Martillo", Activo = true, FechaCreacion = DateTime.UtcNow };
        var proveedorBarato = new Proveedor { Nombre = "Proveedor Barato", Activo = true };
        var proveedorCaro = new Proveedor { Nombre = "Proveedor Caro", Activo = true };

        db.AddRange(producto, proveedorBarato, proveedorCaro);
        await db.SaveChangesAsync();

        return (producto, proveedorBarato, proveedorCaro);
    }

    [Fact]
    public async Task ObtenerPreciosPorProductoAsync_OrdenaPorMenorCostoBaseYMarcaMejorPrecio()
    {
        await using var db = CrearContexto();
        var (producto, barato, caro) = await SembrarProductoConDosProveedoresAsync(db);

        // Costo (con ajuste de porcentaje) queda a propósito "al revés" de CostoBase acá: el mejor
        // precio debe decidirse por CostoBase (el costo normal), Costo es solo informativo.
        db.ProductoProveedorPrecios.AddRange(
            new ProductoProveedorPrecio { ProductoId = producto.Id, ProveedorId = barato.Id, CostoBase = 100, Costo = 999, FechaCotizacion = Hoy, FechaRegistro = DateTime.UtcNow },
            new ProductoProveedorPrecio { ProductoId = producto.Id, ProveedorId = caro.Id, CostoBase = 200, Costo = 1, FechaCotizacion = Hoy, FechaRegistro = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db);
        var resultado = await servicio.ObtenerPreciosPorProductoAsync(producto.Id);

        Assert.Equal(2, resultado.Count);
        Assert.Equal(barato.Id, resultado[0].ProveedorId);
        Assert.True(resultado[0].EsMejorPrecio);
        Assert.False(resultado[1].EsMejorPrecio);
    }

    [Fact]
    public async Task ObtenerPreciosPorProductoAsync_MarcaVencidoSegunMesesConfigurados()
    {
        await using var db = CrearContexto();
        var (producto, vigente, vencido) = await SembrarProductoConDosProveedoresAsync(db);

        db.ProductoProveedorPrecios.AddRange(
            new ProductoProveedorPrecio { ProductoId = producto.Id, ProveedorId = vigente.Id, Costo = 100, FechaCotizacion = Hoy.AddMonths(-1), FechaRegistro = DateTime.UtcNow },
            new ProductoProveedorPrecio { ProductoId = producto.Id, ProveedorId = vencido.Id, Costo = 150, FechaCotizacion = Hoy.AddMonths(-4), FechaRegistro = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db, mesesVigencia: 3);
        var resultado = await servicio.ObtenerPreciosPorProductoAsync(producto.Id);

        var precioVigente = resultado.Single(p => p.ProveedorId == vigente.Id);
        var precioVencido = resultado.Single(p => p.ProveedorId == vencido.Id);

        Assert.False(precioVigente.Vencido);
        Assert.True(precioVencido.Vencido);
    }

    [Fact]
    public async Task ObtenerPreciosPorProductoAsync_TomaSoloElPrecioMasRecientePorProveedor()
    {
        await using var db = CrearContexto();
        var (producto, proveedor, _) = await SembrarProductoConDosProveedoresAsync(db);

        db.ProductoProveedorPrecios.AddRange(
            new ProductoProveedorPrecio { ProductoId = producto.Id, ProveedorId = proveedor.Id, Costo = 100, FechaCotizacion = Hoy.AddMonths(-6), FechaRegistro = DateTime.UtcNow },
            new ProductoProveedorPrecio { ProductoId = producto.Id, ProveedorId = proveedor.Id, Costo = 120, FechaCotizacion = Hoy, FechaRegistro = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db);
        var resultado = await servicio.ObtenerPreciosPorProductoAsync(producto.Id);

        var precioUnico = Assert.Single(resultado);
        Assert.Equal(120, precioUnico.Costo);
        Assert.Equal(Hoy, precioUnico.FechaCotizacion);
    }

    [Fact]
    public async Task ActualizarPorcentajeAsync_RecalculaCostoSinTocarCostoBase()
    {
        await using var db = CrearContexto();
        var (producto, proveedor, _) = await SembrarProductoConDosProveedoresAsync(db);

        var precio = new ProductoProveedorPrecio
        {
            ProductoId = producto.Id,
            ProveedorId = proveedor.Id,
            CostoBase = 100,
            Costo = 100,
            PorcentajeAjuste = 0,
            FechaCotizacion = Hoy,
            FechaRegistro = DateTime.UtcNow
        };
        db.ProductoProveedorPrecios.Add(precio);
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db);
        var resultado = await servicio.ActualizarPorcentajeAsync(producto.Id, precio.Id, new ActualizarPorcentajeDto(20));

        Assert.NotNull(resultado);
        Assert.Equal(120, resultado!.Costo);
        Assert.Equal(20, resultado.PorcentajeAjuste);

        var recargado = await db.ProductoProveedorPrecios.FindAsync(precio.Id);
        Assert.Equal(100, recargado!.CostoBase);
        Assert.Equal(120, recargado.Costo);
    }

    [Fact]
    public async Task ActualizarPorcentajeAsync_DevuelveNullSiElPrecioNoPerteneceAlProducto()
    {
        await using var db = CrearContexto();
        var (producto, proveedor, _) = await SembrarProductoConDosProveedoresAsync(db);
        var otroProducto = new Producto { Nombre = "Otro", Activo = true, FechaCreacion = DateTime.UtcNow };
        db.Productos.Add(otroProducto);

        var precio = new ProductoProveedorPrecio
        {
            ProductoId = producto.Id,
            ProveedorId = proveedor.Id,
            CostoBase = 100,
            Costo = 100,
            FechaCotizacion = Hoy,
            FechaRegistro = DateTime.UtcNow
        };
        db.ProductoProveedorPrecios.Add(precio);
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db);
        var resultado = await servicio.ActualizarPorcentajeAsync(otroProducto.Id, precio.Id, new ActualizarPorcentajeDto(20));

        Assert.Null(resultado);
    }

    [Fact]
    public async Task RegistrarPrecioAsync_InsertaNuevoRegistroSinSobreescribirHistorico()
    {
        await using var db = CrearContexto();
        var (producto, proveedor, _) = await SembrarProductoConDosProveedoresAsync(db);

        db.ProductoProveedorPrecios.Add(new ProductoProveedorPrecio
        {
            ProductoId = producto.Id,
            ProveedorId = proveedor.Id,
            Costo = 100,
            FechaCotizacion = Hoy.AddMonths(-1),
            FechaRegistro = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db);
        await servicio.RegistrarPrecioAsync(producto.Id, new RegistrarPrecioDto(proveedor.Id, 130, 0, Hoy, null, "test"));

        var historico = await db.ProductoProveedorPrecios
            .Where(p => p.ProductoId == producto.Id && p.ProveedorId == proveedor.Id)
            .ToListAsync();

        Assert.Equal(2, historico.Count);
    }

    [Fact]
    public async Task ObtenerAlertasVencidasAsync_SoloIncluyeLosPreciosVigentesVencidos()
    {
        await using var db = CrearContexto();
        var (producto, vigente, vencido) = await SembrarProductoConDosProveedoresAsync(db);

        db.ProductoProveedorPrecios.AddRange(
            new ProductoProveedorPrecio { ProductoId = producto.Id, ProveedorId = vigente.Id, Costo = 100, FechaCotizacion = Hoy, FechaRegistro = DateTime.UtcNow },
            new ProductoProveedorPrecio { ProductoId = producto.Id, ProveedorId = vencido.Id, Costo = 150, FechaCotizacion = Hoy.AddMonths(-5), FechaRegistro = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db, mesesVigencia: 3);
        var resultado = await servicio.ObtenerAlertasVencidasAsync(pagina: 1, tamanoPagina: 10);

        var alerta = Assert.Single(resultado.Items);
        Assert.Equal(1, resultado.Total);
        Assert.Equal(vencido.Id, alerta.ProveedorId);
        Assert.Equal(producto.Id, alerta.ProductoId);
    }

    [Theory]
    [InlineData(100, 10, 110)]
    [InlineData(100, -10, 90)]
    [InlineData(100, -100, 0)]
    [InlineData(100, 0, 100)]
    [InlineData(33.33, 15, 38.33)]
    public void AjustePrecio_CalcularCostoFinal_AplicaPorcentajeYRedondea(decimal costoBase, int porcentaje, decimal esperado)
    {
        Assert.Equal(esperado, AjustePrecio.CalcularCostoFinal(costoBase, porcentaje));
    }

    [Fact]
    public async Task RegistrarPrecioAsync_GuardaCostoBaseYPorcentajeYCalculaCostoFinal()
    {
        await using var db = CrearContexto();
        var (producto, proveedor, _) = await SembrarProductoConDosProveedoresAsync(db);

        var servicio = CrearServicio(db);
        var resultado = await servicio.RegistrarPrecioAsync(
            producto.Id, new RegistrarPrecioDto(proveedor.Id, 100, 20, Hoy, null, "test"));

        Assert.Equal(100, resultado.CostoBase);
        Assert.Equal(20, resultado.PorcentajeAjuste);
        Assert.Equal(120, resultado.Costo);
    }

    [Fact]
    public async Task RegistrarPrecioAsync_RechazaPorcentajeFueraDeRango()
    {
        await using var db = CrearContexto();
        var (producto, proveedor, _) = await SembrarProductoConDosProveedoresAsync(db);

        var servicio = CrearServicio(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.RegistrarPrecioAsync(producto.Id, new RegistrarPrecioDto(proveedor.Id, 100, 101, Hoy, null, "test")));
    }

    private sealed class TiempoFijo(DateTime ahora) : TimeProvider
    {
        private readonly DateTimeOffset _ahora = new(ahora, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _ahora;
    }
}
