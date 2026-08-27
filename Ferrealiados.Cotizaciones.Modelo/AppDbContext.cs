using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Modelo;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<ProductoProveedorPrecio> ProductoProveedorPrecios => Set<ProductoProveedorPrecio>();
    public DbSet<ConfiguracionSistema> ConfiguracionSistema => Set<ConfiguracionSistema>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Cotizacion> Cotizaciones => Set<Cotizacion>();
    public DbSet<CotizacionItem> CotizacionItems => Set<CotizacionItem>();

    // Consumida vía "SELECT NEXT VALUE FOR dbo.SecuenciaConsecutivoCotizacion" (IConsecutivoCotizacionProvider)
    // para asignar el número oficial de una cotización solo al emitirla. Atómica ante concurrencia,
    // a diferencia del patrón read-then-write que usa ConfiguracionSistema (no apto para esto).
    public const string SecuenciaConsecutivoCotizacion = "SecuenciaConsecutivoCotizacion";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Producto>(e =>
        {
            e.HasIndex(p => p.Codigo).IsUnique();
        });

        modelBuilder.Entity<Proveedor>(e =>
        {
            e.HasIndex(p => p.Nombre).IsUnique();
        });

        modelBuilder.Entity<ProductoProveedorPrecio>(e =>
        {
            e.HasOne(p => p.Producto)
                .WithMany(p => p.Precios)
                .HasForeignKey(p => p.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Proveedor)
                .WithMany(p => p.Precios)
                .HasForeignKey(p => p.ProveedorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(p => new { p.ProductoId, p.ProveedorId, p.FechaCotizacion });
        });

        modelBuilder.Entity<ConfiguracionSistema>(e =>
        {
            e.HasIndex(c => c.Clave).IsUnique();
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<ConfiguracionSistema>().HasData(new ConfiguracionSistema
        {
            Id = 1,
            Clave = ClavesConfiguracion.MesesVigenciaPrecio,
            Valor = "3",
            Descripcion = "Meses tras los cuales un precio cotizado se considera desactualizado."
        });

        modelBuilder.Entity<Cliente>(e =>
        {
            e.HasIndex(c => c.Nombre).IsUnique();
        });

        modelBuilder.Entity<Cotizacion>(e =>
        {
            e.HasOne(c => c.Cliente)
                .WithMany(cl => cl.Cotizaciones)
                .HasForeignKey(c => c.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Único solo mientras está en construcción: el Codigo es una etiqueta de trabajo
            // temporal (no el identificador oficial, ese es Consecutivo), así que una vez emitida
            // la cotización el mismo texto queda libre para reusarse en un borrador futuro.
            e.HasIndex(c => c.Codigo)
                .IsUnique()
                .HasFilter("[Estado] = 1");

            e.HasIndex(c => c.Consecutivo)
                .IsUnique()
                .HasFilter("[Consecutivo] IS NOT NULL");

            e.HasIndex(c => c.Estado);
        });

        modelBuilder.Entity<CotizacionItem>(e =>
        {
            e.HasOne(i => i.Cotizacion)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.CotizacionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(i => i.ProductoProveedorPrecioId);
        });

        modelBuilder.HasSequence<int>(SecuenciaConsecutivoCotizacion, schema: "dbo")
            .StartsAt(1)
            .IncrementsBy(1);
    }
}
