using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

public class ProveedorService(AppDbContext db) : IProveedorService
{
    public async Task<PaginaResultado<ProveedorDto>> BuscarAsync(string? texto, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);

        var query = db.Proveedores.AsQueryable();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Nombre, patron) || (p.Nit != null && EF.Functions.Like(p.Nit, patron)));
        }

        query = query.OrderBy(p => p.Nombre);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(p => new ProveedorDto(p.Id, p.Nombre, p.Nit, p.Direccion, p.Telefono, p.Ciudad, p.Activo))
            .ToListAsync(ct);

        return new PaginaResultado<ProveedorDto>(items, total, pagina, tamanoPagina);
    }

    public async Task<IReadOnlyList<ProveedorDto>> ListarActivosAsync(CancellationToken ct = default)
    {
        return await db.Proveedores
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new ProveedorDto(p.Id, p.Nombre, p.Nit, p.Direccion, p.Telefono, p.Ciudad, p.Activo))
            .ToListAsync(ct);
    }

    public async Task<ProveedorDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Proveedores
            .Where(p => p.Id == id)
            .Select(p => new ProveedorDto(p.Id, p.Nombre, p.Nit, p.Direccion, p.Telefono, p.Ciudad, p.Activo))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ProveedorDto> CrearAsync(ProveedorCrearDto dto, CancellationToken ct = default)
    {
        var yaExiste = await db.Proveedores.AnyAsync(p => p.Nombre == dto.Nombre, ct);
        if (yaExiste)
            throw new InvalidOperationException($"Ya existe un proveedor con el nombre '{dto.Nombre}'.");

        var nit = NormalizarNit(dto.Nit);
        if (nit is not null && await db.Proveedores.AnyAsync(p => p.Nit == nit, ct))
            throw new InvalidOperationException($"Ya existe un proveedor con el NIT '{nit}'.");

        var proveedor = new Proveedor
        {
            Nombre = dto.Nombre.Trim(),
            Nit = nit,
            Direccion = dto.Direccion?.Trim(),
            Telefono = dto.Telefono?.Trim(),
            Ciudad = dto.Ciudad?.Trim(),
            Activo = true
        };

        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync(ct);

        return new ProveedorDto(proveedor.Id, proveedor.Nombre, proveedor.Nit, proveedor.Direccion, proveedor.Telefono, proveedor.Ciudad, proveedor.Activo);
    }

    public async Task<ProveedorDto?> ActualizarAsync(int id, ProveedorActualizarDto dto, CancellationToken ct = default)
    {
        var proveedor = await db.Proveedores.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proveedor is null)
            return null;

        var nit = NormalizarNit(dto.Nit);
        if (nit is not null && await db.Proveedores.AnyAsync(p => p.Id != id && p.Nit == nit, ct))
            throw new InvalidOperationException($"Ya existe un proveedor con el NIT '{nit}'.");

        proveedor.Nombre = dto.Nombre.Trim();
        proveedor.Nit = nit;
        proveedor.Direccion = dto.Direccion?.Trim();
        proveedor.Telefono = dto.Telefono?.Trim();
        proveedor.Ciudad = dto.Ciudad?.Trim();
        proveedor.Activo = dto.Activo;

        await db.SaveChangesAsync(ct);

        return new ProveedorDto(proveedor.Id, proveedor.Nombre, proveedor.Nit, proveedor.Direccion, proveedor.Telefono, proveedor.Ciudad, proveedor.Activo);
    }

    private static string? NormalizarNit(string? nit)
        => string.IsNullOrWhiteSpace(nit) ? null : nit.Trim();
}
