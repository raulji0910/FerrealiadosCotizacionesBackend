using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferrealiados.Cotizaciones.Modelo.Entidades;

// Partida abierta de la cartera de clientes, tal como la trae Vista_CuentasPorCobrar_Detallada de
// World Office (cuenta 1305): la vista ya descuenta los abonos, así que el Saldo es lo que falta.
// La sube el Sincronizador; WO es la fuente de verdad y cada corrida deja aquí exactamente lo que
// hay allá (inserta, actualiza y borra). Identidad: AbonaADocumento + cuenta contable.
// En Ferrealiados las remisiones también se contabilizan en cartera; la factura (FVE) las cruza al
// facturarlas, así que una REM que sigue con saldo es una remisión todavía sin factura.
public class PartidaCartera
{
    public int Id { get; set; }

    public int IdDocumentoWorldOffice { get; set; }

    [MaxLength(30)]
    public required string CodigoCuenta { get; set; }

    [MaxLength(20)]
    public required string Nit { get; set; }

    [MaxLength(300)]
    public required string NombreCliente { get; set; }

    [MaxLength(100)]
    public required string TipoDocumento { get; set; }

    [MaxLength(20)]
    public required string Prefijo { get; set; }

    public long Numero { get; set; }

    public DateOnly Fecha { get; set; }

    public DateOnly Vencimiento { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Saldo { get; set; }

    // Remisión (o devolución de remisión) pendiente de facturar: no se le cobra al cliente, va en
    // la pestaña "Remisiones sin factura".
    public bool EsRemision { get; set; }

    public DateTime FechaSincronizacion { get; set; }
}

// Remisión de World Office que todavía tiene algo por facturar. No sale del saldo de cartera (un
// recibo de caja contra la remisión la deja en cero sin que se haya facturado) sino de lo que le
// abonaron las facturas: Pendiente = Valor - Facturado - Otros. Pagado = lo que abonaron recibos de
// caja directamente a la remisión ("pagada sin factura"). La sube el Sincronizador completa en cada
// corrida (solo las que tienen pendiente). Identidad: IdAsientoContable de WO.
public class RemisionPendienteWorldOffice
{
    public int Id { get; set; }

    public int IdAsientoContable { get; set; }

    public long Numero { get; set; }

    public DateOnly Fecha { get; set; }

    [MaxLength(20)]
    public required string Nit { get; set; }

    [MaxLength(300)]
    public required string NombreCliente { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Valor { get; set; }

    // Abonado por facturas (FVE), incluida la devolución de remisión que WO registra con ese prefijo.
    [Column(TypeName = "decimal(18,2)")]
    public decimal Facturado { get; set; }

    // Abonado por recibos de caja (RC) directamente a la remisión, sin factura de por medio.
    [Column(TypeName = "decimal(18,2)")]
    public decimal Pagado { get; set; }

    // Abonado por otros documentos (notas, cruces entre remisiones…).
    [Column(TypeName = "decimal(18,2)")]
    public decimal Otros { get; set; }

    public DateTime FechaSincronizacion { get; set; }
}

// Cliente de la cartera: nombre y correos que trae World Office (son los de facturación, solo de
// referencia) + los datos de cobro que se manejan en la app. Los correos de cobro van separados
// por ";". Los datos de cobro nunca los toca el Sincronizador.
public class ClienteCartera
{
    public int Id { get; set; }

    [MaxLength(20)]
    public required string Nit { get; set; }

    [MaxLength(300)]
    public required string Nombre { get; set; }

    [MaxLength(600)]
    public string? CorreosWorldOffice { get; set; }

    [MaxLength(1000)]
    public string? CorreosCobro { get; set; }

    [MaxLength(200)]
    public string? Contacto { get; set; }

    [MaxLength(100)]
    public string? Telefono { get; set; }

    public DateTime? FechaActualizacionContacto { get; set; }
}

// Un correo de estado de cuenta enviado (o que falló) a un cliente.
public class EnvioCartera
{
    public int Id { get; set; }

    [MaxLength(20)]
    public required string Nit { get; set; }

    [MaxLength(300)]
    public required string NombreCliente { get; set; }

    public DateTime Fecha { get; set; }

    [MaxLength(200)]
    public required string Usuario { get; set; }

    [MaxLength(1000)]
    public required string Para { get; set; }

    [MaxLength(1000)]
    public string? Copias { get; set; }

    [MaxLength(300)]
    public required string Asunto { get; set; }

    public int Documentos { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Saldo { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SaldoVencido { get; set; }

    public bool Exitoso { get; set; }

    [MaxLength(1000)]
    public string? Error { get; set; }
}

// Parámetros del envío de cartera que maneja el Administrador desde la pantalla (una sola fila).
// La cuenta y clave del servidor de correo (SMTP) NO van aquí: van en la configuración del servidor.
public class ConfiguracionCartera
{
    public int Id { get; set; }

    [MaxLength(200)]
    public required string Remitente { get; set; }

    [MaxLength(200)]
    public required string NombreRemitente { get; set; }

    // Correos que van en copia (CC, visibles) en todos los envíos, separados por ";".
    [MaxLength(1000)]
    public string? Copias { get; set; }

    // Por debajo de este saldo total no se le envía cobro al cliente (evita correos por redondeos).
    [Column(TypeName = "decimal(18,2)")]
    public decimal SaldoMinimo { get; set; }

    [MaxLength(300)]
    public required string Asunto { get; set; }

    public required string Cuerpo { get; set; }

    // Texto al final del correo: cuentas bancarias, a quién enviar el soporte de pago, etc.
    public string? DatosPago { get; set; }
}

// Textos iniciales del correo. Marcadores que se reemplazan al enviar: {cliente}, {nit},
// {fechaCorte}, {total}, {vencido}.
public static class ConfiguracionCarteraPorDefecto
{
    public const string Asunto = "Estado de cuenta FERREALIADOS JV - {cliente} - corte {fechaCorte}";

    public const string Cuerpo =
        "Señores {cliente}:\n\n" +
        "Cordial saludo. Les compartimos el estado de cuenta con FERREALIADOS JV con corte al {fechaCorte}, " +
        "por un saldo total de {total}.\n\n" +
        "Agradecemos su pronta respuesta ya que, revisando nuestros extractos bancarios, no contamos con " +
        "soportes de pago para los siguientes documentos. Si ya realizaron el pago, por favor envíennos el " +
        "soporte respondiendo este correo.";
}
