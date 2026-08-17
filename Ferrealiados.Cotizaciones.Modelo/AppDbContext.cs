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
    }
}
