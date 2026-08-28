using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

public class ClienteService(AppDbContext db) : IClienteService
{
    public async Task<PaginaResultado<ClienteDto>> BuscarAsync(string? texto, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);

        var query = db.Clientes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var patron = $"%{texto.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.Nombre, patron) || (c.Nit != null && EF.Functions.Like(c.Nit, patron)));
        }

        query = query.OrderBy(c => c.Nombre);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(c => new ClienteDto(c.Id, c.Nombre, c.Nit, c.Direccion, c.Telefono, c.Ciudad, c.Contacto, c.Email, c.Activo))
            .ToListAsync(ct);

        return new PaginaResultado<ClienteDto>(items, total, pagina, tamanoPagina);
    }

    public async Task<IReadOnlyList<ClienteDto>> ListarActivosAsync(CancellationToken ct = default)
    {
        return await db.Clientes
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new ClienteDto(c.Id, c.Nombre, c.Nit, c.Direccion, c.Telefono, c.Ciudad, c.Contacto, c.Email, c.Activo))
            .ToListAsync(ct);
    }

    public async Task<ClienteDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Clientes
            .Where(c => c.Id == id)
            .Select(c => new ClienteDto(c.Id, c.Nombre, c.Nit, c.Direccion, c.Telefono, c.Ciudad, c.Contacto, c.Email, c.Activo))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ClienteDto> CrearAsync(ClienteCrearDto dto, CancellationToken ct = default)
    {
        var yaExiste = await db.Clientes.AnyAsync(c => c.Nombre == dto.Nombre, ct);
        if (yaExiste)
            throw new InvalidOperationException($"Ya existe un cliente con el nombre '{dto.Nombre}'.");

        var nit = NormalizarNit(dto.Nit);
        if (nit is not null && await db.Clientes.AnyAsync(c => c.Nit == nit, ct))
            throw new InvalidOperationException($"Ya existe un cliente con el NIT '{nit}'.");

        var cliente = new Cliente
        {
            Nombre = dto.Nombre.Trim(),
            Nit = nit,
            Direccion = dto.Direccion?.Trim(),
            Telefono = dto.Telefono?.Trim(),
            Ciudad = dto.Ciudad?.Trim(),
            Contacto = dto.Contacto?.Trim(),
            Email = dto.Email?.Trim(),
            Activo = true
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(ct);

        return new ClienteDto(cliente.Id, cliente.Nombre, cliente.Nit, cliente.Direccion, cliente.Telefono, cliente.Ciudad, cliente.Contacto, cliente.Email, cliente.Activo);
    }

    public async Task<ClienteDto?> ActualizarAsync(int id, ClienteActualizarDto dto, CancellationToken ct = default)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cliente is null)
            return null;

        var nit = NormalizarNit(dto.Nit);
        if (nit is not null && await db.Clientes.AnyAsync(c => c.Id != id && c.Nit == nit, ct))
            throw new InvalidOperationException($"Ya existe un cliente con el NIT '{nit}'.");

        cliente.Nombre = dto.Nombre.Trim();
        cliente.Nit = nit;
        cliente.Direccion = dto.Direccion?.Trim();
        cliente.Telefono = dto.Telefono?.Trim();
        cliente.Ciudad = dto.Ciudad?.Trim();
        cliente.Contacto = dto.Contacto?.Trim();
        cliente.Email = dto.Email?.Trim();
        cliente.Activo = dto.Activo;

        await db.SaveChangesAsync(ct);

        return new ClienteDto(cliente.Id, cliente.Nombre, cliente.Nit, cliente.Direccion, cliente.Telefono, cliente.Ciudad, cliente.Contacto, cliente.Email, cliente.Activo);
    }

    private static string? NormalizarNit(string? nit)
        => string.IsNullOrWhiteSpace(nit) ? null : nit.Trim();
}
