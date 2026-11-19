using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Negocio.Cartera;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Servicios;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ferrealiados.Cotizaciones.TestUnitarios;

public class CarteraServiceTests
{
    // 18/11/2026 12:00 en Colombia.
    private static readonly DateTime AhoraUtc = new(2026, 11, 18, 17, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CrearContexto()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Llave de prueba (32 bytes en base64), solo para estos tests.
    private const string LlavePrueba = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    // Por defecto, como si el .env trajera SMTP_PASSWORD (los tests de envío no dependen de la clave).
    private static readonly SmtpOptions SmtpConClaveDelServidor = new()
        { Host = "smtp.prueba", Puerto = 465, Usuario = "cartera@ferrealiadosjv.com", Password = "clave-del-env" };

    private static readonly SmtpOptions SmtpSinClave = new() { Host = "smtp.prueba", Puerto = 465 };

    private static CarteraService CrearServicio(AppDbContext db, EnviadorFalso? enviador = null, SmtpOptions? smtp = null, string? llave = LlavePrueba)
        => new(
            db,
            enviador ?? new EnviadorFalso(),
            Options.Create(smtp ?? SmtpConClaveDelServidor),
            new CifradorSecretosAes(Options.Create(new CifradoOptions { Clave = llave })),
            new TiempoFijo(AhoraUtc));

    private static PartidaCarteraEntradaDto Partida(int id, string nit, decimal saldo, string vencimiento,
        string prefijo = "FVE", string tipo = "Factura de Venta", string nombre = "CLIENTE UNO SAS", long numero = 0)
        => new(id, "13050501", nit, nombre, tipo, prefijo, numero == 0 ? id : numero,
            DateOnly.Parse(vencimiento).AddDays(-30), DateOnly.Parse(vencimiento), saldo);

    private static SincronizacionCarteraEntradaDto Entrada(params PartidaCarteraEntradaDto[] partidas)
        => new(partidas, null);

    [Fact]
    public async Task Sincronizar_DejaExactamenteLoQueHayEnWorldOffice()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 100m, "2026-11-01"), Partida(2, "900", 200m, "2026-11-01")));

        // La 1 se abonó en parte, la 2 se pagó (ya no viene) y llega una nueva.
        var r = await servicio.SincronizarAsync(Entrada(Partida(1, "900", 60m, "2026-11-01"), Partida(3, "900", 50m, "2026-12-01")));

        Assert.Equal(new SincronizacionCarteraResultadoDto(1, 1, 1, 0), r);
        var saldos = await db.PartidasCartera.OrderBy(p => p.IdDocumentoWorldOffice).Select(p => p.Saldo).ToListAsync();
        Assert.Equal([60m, 50m], saldos);
    }

    [Fact]
    public async Task Sincronizar_QuitaEspaciosDoblesDelNombre_YNoMuestraSaldosDeRedondeo()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarAsync(Entrada(
            Partida(1, "900", 100m, "2026-11-01", nombre: " MINIMA  ARQUITECTOS  S A S "),
            Partida(2, "800", -1m, "2026-11-01", nombre: "CDA REVICOL SAS")));

        var cartera = await servicio.ObtenerAsync();

        var cliente = Assert.Single(cartera.Clientes);
        Assert.Equal("MINIMA ARQUITECTOS S A S", cliente.Nombre);
    }

    [Fact]
    public async Task Sincronizar_SinCambios_NoCuentaActualizadas()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        var entrada = Entrada(Partida(1, "900", 100m, "2026-11-01"));
        await servicio.SincronizarAsync(entrada);

        var r = await servicio.SincronizarAsync(entrada);

        Assert.Equal(new SincronizacionCarteraResultadoDto(0, 0, 0, 0), r);
    }

    [Fact]
    public async Task Sincronizar_SeparaRemisionesYDevolucionesDeRemision()
    {
        using var db = CrearContexto();
        await CrearServicio(db).SincronizarAsync(Entrada(
            Partida(1, "900", 100m, "2026-11-01"),
            Partida(2, "900", 500m, "2026-11-01", prefijo: "REM", tipo: "Remisión"),
            Partida(3, "900", -50m, "2026-11-01", prefijo: "FVE", tipo: "Devolucion de Remisión")));

        var remisiones = await db.PartidasCartera.Where(p => p.EsRemision).Select(p => p.IdDocumentoWorldOffice).OrderBy(x => x).ToListAsync();
        Assert.Equal([2, 3], remisiones);
    }

    [Fact]
    public async Task Sincronizar_GuardaCorreosDeWorldOffice_SinTocarLosDeCobro()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarAsync(new SincronizacionCarteraEntradaDto(
            [Partida(1, "900", 100m, "2026-11-01")],
            [new CorreosClienteWorldOfficeDto("900", "CLIENTE UNO SAS", ["facturacion@uno.com", "no es correo", "FACTURACION@uno.com"])]));
        await servicio.ActualizarContactoAsync("900", new ContactoCobroDto("pagos@uno.com", "Ana", "300"));

        // Corrida donde falló la consulta de correos: no se borran los de WO ni los de cobro.
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 100m, "2026-11-01")));

        var cliente = await db.ClientesCartera.SingleAsync();
        Assert.Equal("facturacion@uno.com", cliente.CorreosWorldOffice);
        Assert.Equal("pagos@uno.com", cliente.CorreosCobro);
        Assert.Equal("Ana", cliente.Contacto);
    }

    [Fact]
    public async Task Obtener_RepartePorEdadesYNoIncluyeRemisiones()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarAsync(Entrada(
            Partida(1, "900", 100m, "2026-11-30"),  // por vencer
            Partida(2, "900", 200m, "2026-11-08"),  // 10 días
            Partida(3, "900", 300m, "2026-10-04"),  // 45 días
            Partida(4, "900", 400m, "2026-09-04"),  // 75 días
            Partida(5, "900", 500m, "2026-06-01"),  // más de 90
            Partida(6, "900", 999m, "2026-06-01", prefijo: "REM", tipo: "Remisión")));

        var cartera = await servicio.ObtenerAsync();

        var cliente = Assert.Single(cartera.Clientes);
        Assert.Equal(new EdadesCarteraDto(100m, 200m, 300m, 400m, 500m), cliente.Edades);
        Assert.Equal(1400m, cliente.Edades.Vencido);
        Assert.Equal(5, cliente.Documentos);
        Assert.Equal(170, cliente.DiasMaximoVencido);
        Assert.Equal(1500m, cartera.Totales.Total);
        Assert.NotNull(cartera.FechaCorte);
    }

    [Fact]
    public async Task ConstruirCorreo_SinContactoDeCobro_NoSePuedeEnviar()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 50000m, "2026-11-01")));

        var correo = await servicio.ConstruirCorreoAsync("900");

        Assert.Equal("El cliente no tiene correo de cobro.", correo!.NoSePuedeEnviar);
    }

    [Fact]
    public async Task ConstruirCorreo_SaldoMenorAlMinimo_NoSePuedeEnviar()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 1m, "2026-11-01")));
        await servicio.ActualizarContactoAsync("900", new ContactoCobroDto("pagos@uno.com", null, null));

        var correo = await servicio.ConstruirCorreoAsync("900");

        Assert.StartsWith("El saldo ($1) es menor al mínimo", correo!.NoSePuedeEnviar);
    }

    [Fact]
    public async Task Enviar_MandaAlContactoConCopiasVisiblesYQuedaEnElHistorial()
    {
        using var db = CrearContexto();
        var enviador = new EnviadorFalso();
        var servicio = CrearServicio(db, enviador);
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 50000m, "2026-11-01", numero: 1234)));
        await servicio.ActualizarContactoAsync("900", new ContactoCobroDto("pagos@uno.com; tesoreria@uno.com", null, null));

        var r = await servicio.EnviarAsync("900", "cartera@ferrealiados.local");

        Assert.True(r!.Enviado);
        var enviado = Assert.Single(enviador.Enviados);
        Assert.Equal(["pagos@uno.com", "tesoreria@uno.com"], enviado.Para);
        Assert.Equal(["ventas@ferrealiadosjv.com", "gerencia.comercial@jimaco.com.co"], enviado.Copias);
        Assert.Equal("cartera@ferrealiadosjv.com", enviado.Remitente);
        Assert.Contains("FVE 1234", enviado.Html);
        Assert.Equal("Estado de cuenta FERREALIADOS JV - CLIENTE UNO SAS - corte 18/11/2026", enviado.Asunto);

        var envio = await db.EnviosCartera.SingleAsync();
        Assert.True(envio.Exitoso);
        Assert.Equal(50000m, envio.SaldoVencido);
        Assert.Equal("cartera@ferrealiados.local", envio.Usuario);
    }

    [Fact]
    public async Task Enviar_SiElServidorRechaza_QuedaComoFallidoEnElHistorial()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db, new EnviadorFalso { Error = new IOException("550 buzón inexistente") });
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 50000m, "2026-11-01")));
        await servicio.ActualizarContactoAsync("900", new ContactoCobroDto("pagos@uno.com", null, null));

        var r = await servicio.EnviarAsync("900", "yo");

        Assert.False(r!.Enviado);
        Assert.Equal("550 buzón inexistente", (await db.EnviosCartera.SingleAsync()).Error);
    }

    [Fact]
    public async Task Enviar_ClienteSinContacto_NoEnviaNiRegistra()
    {
        using var db = CrearContexto();
        var enviador = new EnviadorFalso();
        var servicio = CrearServicio(db, enviador);
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 50000m, "2026-11-01")));

        var r = await servicio.EnviarAsync("900", "yo");

        Assert.False(r!.Enviado);
        Assert.Empty(enviador.Enviados);
        Assert.Empty(db.EnviosCartera);
    }

    [Fact]
    public async Task ActualizarContacto_ConAlgoQueNoEsCorreo_Rechaza()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 50000m, "2026-11-01")));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.ActualizarContactoAsync("900", new ContactoCobroDto("pagos@uno.com; 302 4226878", null, null)));
        Assert.Contains("302", ex.Message);
    }

    [Fact]
    public async Task CargarContactos_CreaYActualiza_YAvisaLoInvalido()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 50000m, "2026-11-01")));

        var r = await servicio.CargarContactosAsync([
            new ContactoCobroCargaDto("900", "UNO", "pagos@uno.com; 3001234567", "Ana", null),
            new ContactoCobroCargaDto("800", "DOS SAS", "dos@dos.com", null, null),
            new ContactoCobroCargaDto("", "SIN NIT", "x@x.com", null, null)
        ]);

        Assert.Equal(1, r.Actualizados);
        Assert.Equal(1, r.Creados);
        Assert.Equal(2, r.Avisos.Count);
        Assert.Equal("pagos@uno.com", (await db.ClientesCartera.SingleAsync(c => c.Nit == "900")).CorreosCobro);
    }

    private static RemisionPendienteEntradaDto Rem(int id, decimal valor, decimal facturado = 0, decimal pagado = 0, decimal otros = 0,
        string fecha = "2026-10-09")
        => new(id, id, DateOnly.Parse(fecha), "900", "CLIENTE  UNO SAS", valor, facturado, pagado, otros);

    [Fact]
    public async Task RemisionesSinFactura_PendienteEsValorMenosLoFacturado_YMarcaLasPagadasSinFactura()
    {
        using var db = CrearContexto();
        await CrearServicio(db).SincronizarAsync(new SincronizacionCarteraEntradaDto([], null, [
            Rem(1, 500m),                                  // sin facturar
            Rem(2, 2932625m, facturado: 2526097m),         // facturada en parte
            Rem(3, 1019985m, pagado: 989094m),             // pagada casi completa, sin factura
            Rem(4, 300m, facturado: 299.5m),               // redondeo: no se muestra
            Rem(5, 800m, otros: 800m)                      // anulada por nota: no se muestra
        ]));

        var remisiones = await CrearServicio(db).ObtenerRemisionesSinFacturaAsync();

        Assert.Equal([1L, 2L, 3L], remisiones.Select(r => r.Numero));
        Assert.Equal(40, remisiones[0].DiasSinFacturar); // 09/10 → 18/11
        Assert.Equal(406528m, remisiones[1].PendienteFacturar);
        Assert.False(remisiones[1].PagadaSinFactura);
        Assert.Equal(1019985m, remisiones[2].PendienteFacturar); // lo pagado no la da por facturada
        Assert.True(remisiones[2].PagadaSinFactura);
        Assert.Equal("CLIENTE UNO SAS", remisiones[0].NombreCliente);
    }

    [Fact]
    public async Task RemisionesSinFactura_TraeLasFacturasYRecibosQueLeAbonaron()
    {
        using var db = CrearContexto();
        var abonos = new[]
        {
            new AbonoRemisionDto("RC ", 58, new DateOnly(2026, 11, 2), 100000m),
            new AbonoRemisionDto("FVE", 2210, new DateOnly(2026, 10, 20), 2526097m),
            new AbonoRemisionDto("FVE", 2300, new DateOnly(2026, 10, 25), 0.2m) // redondeo: no se muestra
        };
        await CrearServicio(db).SincronizarAsync(new SincronizacionCarteraEntradaDto([], null,
            [Rem(654, 2932625m, facturado: 2526097m, pagado: 100000m) with { Abonos = abonos }]));

        var detalle = Assert.Single(await CrearServicio(db).ObtenerRemisionesSinFacturaAsync()).Abonos;

        Assert.Equal(
            [new AbonoRemisionDto("FVE", 2210, new DateOnly(2026, 10, 20), 2526097m), new AbonoRemisionDto("RC", 58, new DateOnly(2026, 11, 2), 100000m)],
            detalle);
    }

    [Fact]
    public async Task RemisionesSinFactura_SeQuitanAlFacturarse_YNoSeTocanSiLaConsultaFallo()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        await servicio.SincronizarAsync(new SincronizacionCarteraEntradaDto([], null, [Rem(1, 500m), Rem(2, 700m)]));

        // Corrida sin remisiones (la consulta falló): se dejan como estaban.
        await servicio.SincronizarAsync(new SincronizacionCarteraEntradaDto([], null, null));
        Assert.Equal(2, (await servicio.ObtenerRemisionesSinFacturaAsync()).Count);

        // La 1 se facturó completa: ya no viene.
        var r = await servicio.SincronizarAsync(new SincronizacionCarteraEntradaDto([], null, [Rem(2, 700m)]));
        Assert.Equal(1, r.RemisionesPendientes);
        Assert.Equal([2L], (await servicio.ObtenerRemisionesSinFacturaAsync()).Select(x => x.Numero));
    }

    [Fact]
    public async Task ActualizarConfiguracion_CopiaInvalida_Rechaza()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);
        var actual = await servicio.ObtenerConfiguracionAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.ActualizarConfiguracionAsync(actual with { Copias = "ventas@ferrealiadosjv.com; gerencia" }));

        var nueva = await servicio.ActualizarConfiguracionAsync(actual with { SaldoMinimo = 50000m, Copias = "a@b.com" });
        Assert.Equal(50000m, nueva.SaldoMinimo);
        Assert.Equal("a@b.com", (await servicio.ObtenerConfiguracionAsync()).Copias);
    }

    [Fact]
    public void Html_EscapaElTextoYFormateaPesos()
    {
        var html = CorreoCarteraBuilder.Html("Señores {cliente}: total {total}", "Cuenta <b>123</b>", "Cartera", "A&D <SAS>", "900",
            new DateOnly(2026, 11, 18),
            [new DocumentoCarteraDto("Factura de Venta", "FVE", 7, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 18, 1234567.4m)],
            new EdadesCarteraDto(0, 1234567.4m, 0, 0, 0));

        Assert.Contains("A&amp;D &lt;SAS&gt;: total $1.234.567", html);
        Assert.Contains("Cuenta &lt;b&gt;123&lt;/b&gt;", html);
        Assert.DoesNotContain("<SAS>", html);
    }

    [Fact]
    public void SepararCorreos_LimpiaRepetidosYSeparaInvalidos()
    {
        var invalidos = new List<string>();

        var correos = CorreoCarteraBuilder.SepararCorreos("a@b.com, A@B.com;c@d.co\n302 4226878 @x", invalidos);

        Assert.Equal(["a@b.com", "c@d.co"], correos);
        Assert.Equal(["302", "4226878", "@x"], invalidos);
    }

    // ---------------- Clave del correo remitente ----------------

    private static async Task<CarteraService> ConClienteParaEnviar(AppDbContext db, EnviadorFalso enviador, SmtpOptions smtp)
    {
        var servicio = CrearServicio(db, enviador, smtp);
        await servicio.SincronizarAsync(Entrada(Partida(1, "900", 50000m, "2026-11-01")));
        await servicio.ActualizarContactoAsync("900", new ContactoCobroDto("pagos@uno.com", null, null));
        return servicio;
    }

    [Fact]
    public async Task Enviar_SinClaveDelCorreo_AvisaYNoRegistra()
    {
        using var db = CrearContexto();
        var enviador = new EnviadorFalso();
        var servicio = await ConClienteParaEnviar(db, enviador, SmtpSinClave);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.EnviarAsync("900", "yo"));

        Assert.Contains("Configuración", ex.Message);
        Assert.Empty(enviador.Enviados);
        Assert.Empty(db.EnviosCartera);
        Assert.False((await servicio.ObtenerConfiguracionAsync()).ServidorCorreoConfigurado);
    }

    [Fact]
    public async Task GuardarClave_SeCompruebaSeGuardaCifradaYSeUsaParaEnviar()
    {
        using var db = CrearContexto();
        var enviador = new EnviadorFalso { ClaveCorrecta = "Clave-Real.2026" };
        var servicio = await ConClienteParaEnviar(db, enviador, SmtpSinClave);

        var config = await servicio.GuardarClaveCorreoAsync("Clave-Real.2026", "admin@ferrealiados.local");

        Assert.True(config.ServidorCorreoConfigurado);
        Assert.Equal("Aplicacion", config.OrigenClaveCorreo);
        Assert.Equal("admin@ferrealiados.local", config.ClaveCorreoActualizadaPor);
        Assert.Equal(AhoraUtc, config.ClaveCorreoActualizada);

        var guardada = await db.ConfiguracionCartera.SingleAsync();
        Assert.NotNull(guardada.ClaveCorreoCifrada);
        Assert.DoesNotContain("Clave-Real", guardada.ClaveCorreoCifrada);
        Assert.Equal("cartera@ferrealiadosjv.com", guardada.ClaveCorreoCuenta);

        Assert.True((await servicio.EnviarAsync("900", "yo"))!.Enviado);
        var usadas = enviador.Credenciales.Last();
        Assert.Equal(("smtp.prueba", 465, "cartera@ferrealiadosjv.com", "Clave-Real.2026"),
            (usadas.Host, usadas.Puerto, usadas.Usuario, usadas.Password));
    }

    [Fact]
    public async Task GuardarClave_SiElServidorLaRechaza_NoSeGuarda()
    {
        using var db = CrearContexto();
        var servicio = await ConClienteParaEnviar(db, new EnviadorFalso { ClaveCorrecta = "buena" }, SmtpSinClave);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => servicio.GuardarClaveCorreoAsync("mala", "yo"));

        Assert.Contains("rechazó", ex.Message);
        Assert.Null((await db.ConfiguracionCartera.FirstOrDefaultAsync())?.ClaveCorreoCifrada);
    }

    [Fact]
    public async Task GuardarClave_SinLlaveDeCifradoEnElServidor_Avisa()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db, new EnviadorFalso(), SmtpSinClave, llave: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.GuardarClaveCorreoAsync("x", "yo"));

        Assert.Contains("CIFRADO_CLAVE", ex.Message);
    }

    [Fact]
    public async Task ClaveDelEnv_TienePrioridadSobreLaGuardada()
    {
        using var db = CrearContexto();
        var enviador = new EnviadorFalso();
        await CrearServicio(db, enviador, SmtpSinClave).GuardarClaveCorreoAsync("guardada", "yo");
        var servicio = await ConClienteParaEnviar(db, enviador, SmtpConClaveDelServidor);

        Assert.Equal("Servidor", (await servicio.ObtenerConfiguracionAsync()).OrigenClaveCorreo);
        await servicio.EnviarAsync("900", "yo");
        Assert.Equal("clave-del-env", enviador.Credenciales.Last().Password);
    }

    [Fact]
    public async Task SiCambianElRemitente_LaClaveGuardadaDejaDeUsarse()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db, new EnviadorFalso(), SmtpSinClave);
        await servicio.GuardarClaveCorreoAsync("clave", "yo");
        var actual = await servicio.ObtenerConfiguracionAsync();

        var config = await servicio.ActualizarConfiguracionAsync(actual with { Remitente = "cobros@ferrealiadosjv.com" });

        Assert.False(config.ServidorCorreoConfigurado);
        Assert.Equal("cartera@ferrealiadosjv.com", config.ClaveCorreoDeOtraCuenta);
        var prueba = await servicio.ProbarCorreoAsync();
        Assert.False(prueba.Exitosa);
        Assert.Contains("cobros@ferrealiadosjv.com", prueba.Mensaje);
    }

    [Fact]
    public async Task ProbarCorreo_InformaSiLaClaveSirve()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db, new EnviadorFalso(), SmtpSinClave);
        Assert.False((await servicio.ProbarCorreoAsync()).Exitosa);

        await servicio.GuardarClaveCorreoAsync("clave", "yo");

        var prueba = await servicio.ProbarCorreoAsync();
        Assert.True(prueba.Exitosa);
        Assert.Contains("cartera@ferrealiadosjv.com", prueba.Mensaje);
    }

    [Fact]
    public void Cifrador_DescifraSoloConLaMismaLlave()
    {
        var cifrador = new CifradorSecretosAes(Options.Create(new CifradoOptions { Clave = LlavePrueba }));
        var otra = new CifradorSecretosAes(Options.Create(new CifradoOptions { Clave = Convert.ToBase64String(new byte[32]) }));

        var cifrado = cifrador.Cifrar("Ñandú#2026");

        Assert.NotEqual(cifrado, cifrador.Cifrar("Ñandú#2026"));
        Assert.Equal("Ñandú#2026", cifrador.Descifrar(cifrado));
        Assert.Null(otra.Descifrar(cifrado));
        Assert.False(new CifradorSecretosAes(Options.Create(new CifradoOptions { Clave = "corta" })).EstaDisponible);
    }

    private sealed class EnviadorFalso : IEnviadorCorreo
    {
        public List<CorreoSaliente> Enviados { get; } = [];
        public List<CredencialesSmtp> Credenciales { get; } = [];
        public Exception? Error { get; init; }

        // Clave que el "servidor de correo" acepta en ProbarAsync (null = acepta cualquiera).
        public string? ClaveCorrecta { get; init; }

        public Task EnviarAsync(CorreoSaliente correo, CredencialesSmtp credenciales, CancellationToken ct = default)
        {
            if (Error is not null) throw Error;
            Enviados.Add(correo);
            Credenciales.Add(credenciales);
            return Task.CompletedTask;
        }

        public Task ProbarAsync(CredencialesSmtp credenciales, CancellationToken ct = default)
        {
            if (ClaveCorrecta is not null && credenciales.Password != ClaveCorrecta)
                throw new AuthenticationException("535 Incorrect authentication data");
            Credenciales.Add(credenciales);
            return Task.CompletedTask;
        }
    }

    private sealed class TiempoFijo(DateTime ahora) : TimeProvider
    {
        private readonly DateTimeOffset _ahora = new(ahora, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _ahora;
    }
}
