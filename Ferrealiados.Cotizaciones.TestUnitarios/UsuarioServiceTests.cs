using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Servicios;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.TestUnitarios;

public class UsuarioServiceTests
{
    private static AppDbContext CrearContexto()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Crear_ConVariosRoles_LosGuardaTodos_YElPrincipalEsElMasAlto()
    {
        using var db = CrearContexto();
        var servicio = new UsuarioService(db);

        var creado = await servicio.CrearAsync(new UsuarioCrearDto("Ana", "ana@x.com", "Clave123!",
            [RolUsuario.Cartera, RolUsuario.Conciliador, RolUsuario.Cartera]));

        Assert.Equal([RolUsuario.Conciliador, RolUsuario.Cartera], creado.Roles);
        Assert.Equal(RolUsuario.Conciliador, (await db.Usuarios.SingleAsync()).Rol);
    }

    [Fact]
    public async Task Actualizar_CambiaLosRoles_SinDuplicar()
    {
        using var db = CrearContexto();
        var servicio = new UsuarioService(db);
        var creado = await servicio.CrearAsync(new UsuarioCrearDto("Ana", "ana@x.com", "Clave123!", [RolUsuario.Cotizador, RolUsuario.Cartera]));

        var actualizado = await servicio.ActualizarAsync(creado.Id,
            new UsuarioActualizarDto("Ana", [RolUsuario.Cartera, RolUsuario.Conciliador], true, null), idUsuarioActual: 999);

        Assert.Equal([RolUsuario.Conciliador, RolUsuario.Cartera], actualizado!.Roles);
        Assert.Equal(2, await db.UsuarioRoles.CountAsync());
    }

    [Fact]
    public async Task Validaciones_SinRoles_OQuitarseElAdminAsiMismo_Rechaza()
    {
        using var db = CrearContexto();
        var servicio = new UsuarioService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.CrearAsync(new UsuarioCrearDto("Ana", "ana@x.com", "x", [])));

        var admin = await servicio.CrearAsync(new UsuarioCrearDto("Admin", "admin@x.com", "x", [RolUsuario.Admin]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.ActualizarAsync(admin.Id,
            new UsuarioActualizarDto("Admin", [RolUsuario.Cartera], true, null), idUsuarioActual: admin.Id));

        // Agregarse otro rol sin quitarse el de Administrador sí se puede.
        var conCartera = await servicio.ActualizarAsync(admin.Id,
            new UsuarioActualizarDto("Admin", [RolUsuario.Admin, RolUsuario.Cartera], true, null), idUsuarioActual: admin.Id);
        Assert.Equal([RolUsuario.Admin, RolUsuario.Cartera], conCartera!.Roles);
    }

    [Fact]
    public void RolesDe_UsuarioSinFilasDeRoles_UsaElRolPrincipal()
    {
        var usuario = new Usuario { Nombre = "Viejo", Email = "v@x.com", PasswordHash = "x", Rol = RolUsuario.Conciliador };

        Assert.Equal([RolUsuario.Conciliador], UsuarioService.RolesDe(usuario));
    }
}
