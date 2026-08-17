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
        return await db.Usuarios
            .OrderBy(u => u.Nombre)
            .Select(u => new UsuarioDto(u.Id, u.Nombre, u.Email, u.Rol, u.Activo))
            .ToListAsync(ct);
    }

    public async Task<UsuarioDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Usuarios
            .Where(u => u.Id == id)
            .Select(u => new UsuarioDto(u.Id, u.Nombre, u.Email, u.Rol, u.Activo))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UsuarioDto> CrearAsync(UsuarioCrearDto dto, CancellationToken ct = default)
    {
        var yaExiste = await db.Usuarios.AnyAsync(u => u.Email == dto.Email, ct);
        if (yaExiste)
            throw new InvalidOperationException($"Ya existe un usuario con el correo '{dto.Email}'.");

        var usuario = new Usuario
        {
            Nombre = dto.Nombre.Trim(),
            Email = dto.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Rol = dto.Rol,
            Activo = true
        };

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);

        return new UsuarioDto(usuario.Id, usuario.Nombre, usuario.Email, usuario.Rol, usuario.Activo);
    }

    public async Task<UsuarioDto?> ActualizarAsync(int id, UsuarioActualizarDto dto, int idUsuarioActual, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null)
            return null;

        if (id == idUsuarioActual && (!dto.Activo || dto.Rol != RolUsuario.Admin))
            throw new InvalidOperationException("No puedes desactivar tu propio usuario ni quitarte el rol de Administrador.");

        usuario.Nombre = dto.Nombre.Trim();
        usuario.Rol = dto.Rol;
        usuario.Activo = dto.Activo;

        if (!string.IsNullOrWhiteSpace(dto.NuevaPassword))
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);

        await db.SaveChangesAsync(ct);

        return new UsuarioDto(usuario.Id, usuario.Nombre, usuario.Email, usuario.Rol, usuario.Activo);
    }
}
