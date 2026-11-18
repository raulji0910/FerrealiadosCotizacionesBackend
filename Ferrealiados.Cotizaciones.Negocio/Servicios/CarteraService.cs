using System.Globalization;
using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.Cartera;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

/// <summary>
/// Cartera de clientes de Ferrealiados: guarda lo que sube el Sincronizador desde World Office
/// (Vista_CuentasPorCobrar_Detallada, cuenta 1305), arma el estado de cuenta por cliente y lo envía
/// por correo a los contactos de cobro (que se manejan aquí, no en WO: los correos de WO son los de
/// facturación). Las remisiones con saldo no se cobran: son las "remisiones sin factura".
/// </summary>
public class CarteraService(AppDbContext db, IEnviadorCorreo enviador, TimeProvider reloj) : ICarteraService
{
    // Colombia no tiene horario de verano: la fecha local es siempre UTC-5.
    private static readonly TimeSpan HoraColombia = TimeSpan.FromHours(-5);

    // ---------------- Sincronización desde World Office ----------------

    public async Task<SincronizacionCarteraResultadoDto> SincronizarAsync(SincronizacionCarteraEntradaDto entrada, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;

        // La misma partida podría venir repetida (p. ej. dos renglones de la vista): se suman.
        var nuevas = entrada.Partidas
            .Select(p => p with
            {
                CodigoCuenta = p.CodigoCuenta.Trim(),
                Nit = NormalizarNit(p.Nit),
                NombreCliente = NormalizarNombre(p.NombreCliente),
                TipoDocumento = p.TipoDocumento.Trim(),
                Prefijo = p.Prefijo.Trim()
            })
            .Where(p => p.Nit.Length > 0)
            .GroupBy(p => (p.IdDocumentoWorldOffice, p.CodigoCuenta))
            .ToDictionary(g => g.Key, g => g.First() with { Saldo = Math.Round(g.Sum(x => x.Saldo), 2) });

        var existentes = await db.PartidasCartera.ToListAsync(ct);
        int creadas = 0, actualizadas = 0, eliminadas = 0;

        foreach (var partida in existentes)
        {
            if (!nuevas.Remove((partida.IdDocumentoWorldOffice, partida.CodigoCuenta), out var dto))
            {
                db.PartidasCartera.Remove(partida);
                eliminadas++;
                continue;
            }

            Copiar(dto, partida);
            if (db.Entry(partida).State == EntityState.Modified)
            {
                partida.FechaSincronizacion = ahora;
                actualizadas++;
            }
        }

        foreach (var dto in nuevas.Values)
        {
            var partida = new PartidaCartera
            {
                IdDocumentoWorldOffice = dto.IdDocumentoWorldOffice,
                CodigoCuenta = dto.CodigoCuenta,
                Nit = "",
                NombreCliente = "",
                TipoDocumento = "",
                Prefijo = "",
                FechaSincronizacion = ahora
            };
            Copiar(dto, partida);
            db.PartidasCartera.Add(partida);
            creadas++;
        }

        var clientes = await SincronizarClientesAsync(entrada, ct);
        var remisiones = entrada.Remisiones is null ? 0 : await SincronizarRemisionesAsync(entrada.Remisiones, ahora, ct);
        await GuardarUltimaSincronizacionAsync(ahora, ct);
        await db.SaveChangesAsync(ct);

        return new SincronizacionCarteraResultadoDto(creadas, actualizadas, eliminadas, clientes, remisiones);
    }

    // Deja la tabla igual a lo que manda WO: solo remisiones con algo por facturar (más de $1, lo
    // demás son redondeos). Devuelve cuántas quedaron pendientes.
    private async Task<int> SincronizarRemisionesAsync(IReadOnlyList<RemisionPendienteEntradaDto> remisiones, DateTime ahora, CancellationToken ct)
    {
        var nuevas = remisiones
            .Where(r => r.Valor - r.Facturado - r.Otros > 1m && NormalizarNit(r.Nit).Length > 0)
            .GroupBy(r => r.IdAsientoContable)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var existente in await db.RemisionesPendientesWorldOffice.ToListAsync(ct))
        {
            if (!nuevas.Remove(existente.IdAsientoContable, out var dto))
            {
                db.RemisionesPendientesWorldOffice.Remove(existente);
                continue;
            }
            Copiar(dto, existente);
            if (db.Entry(existente).State == EntityState.Modified)
                existente.FechaSincronizacion = ahora;
        }

        foreach (var dto in nuevas.Values)
        {
            var remision = new RemisionPendienteWorldOffice { Nit = "", NombreCliente = "", FechaSincronizacion = ahora };
            Copiar(dto, remision);
            db.RemisionesPendientesWorldOffice.Add(remision);
        }

        return remisiones.Count(r => r.Valor - r.Facturado - r.Otros > 1m && NormalizarNit(r.Nit).Length > 0);
    }

    private static void Copiar(RemisionPendienteEntradaDto dto, RemisionPendienteWorldOffice remision)
    {
        remision.IdAsientoContable = dto.IdAsientoContable;
        remision.Numero = dto.Numero;
        remision.Fecha = dto.Fecha;
        remision.Nit = Truncar(NormalizarNit(dto.Nit), 20);
        remision.NombreCliente = Truncar(NormalizarNombre(dto.NombreCliente), 300);
        remision.Valor = Math.Round(dto.Valor, 2);
        remision.Facturado = Math.Round(dto.Facturado, 2);
        remision.Pagado = Math.Round(dto.Pagado, 2);
        remision.Otros = Math.Round(dto.Otros, 2);
    }

    private static void Copiar(PartidaCarteraEntradaDto dto, PartidaCartera partida)
    {
        partida.Nit = Truncar(dto.Nit, 20);
        partida.NombreCliente = Truncar(dto.NombreCliente, 300);
        partida.TipoDocumento = Truncar(dto.TipoDocumento, 100);
        partida.Prefijo = Truncar(dto.Prefijo, 20);
        partida.Numero = dto.Numero;
        partida.Fecha = dto.Fecha;
        partida.Vencimiento = dto.Vencimiento;
        partida.Saldo = dto.Saldo;
        partida.EsRemision = EsRemision(dto.Prefijo, dto.TipoDocumento);
    }

    // REM, o la "Devolución de Remisión" (que en WO puede venir con otro prefijo).
    public static bool EsRemision(string prefijo, string tipoDocumento)
        => prefijo.Trim().Equals("REM", StringComparison.OrdinalIgnoreCase) ||
           CultureInfo.InvariantCulture.CompareInfo.IndexOf(tipoDocumento, "remision",
               CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    // Crea los clientes que aparecen por primera vez y refresca nombre y correos de WO. Los datos de
    // cobro (los que se manejan en la app) no se tocan.
    private async Task<int> SincronizarClientesAsync(SincronizacionCarteraEntradaDto entrada, CancellationToken ct)
    {
        var nombres = new Dictionary<string, string>();
        foreach (var p in entrada.Partidas)
        {
            var nit = NormalizarNit(p.Nit);
            if (nit.Length > 0) nombres[nit] = NormalizarNombre(p.NombreCliente);
        }

        var correos = new Dictionary<string, string?>();
        foreach (var c in entrada.CorreosClientes ?? [])
        {
            var nit = NormalizarNit(c.Nit);
            if (nit.Length == 0) continue;
            if (!nombres.ContainsKey(nit) && !string.IsNullOrWhiteSpace(c.Nombre)) nombres[nit] = NormalizarNombre(c.Nombre);
            var lista = CorreoCarteraBuilder.SepararCorreos(string.Join(';', c.Correos));
            correos[nit] = lista.Count > 0 ? Truncar(string.Join("; ", lista), 600) : null;
        }

        var existentes = await db.ClientesCartera.ToDictionaryAsync(c => c.Nit, ct);
        var tocados = 0;
        foreach (var (nit, nombre) in nombres)
        {
            if (!existentes.TryGetValue(nit, out var cliente))
            {
                cliente = new ClienteCartera { Nit = nit, Nombre = Truncar(nombre, 300) };
                db.ClientesCartera.Add(cliente);
                existentes[nit] = cliente;
                tocados++;
            }
            else if (nombre.Length > 0 && cliente.Nombre != Truncar(nombre, 300))
            {
                cliente.Nombre = Truncar(nombre, 300);
                tocados++;
            }

            // Solo si esta corrida trajo los correos de WO (si la consulta falló, se dejan los anteriores).
            if (entrada.CorreosClientes is not null && correos.TryGetValue(nit, out var deWo) && cliente.CorreosWorldOffice != deWo)
                cliente.CorreosWorldOffice = deWo;
        }
        return tocados;
    }

    private async Task GuardarUltimaSincronizacionAsync(DateTime ahora, CancellationToken ct)
    {
        var valor = ahora.ToString("O", CultureInfo.InvariantCulture);
        var config = await db.ConfiguracionSistema.FirstOrDefaultAsync(c => c.Clave == ClavesConfiguracion.UltimaSincronizacionCartera, ct);
        if (config is null)
        {
            db.ConfiguracionSistema.Add(new ConfiguracionSistema
            {
                Clave = ClavesConfiguracion.UltimaSincronizacionCartera,
                Valor = valor,
                Descripcion = "Última vez que el Sincronizador subió la cartera de clientes de World Office (UTC)."
            });
        }
        else
        {
            config.Valor = valor;
        }
    }

    private async Task<DateTime?> LeerUltimaSincronizacionAsync(CancellationToken ct)
    {
        var config = await db.ConfiguracionSistema.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Clave == ClavesConfiguracion.UltimaSincronizacionCartera, ct);
        // Se devuelve sin zona (como las demás fechas UTC que guarda EF): la pantalla le agrega la "Z".
        return config is not null && DateTime.TryParse(config.Valor, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var fecha)
            ? DateTime.SpecifyKind(fecha, DateTimeKind.Unspecified)
            : null;
    }

    // ---------------- Pantalla ----------------

    public async Task<CarteraDto> ObtenerAsync(CancellationToken ct = default)
    {
        var hoy = Hoy();
        var config = await LeerConfiguracionAsync(ct);
        var partidas = await db.PartidasCartera.AsNoTracking().Where(p => !p.EsRemision).ToListAsync(ct);
        var clientes = await db.ClientesCartera.AsNoTracking().ToDictionaryAsync(c => c.Nit, ct);
        var ultimosEnvios = (await db.EnviosCartera.AsNoTracking()
                .OrderByDescending(e => e.Fecha)
                .Select(e => new { e.Nit, e.Fecha, e.Usuario, e.Exitoso, e.Error })
                .ToListAsync(ct))
            .GroupBy(e => e.Nit)
            .ToDictionary(g => g.Key, g => g.First());

        var resumen = partidas
            .GroupBy(p => p.Nit)
            .Select(g =>
            {
                clientes.TryGetValue(g.Key, out var cliente);
                ultimosEnvios.TryGetValue(g.Key, out var envio);
                return new ClienteCarteraResumenDto(
                    g.Key,
                    cliente?.Nombre ?? g.First().NombreCliente,
                    g.Count(),
                    Edades(g, hoy),
                    g.Where(p => p.Saldo > 0).Select(p => DiasVencido(p.Vencimiento, hoy)).DefaultIfEmpty(0).Max(),
                    cliente?.CorreosCobro,
                    cliente?.Contacto,
                    envio is null ? null : new UltimoEnvioDto(envio.Fecha, envio.Usuario, envio.Exitoso, envio.Error));
            })
            // Saldos de centavos/1 peso son redondeos de WO, no cartera real.
            .Where(c => Math.Abs(c.Edades.Total) > 1)
            .OrderByDescending(c => c.Edades.Vencido)
            .ThenByDescending(c => c.Edades.Total)
            .ToList();

        var totales = new EdadesCarteraDto(
            resumen.Sum(c => c.Edades.PorVencer),
            resumen.Sum(c => c.Edades.De1A30),
            resumen.Sum(c => c.Edades.De31A60),
            resumen.Sum(c => c.Edades.De61A90),
            resumen.Sum(c => c.Edades.MasDe90));

        return new CarteraDto(await LeerUltimaSincronizacionAsync(ct), totales, config.SaldoMinimo, resumen);
    }

    public async Task<ClienteCarteraDetalleDto?> ObtenerClienteAsync(string nit, CancellationToken ct = default)
    {
        nit = NormalizarNit(nit);
        var cliente = await db.ClientesCartera.AsNoTracking().FirstOrDefaultAsync(c => c.Nit == nit, ct);
        var partidas = await db.PartidasCartera.AsNoTracking().Where(p => p.Nit == nit && !p.EsRemision).ToListAsync(ct);
        if (cliente is null && partidas.Count == 0) return null;

        var hoy = Hoy();
        var envios = await db.EnviosCartera.AsNoTracking()
            .Where(e => e.Nit == nit)
            .OrderByDescending(e => e.Fecha)
            .Take(50)
            .ToListAsync(ct);

        return new ClienteCarteraDetalleDto(
            nit,
            cliente?.Nombre ?? partidas[0].NombreCliente,
            cliente?.CorreosWorldOffice,
            cliente?.CorreosCobro,
            cliente?.Contacto,
            cliente?.Telefono,
            Edades(partidas, hoy),
            Documentos(partidas, hoy),
            envios.Select(MapEnvio).ToList());
    }

    // ---------------- Correo ----------------

    public async Task<CorreoCarteraDto?> ConstruirCorreoAsync(string nit, CancellationToken ct = default)
    {
        nit = NormalizarNit(nit);
        var cliente = await db.ClientesCartera.AsNoTracking().FirstOrDefaultAsync(c => c.Nit == nit, ct);
        var partidas = await db.PartidasCartera.AsNoTracking().Where(p => p.Nit == nit && !p.EsRemision).ToListAsync(ct);
        if (cliente is null && partidas.Count == 0) return null;

        var hoy = Hoy();
        var config = await LeerConfiguracionAsync(ct);
        var nombre = cliente?.Nombre ?? partidas[0].NombreCliente;
        var documentos = Documentos(partidas, hoy);
        var edades = Edades(partidas, hoy);
        var para = CorreoCarteraBuilder.SepararCorreos(cliente?.CorreosCobro);
        var copias = CorreoCarteraBuilder.SepararCorreos(config.Copias)
            .Where(c => !para.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToList();

        string? motivo = null;
        if (documentos.Count == 0 || edades.Total <= 0)
            motivo = "El cliente no tiene saldo pendiente.";
        else if (edades.Total < config.SaldoMinimo)
            motivo = $"El saldo ({CorreoCarteraBuilder.Moneda(edades.Total)}) es menor al mínimo para enviar ({CorreoCarteraBuilder.Moneda(config.SaldoMinimo)}).";
        else if (para.Count == 0)
            motivo = "El cliente no tiene correo de cobro.";

        return new CorreoCarteraDto(
            nit,
            nombre,
            config.Remitente,
            para,
            copias,
            CorreoCarteraBuilder.Asunto(config.Asunto, nombre, nit, hoy, edades.Total, edades.Vencido),
            CorreoCarteraBuilder.Html(config.Cuerpo, config.DatosPago, config.NombreRemitente, nombre, nit, hoy, documentos, edades),
            documentos.Count,
            edades.Total,
            edades.Vencido,
            motivo);
    }

    public async Task<ResultadoEnvioCarteraDto?> EnviarAsync(string nit, string usuario, CancellationToken ct = default)
    {
        var correo = await ConstruirCorreoAsync(nit, ct);
        if (correo is null) return null;
        if (correo.NoSePuedeEnviar is not null)
            return new ResultadoEnvioCarteraDto(correo.Nit, correo.NombreCliente, false, correo.NoSePuedeEnviar);

        var config = await LeerConfiguracionAsync(ct);
        string? error = null;
        try
        {
            await enviador.EnviarAsync(new CorreoSaliente(config.Remitente, config.NombreRemitente, correo.Para, correo.Copias, correo.Asunto, correo.Html), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not InvalidOperationException)
        {
            // El fallo queda en el historial (rebote del servidor, buzón inexistente, etc.).
            error = ex.Message;
        }

        db.EnviosCartera.Add(new EnvioCartera
        {
            Nit = correo.Nit,
            NombreCliente = Truncar(correo.NombreCliente, 300),
            Fecha = reloj.GetUtcNow().UtcDateTime,
            Usuario = Truncar(usuario, 200),
            Para = Truncar(string.Join("; ", correo.Para), 1000),
            Copias = correo.Copias.Count > 0 ? Truncar(string.Join("; ", correo.Copias), 1000) : null,
            Asunto = Truncar(correo.Asunto, 300),
            Documentos = correo.Documentos,
            Saldo = correo.Saldo,
            SaldoVencido = correo.SaldoVencido,
            Exitoso = error is null,
            Error = error is null ? null : Truncar(error, 1000)
        });
        await db.SaveChangesAsync(ct);

        return new ResultadoEnvioCarteraDto(correo.Nit, correo.NombreCliente, error is null, error);
    }

    // ---------------- Contactos de cobro ----------------

    public async Task<ClienteCarteraDetalleDto?> ActualizarContactoAsync(string nit, ContactoCobroDto contacto, CancellationToken ct = default)
    {
        nit = NormalizarNit(nit);
        var cliente = await db.ClientesCartera.FirstOrDefaultAsync(c => c.Nit == nit, ct);
        if (cliente is null)
        {
            var partida = await db.PartidasCartera.AsNoTracking().FirstOrDefaultAsync(p => p.Nit == nit, ct);
            if (partida is null) return null;
            cliente = new ClienteCartera { Nit = nit, Nombre = partida.NombreCliente };
            db.ClientesCartera.Add(cliente);
        }

        Aplicar(cliente, contacto.CorreosCobro, contacto.Contacto, contacto.Telefono, lanzarSiInvalido: true);
        await db.SaveChangesAsync(ct);
        return await ObtenerClienteAsync(nit, ct);
    }

    public async Task<CargaContactosResultadoDto> CargarContactosAsync(IReadOnlyList<ContactoCobroCargaDto> contactos, CancellationToken ct = default)
    {
        var existentes = await db.ClientesCartera.ToDictionaryAsync(c => c.Nit, ct);
        var nombresWo = (await db.PartidasCartera.AsNoTracking().Select(p => new { p.Nit, p.NombreCliente }).ToListAsync(ct))
            .GroupBy(p => p.Nit)
            .ToDictionary(g => g.Key, g => g.First().NombreCliente);

        int actualizados = 0, creados = 0;
        var avisos = new List<string>();
        foreach (var c in contactos)
        {
            var nit = NormalizarNit(c.Nit);
            if (nit.Length == 0)
            {
                avisos.Add($"Se omitió \"{c.Nombre}\": no tiene NIT.");
                continue;
            }

            if (!existentes.TryGetValue(nit, out var cliente))
            {
                var nombre = nombresWo.GetValueOrDefault(nit) ?? c.Nombre?.Trim();
                if (string.IsNullOrWhiteSpace(nombre))
                {
                    avisos.Add($"Se omitió el NIT {nit}: no está en la cartera y no trae nombre.");
                    continue;
                }
                cliente = new ClienteCartera { Nit = nit, Nombre = Truncar(nombre, 300) };
                db.ClientesCartera.Add(cliente);
                existentes[nit] = cliente;
                creados++;
            }
            else
            {
                actualizados++;
            }

            var invalidos = Aplicar(cliente, c.CorreosCobro, c.Contacto, c.Telefono, lanzarSiInvalido: false);
            if (invalidos.Count > 0)
                avisos.Add($"{cliente.Nombre} ({nit}): se ignoró lo que no es un correo válido: {string.Join(", ", invalidos)}.");
        }

        await db.SaveChangesAsync(ct);
        return new CargaContactosResultadoDto(actualizados, creados, avisos);
    }

    private List<string> Aplicar(ClienteCartera cliente, string? correosCobro, string? contacto, string? telefono, bool lanzarSiInvalido)
    {
        var invalidos = new List<string>();
        var correos = CorreoCarteraBuilder.SepararCorreos(correosCobro, invalidos);
        if (lanzarSiInvalido && invalidos.Count > 0)
            throw new ArgumentException($"No son correos válidos: {string.Join(", ", invalidos)}.");

        var texto = correos.Count > 0 ? string.Join("; ", correos) : null;
        if (texto?.Length > 1000)
            throw new ArgumentException("Son demasiados correos para un cliente.");

        cliente.CorreosCobro = texto;
        cliente.Contacto = Vacio(contacto, 200);
        cliente.Telefono = Vacio(telefono, 100);
        cliente.FechaActualizacionContacto = reloj.GetUtcNow().UtcDateTime;
        return invalidos;
    }

    // ---------------- Remisiones e historial ----------------

    public async Task<IReadOnlyList<RemisionSinFacturaDto>> ObtenerRemisionesSinFacturaAsync(CancellationToken ct = default)
    {
        var hoy = Hoy();
        var remisiones = await db.RemisionesPendientesWorldOffice.AsNoTracking().ToListAsync(ct);
        var nombres = await db.ClientesCartera.AsNoTracking().ToDictionaryAsync(c => c.Nit, c => c.Nombre, ct);

        return remisiones
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.Numero)
            .Select(r => new RemisionSinFacturaDto(
                r.Nit,
                nombres.GetValueOrDefault(r.Nit) ?? r.NombreCliente,
                r.Numero,
                r.Fecha,
                Math.Max(0, hoy.DayNumber - r.Fecha.DayNumber),
                r.Valor,
                r.Facturado,
                r.Pagado,
                r.Valor - r.Facturado - r.Otros,
                r.Pagado > 1m))
            .ToList();
    }

    public async Task<IReadOnlyList<EnvioCarteraDto>> ObtenerEnviosAsync(int maximo, CancellationToken ct = default)
        => (await db.EnviosCartera.AsNoTracking()
                .OrderByDescending(e => e.Fecha)
                .Take(Math.Clamp(maximo, 1, 1000))
                .ToListAsync(ct))
            .Select(MapEnvio)
            .ToList();

    // ---------------- Configuración ----------------

    public async Task<ConfiguracionCarteraDto> ObtenerConfiguracionAsync(CancellationToken ct = default)
        => MapConfiguracion(await LeerConfiguracionAsync(ct));

    public async Task<ConfiguracionCarteraDto> ActualizarConfiguracionAsync(ConfiguracionCarteraDto dto, CancellationToken ct = default)
    {
        var remitente = dto.Remitente?.Trim() ?? "";
        if (!CorreoCarteraBuilder.EsCorreoValido(remitente))
            throw new ArgumentException("El correo remitente no es válido.");
        if (string.IsNullOrWhiteSpace(dto.NombreRemitente))
            throw new ArgumentException("Falta el nombre del remitente.");
        if (string.IsNullOrWhiteSpace(dto.Asunto))
            throw new ArgumentException("Falta el asunto del correo.");
        if (string.IsNullOrWhiteSpace(dto.Cuerpo))
            throw new ArgumentException("Falta el texto del correo.");
        if (dto.SaldoMinimo < 0)
            throw new ArgumentException("El saldo mínimo no puede ser negativo.");

        var invalidos = new List<string>();
        var copias = CorreoCarteraBuilder.SepararCorreos(dto.Copias, invalidos);
        if (invalidos.Count > 0)
            throw new ArgumentException($"En las copias hay algo que no es un correo válido: {string.Join(", ", invalidos)}.");

        var config = await db.ConfiguracionCartera.FirstOrDefaultAsync(ct);
        if (config is null)
        {
            config = NuevaConfiguracion();
            db.ConfiguracionCartera.Add(config);
        }

        config.Remitente = Truncar(remitente, 200);
        config.NombreRemitente = Truncar(dto.NombreRemitente.Trim(), 200);
        config.Copias = copias.Count > 0 ? Truncar(string.Join("; ", copias), 1000) : null;
        config.SaldoMinimo = Math.Round(dto.SaldoMinimo, 2);
        config.Asunto = Truncar(dto.Asunto.Trim(), 300);
        config.Cuerpo = dto.Cuerpo.Trim();
        config.DatosPago = string.IsNullOrWhiteSpace(dto.DatosPago) ? null : dto.DatosPago.Trim();
        await db.SaveChangesAsync(ct);
        return MapConfiguracion(config);
    }

    private async Task<ConfiguracionCartera> LeerConfiguracionAsync(CancellationToken ct)
        => await db.ConfiguracionCartera.AsNoTracking().FirstOrDefaultAsync(ct) ?? NuevaConfiguracion();

    private static ConfiguracionCartera NuevaConfiguracion() => new()
    {
        Remitente = "cartera@ferrealiadosjv.com",
        NombreRemitente = "Cartera FERREALIADOS JV",
        Copias = "ventas@ferrealiadosjv.com; gerencia.comercial@jimaco.com.co",
        SaldoMinimo = 10000m,
        Asunto = ConfiguracionCarteraPorDefecto.Asunto,
        Cuerpo = ConfiguracionCarteraPorDefecto.Cuerpo
    };

    private ConfiguracionCarteraDto MapConfiguracion(ConfiguracionCartera c)
        => new(c.Remitente, c.NombreRemitente, c.Copias, c.SaldoMinimo, c.Asunto, c.Cuerpo, c.DatosPago, enviador.EstaConfigurado);

    // ---------------- Utilidades ----------------

    private DateOnly Hoy() => DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime + HoraColombia);

    private static int DiasVencido(DateOnly vencimiento, DateOnly hoy) => Math.Max(0, hoy.DayNumber - vencimiento.DayNumber);

    private static EdadesCarteraDto Edades(IEnumerable<PartidaCartera> partidas, DateOnly hoy)
    {
        decimal porVencer = 0, de1A30 = 0, de31A60 = 0, de61A90 = 0, masDe90 = 0;
        foreach (var p in partidas)
        {
            switch (DiasVencido(p.Vencimiento, hoy))
            {
                case 0: porVencer += p.Saldo; break;
                case <= 30: de1A30 += p.Saldo; break;
                case <= 60: de31A60 += p.Saldo; break;
                case <= 90: de61A90 += p.Saldo; break;
                default: masDe90 += p.Saldo; break;
            }
        }
        return new EdadesCarteraDto(porVencer, de1A30, de31A60, de61A90, masDe90);
    }

    private static List<DocumentoCarteraDto> Documentos(IEnumerable<PartidaCartera> partidas, DateOnly hoy)
        => partidas
            .OrderBy(p => p.Vencimiento)
            .ThenBy(p => p.Fecha)
            .ThenBy(p => p.Numero)
            .Select(p => new DocumentoCarteraDto(p.TipoDocumento, p.Prefijo, p.Numero, p.Fecha, p.Vencimiento, DiasVencido(p.Vencimiento, hoy), p.Saldo))
            .ToList();

    private static EnvioCarteraDto MapEnvio(EnvioCartera e)
        => new(e.Id, e.Nit, e.NombreCliente, e.Fecha, e.Usuario, e.Para, e.Copias, e.Asunto, e.Documentos, e.Saldo, e.SaldoVencido, e.Exitoso, e.Error);

    private static string NormalizarNit(string? nit) => (nit ?? "").Trim();

    // WO trae nombres con espacios dobles ("MINIMA  ARQUITECTOS  S A S").
    private static string NormalizarNombre(string? nombre) => string.Join(' ', (nombre ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string? Vacio(string? texto, int maximo) => string.IsNullOrWhiteSpace(texto) ? null : Truncar(texto.Trim(), maximo);

    private static string Truncar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
