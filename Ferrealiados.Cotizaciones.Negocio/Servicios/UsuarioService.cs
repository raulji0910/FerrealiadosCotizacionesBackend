using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

public class UsuarioService(AppDbContext db) : IUsuarioService
{
    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct = default)
    {
        var usuarios = await db.Usuarios.AsNoTracking()
            .Include(u => u.Roles)
            .OrderBy(u => u.Nombre)
            .ToListAsync(ct);
        return usuarios.Select(Mapear).ToList();
    }

    public async Task<UsuarioDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.AsNoTracking().Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct);
        return usuario is null ? null : Mapear(usuario);
    }

    public async Task<UsuarioDto> CrearAsync(UsuarioCrearDto dto, CancellationToken ct = default)
    {
        var roles = Validar(dto.Roles);

        var yaExiste = await db.Usuarios.AnyAsync(u => u.Email == dto.Email, ct);
        if (yaExiste)
            throw new InvalidOperationException($"Ya existe un usuario con el correo '{dto.Email}'.");

        var usuario = new Usuario
        {
            Nombre = dto.Nombre.Trim(),
            Email = dto.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Rol = RolPrincipal(roles),
            Activo = true,
            Roles = roles.Select(r => new UsuarioRol { Rol = r }).ToList()
        };

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);

        return Mapear(usuario);
    }

    public async Task<UsuarioDto?> ActualizarAsync(int id, UsuarioActualizarDto dto, int idUsuarioActual, CancellationToken ct = default)
    {
        var roles = Validar(dto.Roles);

        var usuario = await db.Usuarios.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null)
            return null;

        if (id == idUsuarioActual && (!dto.Activo || !roles.Contains(RolUsuario.Admin)))
            throw new InvalidOperationException("No puedes desactivar tu propio usuario ni quitarte el rol de Administrador.");

        usuario.Nombre = dto.Nombre.Trim();
        usuario.Activo = dto.Activo;
        usuario.Rol = RolPrincipal(roles);

        // Solo se agregan/quitan los que cambiaron (la llave es usuario + rol).
        foreach (var quitado in usuario.Roles.Where(r => !roles.Contains(r.Rol)).ToList())
            usuario.Roles.Remove(quitado);
        foreach (var nuevo in roles.Where(r => usuario.Roles.All(x => x.Rol != r)))
            usuario.Roles.Add(new UsuarioRol { Rol = nuevo });

        if (!string.IsNullOrWhiteSpace(dto.NuevaPassword))
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);

        await db.SaveChangesAsync(ct);

        return Mapear(usuario);
    }

    // Roles efectivos de un usuario. Si por alguna razón no tiene filas en UsuarioRoles (datos
    // anteriores al multirol), se usa su rol principal.
    public static IReadOnlyList<RolUsuario> RolesDe(Usuario usuario)
        => usuario.Roles.Count > 0
            ? usuario.Roles.Select(r => r.Rol).Distinct().OrderBy(r => r).ToList()
            : [usuario.Rol];

    private static List<RolUsuario> Validar(IReadOnlyList<RolUsuario>? roles)
    {
        var lista = (roles ?? []).Distinct().ToList();
        if (lista.Count == 0)
            throw new InvalidOperationException("El usuario debe tener al menos un rol.");
        if (lista.Any(r => !Enum.IsDefined(r)))
            throw new InvalidOperationException("Hay un rol que no existe.");
        return lista;
    }

    // Admin primero; si no, el de menor número (Cotizador, Conciliador, Cartera).
    private static RolUsuario RolPrincipal(IReadOnlyCollection<RolUsuario> roles)
        => roles.Contains(RolUsuario.Admin) ? RolUsuario.Admin : roles.Min();

    private static UsuarioDto Mapear(Usuario u) => new(u.Id, u.Nombre, u.Email, RolesDe(u), u.Activo);
}
