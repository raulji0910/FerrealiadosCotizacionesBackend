using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.Servicios;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ferrealiados.Cotizaciones.TestUnitarios;

public class ProductoServiceTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 28);

    private static AppDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opciones);
    }

    private static ProductoService CrearServicio(AppDbContext db)
        => new(db, new TiempoFijo(Hoy.ToDateTime(TimeOnly.MinValue)));

    [Fact]
    public async Task EliminarAsync_BorraElProductoYSusPrecios()
    {
        await using var db = CrearContexto();
        var producto = new Producto { Nombre = "Martillo", Activo = true, FechaCreacion = DateTime.UtcNow };
        var proveedor = new Proveedor { Nombre = "Proveedor Uno", Activo = true };
        db.AddRange(producto, proveedor);
        await db.SaveChangesAsync();

        db.ProductoProveedorPrecios.Add(new ProductoProveedorPrecio
        {
            ProductoId = producto.Id, ProveedorId = proveedor.Id, CostoBase = 100, Costo = 100,
            FechaCotizacion = Hoy, FechaRegistro = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db);
        var eliminado = await servicio.EliminarAsync(producto.Id);

        Assert.True(eliminado);
        Assert.Empty(await db.Productos.ToListAsync());
        Assert.Empty(await db.ProductoProveedorPrecios.ToListAsync());
    }

    [Fact]
    public async Task EliminarAsync_DevuelveFalseSiElProductoNoExiste()
    {
        await using var db = CrearContexto();
        var servicio = CrearServicio(db);

        var eliminado = await servicio.EliminarAsync(9999);

        Assert.False(eliminado);
    }

    [Fact]
    public async Task EliminarAsync_RechazaSiElProductoEstaEnAlgunaCotizacion()
    {
        await using var db = CrearContexto();
        var producto = new Producto { Nombre = "Martillo", Activo = true, FechaCreacion = DateTime.UtcNow };
        var proveedor = new Proveedor { Nombre = "Proveedor Uno", Activo = true };
        db.AddRange(producto, proveedor);
        await db.SaveChangesAsync();

        var precio = new ProductoProveedorPrecio
        {
            ProductoId = producto.Id, ProveedorId = proveedor.Id, CostoBase = 100, Costo = 100,
            FechaCotizacion = Hoy, FechaRegistro = DateTime.UtcNow
        };
        db.ProductoProveedorPrecios.Add(precio);
        await db.SaveChangesAsync();

        db.Cotizaciones.Add(new Cotizacion { Codigo = "PJ-1", Estado = EstadoCotizacion.Borrador, FechaCreacion = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var cotizacion = await db.Cotizaciones.SingleAsync();

        db.CotizacionItems.Add(new CotizacionItem
        {
            CotizacionId = cotizacion.Id,
            ProductoProveedorPrecioId = precio.Id,
            ProductoId = producto.Id,
            ProductoNombreSnapshot = producto.Nombre,
            ProveedorId = proveedor.Id,
            ProveedorNombreSnapshot = proveedor.Nombre,
            PrecioUnitario = precio.Costo,
            Cantidad = 1,
            FechaMarcado = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var servicio = CrearServicio(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.EliminarAsync(producto.Id));
        Assert.NotEmpty(await db.Productos.ToListAsync()); // no se borró nada
    }

    private sealed class TiempoFijo(DateTime ahora) : TimeProvider
    {
        private readonly DateTimeOffset _ahora = new(ahora, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _ahora;
    }
}
