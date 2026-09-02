using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Ferrealiados.Cotizaciones.Negocio.Servicios;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Ferrealiados.Cotizaciones.TestUnitarios;

public class CotizacionServiceTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 27);

    private static AppDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opciones);
    }

    private static CotizacionService CrearServicio(AppDbContext db, IConsecutivoCotizacionProvider? consecutivoProvider = null)
    {
        var consecutivoMock = consecutivoProvider ?? Mock.Of<IConsecutivoCotizacionProvider>();
        var pdfBuilderMock = Mock.Of<ICotizacionPdfBuilder>();
        var tiempoFijo = new TiempoFijo(Hoy.ToDateTime(TimeOnly.MinValue));

        return new CotizacionService(db, consecutivoMock, pdfBuilderMock, tiempoFijo);
    }

    private static async Task<(Producto producto, Proveedor proveedor, ProductoProveedorPrecio precio)> SembrarProductoConPrecioAsync(AppDbContext db, decimal costo = 120)
    {
        var producto = new Producto { Codigo = "P-1", Nombre = "Martillo", Activo = true, FechaCreacion = DateTime.UtcNow };
        var proveedor = new Proveedor { Nombre = "Proveedor Uno", Activo = true };
        db.AddRange(producto, proveedor);
        await db.SaveChangesAsync();

        var precio = new ProductoProveedorPrecio
        {
            ProductoId = producto.Id,
            ProveedorId = proveedor.Id,
            CostoBase = 100,
            Costo = costo,
            PorcentajeAjuste = 20,
            FechaCotizacion = Hoy,
            FechaRegistro = DateTime.UtcNow
        };
        db.ProductoProveedorPrecios.Add(precio);
        await db.SaveChangesAsync();

        return (producto, proveedor, precio);
    }

    private static async Task<Cliente> SembrarClienteAsync(AppDbContext db, bool activo = true)
    {
        var cliente = new Cliente { Nombre = "Cliente Uno", Nit = "900123456", Activo = activo };
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return cliente;
    }

    [Fact]
    public async Task MarcarPrecioAsync_CreaBorradorNuevoSiNoExisteElCodigo()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);

        var servicio = CrearServicio(db);
        var item = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "pj-5087", 3), "cotizador1");

        Assert.Equal(3, item.Cantidad);
        Assert.Equal(precio.Costo, item.PrecioUnitario);

        var cotizacion = await db.Cotizaciones.SingleAsync();
        Assert.Equal("PJ-5087", cotizacion.Codigo); // normalizado a mayúsculas
        Assert.Equal(EstadoCotizacion.Borrador, cotizacion.Estado);
        Assert.Null(cotizacion.Consecutivo);
    }

    [Fact]
    public async Task MarcarPrecioAsync_ReutilizaBorradorExistenteConMismoCodigoYSumaCantidad()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);

        var servicio = CrearServicio(db);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 2), "cotizador1");
        var segundo = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 5), "cotizador1");

        Assert.Single(await db.Cotizaciones.ToListAsync());
        Assert.Single(await db.CotizacionItems.ToListAsync());
        Assert.Equal(7, segundo.Cantidad); // 2 + 5, mismo ítem
    }

    [Fact]
    public async Task MarcarPrecioAsync_PermiteVariasMarcasSimultaneasDelMismoPrecioEnCodigosDistintos()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);

        var servicio = CrearServicio(db);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-9999", 1), "cotizador1");

        Assert.Equal(2, await db.Cotizaciones.CountAsync());
        Assert.Equal(2, await db.CotizacionItems.CountAsync());
    }

    [Fact]
    public async Task MarcarPrecioAsync_ElSnapshotDePrecioNoCambiaSiElPrecioSeEditaDespues()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db, costo: 120);

        var servicio = CrearServicio(db);
        var item = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        Assert.Equal(120, item.PrecioUnitario);

        precio.Costo = 500;
        await db.SaveChangesAsync();

        var itemRecargado = await db.CotizacionItems.SingleAsync();
        Assert.Equal(120, itemRecargado.PrecioUnitario); // sigue congelado, no sigue al precio vivo
    }

    [Fact]
    public async Task MarcarPrecioAsync_RechazaCantidadMenorAUno()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var servicio = CrearServicio(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 0), "cotizador1"));
    }

    [Fact]
    public async Task QuitarItemAsync_EliminaLaCotizacionSiQuedaSinItems()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var servicio = CrearServicio(db);
        var item = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");

        var eliminado = await servicio.QuitarItemAsync(item.Id);

        Assert.True(eliminado);
        Assert.Empty(await db.CotizacionItems.ToListAsync());
        Assert.Empty(await db.Cotizaciones.ToListAsync());
    }

    [Fact]
    public async Task QuitarItemAsync_ConservaLaCotizacionSiQuedanOtrosItems()
    {
        await using var db = CrearContexto();
        var (producto, proveedor, precio1) = await SembrarProductoConPrecioAsync(db);
        var otroProveedor = new Proveedor { Nombre = "Proveedor Dos", Activo = true };
        db.Proveedores.Add(otroProveedor);
        await db.SaveChangesAsync();
        var precio2 = new ProductoProveedorPrecio
        {
            ProductoId = producto.Id, ProveedorId = otroProveedor.Id, CostoBase = 90, Costo = 90,
            FechaCotizacion = Hoy, FechaRegistro = DateTime.UtcNow
        };
        db.ProductoProveedorPrecios.Add(precio2);
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db);
        var item1 = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio1.Id, "PJ-5087", 1), "cotizador1");
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio2.Id, "PJ-5087", 1), "cotizador1");

        var eliminado = await servicio.QuitarItemAsync(item1.Id);

        Assert.True(eliminado);
        Assert.Single(await db.CotizacionItems.ToListAsync());
        Assert.Single(await db.Cotizaciones.ToListAsync());
    }

    [Fact]
    public async Task QuitarItemAsync_RechazaSiLaCotizacionYaFueEmitida()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var cliente = await SembrarClienteAsync(db);
        var consecutivoMock = new Mock<IConsecutivoCotizacionProvider>();
        consecutivoMock.Setup(p => p.ObtenerSiguienteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var servicio = CrearServicio(db, consecutivoMock.Object);
        var item = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();
        await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.QuitarItemAsync(item.Id));
    }

    [Fact]
    public async Task ActualizarCantidadItemAsync_RecalculaSubtotalEnElDto()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db, costo: 50);
        var servicio = CrearServicio(db);
        var item = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");

        var actualizado = await servicio.ActualizarCantidadItemAsync(item.Id, new ActualizarCantidadItemDto(4));

        Assert.NotNull(actualizado);
        Assert.Equal(4, actualizado!.Cantidad);
        Assert.Equal(200, actualizado.Subtotal); // 50 * 4
    }

    [Fact]
    public async Task ActualizarPrecioItemAsync_CambiaSoloElItemSinTocarElPrecioDelCatalogo()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db, costo: 100);
        var servicio = CrearServicio(db);
        var item = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 3), "cotizador1");

        var actualizado = await servicio.ActualizarPrecioItemAsync(item.Id, new ActualizarPrecioItemDto(80));

        Assert.NotNull(actualizado);
        Assert.Equal(80, actualizado!.PrecioUnitario);
        Assert.Equal(240, actualizado.Subtotal); // 80 * 3

        var precioRecargado = await db.ProductoProveedorPrecios.FindAsync(precio.Id);
        Assert.Equal(100, precioRecargado!.Costo); // el catálogo no se tocó
    }

    [Fact]
    public async Task ActualizarPrecioItemAsync_RechazaValorNegativo()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var servicio = CrearServicio(db);
        var item = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.ActualizarPrecioItemAsync(item.Id, new ActualizarPrecioItemDto(-10)));
    }

    [Fact]
    public async Task ActualizarPrecioItemAsync_RechazaSiLaCotizacionYaFueEmitida()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var cliente = await SembrarClienteAsync(db);
        var consecutivoMock = new Mock<IConsecutivoCotizacionProvider>();
        consecutivoMock.Setup(p => p.ObtenerSiguienteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var servicio = CrearServicio(db, consecutivoMock.Object);
        var item = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();
        await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.ActualizarPrecioItemAsync(item.Id, new ActualizarPrecioItemDto(50)));
    }

    [Fact]
    public async Task ActualizarIvaItemAsync_CambiaLaTarifaDelItemYSeReflejaEnElDesglose()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db, costo: 100);
        var cliente = await SembrarClienteAsync(db);
        var servicio = CrearServicio(db);
        var item = await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        Assert.Null(item.IvaSnapshot); // no traía IVA del catálogo

        var actualizado = await servicio.ActualizarIvaItemAsync(item.Id, new ActualizarIvaItemDto(5));
        Assert.Equal(5, actualizado!.IvaSnapshot);

        var cotizacion = await db.Cotizaciones.SingleAsync();
        var detalle = await servicio.ObtenerPorIdAsync(cotizacion.Id);
        var tramo = Assert.Single(detalle!.IvaDesglose);
        Assert.Equal(5, tramo.Tarifa);
        Assert.Equal(5, tramo.Valor); // 100 * 5%
    }

    [Fact]
    public async Task EmitirAsync_AsignaConsecutivoClienteYFechaYCambiaEstado()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var cliente = await SembrarClienteAsync(db);

        var consecutivoMock = new Mock<IConsecutivoCotizacionProvider>();
        consecutivoMock.Setup(p => p.ObtenerSiguienteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(42);

        var servicio = CrearServicio(db, consecutivoMock.Object);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();

        var emitida = await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, "Contado", "Entrega en obra", null));

        Assert.NotNull(emitida);
        Assert.Equal(EstadoCotizacion.Emitida, emitida!.Estado);
        Assert.Equal(42, emitida.Consecutivo);
        Assert.Equal(cliente.Id, emitida.ClienteId);
        Assert.Equal(cliente.Nombre, emitida.ClienteNombre);
        Assert.Equal(Hoy, emitida.FechaEmision);
        Assert.Equal("Contado", emitida.FormaPago);
        Assert.Equal("COT-FA-00042", emitida.ConsecutivoFormateado);
        consecutivoMock.Verify(p => p.ObtenerSiguienteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmitirAsync_DesglosaIvaPorTarifaCuandoLosItemsTienenTarifasDistintas()
    {
        await using var db = CrearContexto();
        var (producto, proveedor, precio19) = await SembrarProductoConPrecioAsync(db, costo: 100);
        precio19.Iva = 19;
        var proveedor2 = new Proveedor { Nombre = "Proveedor Dos", Activo = true };
        db.Proveedores.Add(proveedor2);
        await db.SaveChangesAsync();
        var precio5 = new ProductoProveedorPrecio
        {
            ProductoId = producto.Id, ProveedorId = proveedor2.Id, CostoBase = 100, Costo = 100, Iva = 5,
            FechaCotizacion = Hoy, FechaRegistro = DateTime.UtcNow
        };
        db.ProductoProveedorPrecios.Add(precio5);
        await db.SaveChangesAsync();

        var cliente = await SembrarClienteAsync(db);
        var servicio = CrearServicio(db);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio19.Id, "PJ-5087", 1), "cotizador1"); // subtotal 100 al 19%
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio5.Id, "PJ-5087", 1), "cotizador1");  // subtotal 100 al 5%
        var cotizacion = await db.Cotizaciones.SingleAsync();

        var emitida = await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, null));

        Assert.NotNull(emitida);
        Assert.Equal(200, emitida!.Subtotal); // sin descuento
        Assert.Equal(2, emitida.IvaDesglose.Count);
        var tramo19 = emitida.IvaDesglose.Single(d => d.Tarifa == 19);
        var tramo5 = emitida.IvaDesglose.Single(d => d.Tarifa == 5);
        Assert.Equal(100, tramo19.Base);
        Assert.Equal(19, tramo19.Valor);
        Assert.Equal(100, tramo5.Base);
        Assert.Equal(5, tramo5.Valor);
        Assert.Equal(224, emitida.TotalGeneral); // 200 + 19 + 5
    }

    [Fact]
    public async Task EmitirAsync_ReparteElDescuentoProporcionalmenteEntreTarifasDeIva()
    {
        await using var db = CrearContexto();
        var (producto, proveedor, precio19) = await SembrarProductoConPrecioAsync(db, costo: 100);
        precio19.Iva = 19;
        var proveedor2 = new Proveedor { Nombre = "Proveedor Dos", Activo = true };
        db.Proveedores.Add(proveedor2);
        await db.SaveChangesAsync();
        var precioSinIva = new ProductoProveedorPrecio
        {
            ProductoId = producto.Id, ProveedorId = proveedor2.Id, CostoBase = 100, Costo = 100, Iva = null,
            FechaCotizacion = Hoy, FechaRegistro = DateTime.UtcNow
        };
        db.ProductoProveedorPrecios.Add(precioSinIva);
        await db.SaveChangesAsync();

        var cliente = await SembrarClienteAsync(db);
        var servicio = CrearServicio(db);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio19.Id, "PJ-5087", 1), "cotizador1");     // 100, 19%
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precioSinIva.Id, "PJ-5087", 1), "cotizador1"); // 100, sin IVA
        var cotizacion = await db.Cotizaciones.SingleAsync();

        // Descuento de 20 sobre subtotal 200 -> se reparte 50/50 entre los dos tramos (10 cada uno).
        var emitida = await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, 20));

        Assert.NotNull(emitida);
        Assert.Equal(20, emitida!.Descuento);
        Assert.Equal(180, emitida.Subtotal); // 200 - 20
        var tramo19 = Assert.Single(emitida.IvaDesglose);
        Assert.Equal(90, tramo19.Base); // (100 - 10) del tramo con IVA
        Assert.Equal(17.1m, tramo19.Valor); // 90 * 19%
        Assert.Equal(197.1m, emitida.TotalGeneral); // 180 + 17.1
    }

    [Fact]
    public async Task EmitirAsync_RechazaDescuentoMayorAlSubtotal()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db, costo: 100);
        var cliente = await SembrarClienteAsync(db);
        var servicio = CrearServicio(db);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, 150)));
    }

    [Fact]
    public async Task ReabrirAsync_VuelveABorradorYLimpiaDatosDelCliente()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var cliente = await SembrarClienteAsync(db);
        var consecutivoMock = new Mock<IConsecutivoCotizacionProvider>();
        consecutivoMock.Setup(p => p.ObtenerSiguienteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var servicio = CrearServicio(db, consecutivoMock.Object);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();
        await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, "Contado", "Nota", null));

        var reabierta = await servicio.ReabrirAsync(cotizacion.Id);

        Assert.NotNull(reabierta);
        Assert.Equal(EstadoCotizacion.Borrador, reabierta!.Estado);
        Assert.Equal(7, reabierta.Consecutivo); // conserva el número oficial
        Assert.Null(reabierta.ClienteId);
        Assert.Null(reabierta.ClienteNombre);
        Assert.Null(reabierta.FormaPago);
        Assert.Null(reabierta.Nota);
        Assert.Equal(0, reabierta.Descuento);
        Assert.Null(reabierta.FechaEmision);
    }

    [Fact]
    public async Task ReabrirAsync_RechazaSiLaCotizacionEsBorrador()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var servicio = CrearServicio(db);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.ReabrirAsync(cotizacion.Id));
    }

    [Fact]
    public async Task ReabrirAsync_RechazaSiOtroBorradorYaUsaElMismoCodigo()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var cliente = await SembrarClienteAsync(db);
        var consecutivoMock = new Mock<IConsecutivoCotizacionProvider>();
        consecutivoMock.Setup(p => p.ObtenerSiguienteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var servicio = CrearServicio(db, consecutivoMock.Object);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();
        await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, null));

        // Alguien más vuelve a usar "PJ-5087" para un borrador nuevo, ahora que quedó libre.
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador2");

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.ReabrirAsync(cotizacion.Id));
    }

    [Fact]
    public async Task EmitirAsync_ReutilizaElConsecutivoAlReemitirUnaCotizacionReabierta()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var cliente = await SembrarClienteAsync(db);
        var consecutivoMock = new Mock<IConsecutivoCotizacionProvider>();
        consecutivoMock.Setup(p => p.ObtenerSiguienteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var servicio = CrearServicio(db, consecutivoMock.Object);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();
        await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, null));
        await servicio.ReabrirAsync(cotizacion.Id);

        var reemitida = await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, null));

        Assert.NotNull(reemitida);
        Assert.Equal(7, reemitida!.Consecutivo);
        consecutivoMock.Verify(p => p.ObtenerSiguienteAsync(It.IsAny<CancellationToken>()), Times.Once); // solo se consumió una vez
    }

    [Fact]
    public async Task EmitirAsync_RechazaSiNoTieneItems()
    {
        await using var db = CrearContexto();
        var cliente = await SembrarClienteAsync(db);
        db.Cotizaciones.Add(new Cotizacion { Codigo = "VACIO", Estado = EstadoCotizacion.Borrador, FechaCreacion = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var cotizacion = await db.Cotizaciones.SingleAsync();

        var servicio = CrearServicio(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, null)));
    }

    [Fact]
    public async Task EmitirAsync_RechazaSiYaFueEmitida()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var cliente = await SembrarClienteAsync(db);
        var consecutivoMock = new Mock<IConsecutivoCotizacionProvider>();
        consecutivoMock.Setup(p => p.ObtenerSiguienteAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var servicio = CrearServicio(db, consecutivoMock.Object);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();
        await servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(cliente.Id, null, null, null)));
    }

    [Fact]
    public async Task EmitirAsync_RechazaSiElClienteNoExisteOEstaInactivo()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var clienteInactivo = await SembrarClienteAsync(db, activo: false);

        var servicio = CrearServicio(db);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(clienteInactivo.Id, null, null, null)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.EmitirAsync(cotizacion.Id, new EmitirCotizacionDto(9999, null, null, null)));
    }

    [Fact]
    public async Task GenerarPdfAsync_RechazaSiLaCotizacionEsBorrador()
    {
        await using var db = CrearContexto();
        var (_, _, precio) = await SembrarProductoConPrecioAsync(db);
        var servicio = CrearServicio(db);
        await servicio.MarcarPrecioAsync(new MarcarPrecioDto(precio.Id, "PJ-5087", 1), "cotizador1");
        var cotizacion = await db.Cotizaciones.SingleAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.GenerarPdfAsync(cotizacion.Id));
    }

    private sealed class TiempoFijo(DateTime ahora) : TimeProvider
    {
        private readonly DateTimeOffset _ahora = new(ahora, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _ahora;
    }
}
