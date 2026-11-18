namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

// ---- Lo que sube el Sincronizador ----

public record PartidaCarteraEntradaDto(
    int IdDocumentoWorldOffice,
    string CodigoCuenta,
    string Nit,
    string NombreCliente,
    string TipoDocumento,
    string Prefijo,
    long Numero,
    DateOnly Fecha,
    DateOnly Vencimiento,
    decimal Saldo);

// Correos que tiene el cliente en World Office (Email 1/2/3 de sus direcciones): son los de
// facturación, se guardan solo como referencia.
public record CorreosClienteWorldOfficeDto(string Nit, string Nombre, IReadOnlyList<string> Correos);

public record SincronizacionCarteraEntradaDto(
    IReadOnlyList<PartidaCarteraEntradaDto> Partidas,
    IReadOnlyList<CorreosClienteWorldOfficeDto>? CorreosClientes);

public record SincronizacionCarteraResultadoDto(int Nuevas, int Actualizadas, int Eliminadas, int Clientes);

// ---- Pantalla ----

// Saldo repartido por edades de vencimiento (días desde el vencimiento hasta la fecha de corte).
public record EdadesCarteraDto(decimal PorVencer, decimal De1A30, decimal De31A60, decimal De61A90, decimal MasDe90)
{
    public decimal Vencido => De1A30 + De31A60 + De61A90 + MasDe90;
    public decimal Total => PorVencer + Vencido;
}

public record UltimoEnvioDto(DateTime Fecha, string Usuario, bool Exitoso, string? Error);

public record ClienteCarteraResumenDto(
    string Nit,
    string Nombre,
    int Documentos,
    EdadesCarteraDto Edades,
    int DiasMaximoVencido,
    string? CorreosCobro,
    string? Contacto,
    UltimoEnvioDto? UltimoEnvio);

public record CarteraDto(
    DateTime? FechaCorte,
    EdadesCarteraDto Totales,
    decimal SaldoMinimo,
    IReadOnlyList<ClienteCarteraResumenDto> Clientes);

public record DocumentoCarteraDto(
    string TipoDocumento,
    string Prefijo,
    long Numero,
    DateOnly Fecha,
    DateOnly Vencimiento,
    int DiasVencido,
    decimal Saldo);

public record EnvioCarteraDto(
    int Id,
    string Nit,
    string NombreCliente,
    DateTime Fecha,
    string Usuario,
    string Para,
    string? Copias,
    string Asunto,
    int Documentos,
    decimal Saldo,
    decimal SaldoVencido,
    bool Exitoso,
    string? Error);

public record ClienteCarteraDetalleDto(
    string Nit,
    string Nombre,
    string? CorreosWorldOffice,
    string? CorreosCobro,
    string? Contacto,
    string? Telefono,
    EdadesCarteraDto Edades,
    IReadOnlyList<DocumentoCarteraDto> Documentos,
    IReadOnlyList<EnvioCarteraDto> Envios);

public record ContactoCobroDto(string? CorreosCobro, string? Contacto, string? Telefono);

// Carga de varios contactos a la vez (p. ej. la lista inicial que se revisó en Excel).
public record ContactoCobroCargaDto(string Nit, string? Nombre, string? CorreosCobro, string? Contacto, string? Telefono);

public record CargaContactosResultadoDto(int Actualizados, int Creados, IReadOnlyList<string> Avisos);

// Vista previa (y contenido real) del correo de un cliente. Si NoSePuedeEnviar trae un motivo,
// el botón de enviar no aplica para ese cliente.
public record CorreoCarteraDto(
    string Nit,
    string NombreCliente,
    string Remitente,
    IReadOnlyList<string> Para,
    IReadOnlyList<string> Copias,
    string Asunto,
    string Html,
    int Documentos,
    decimal Saldo,
    decimal SaldoVencido,
    string? NoSePuedeEnviar);

public record ResultadoEnvioCarteraDto(string Nit, string NombreCliente, bool Enviado, string? Motivo);

public record RemisionSinFacturaDto(
    string Nit,
    string NombreCliente,
    string TipoDocumento,
    string Prefijo,
    long Numero,
    DateOnly Fecha,
    int DiasSinFacturar,
    decimal Saldo);

public record ConfiguracionCarteraDto(
    string Remitente,
    string NombreRemitente,
    string? Copias,
    decimal SaldoMinimo,
    string Asunto,
    string Cuerpo,
    string? DatosPago,
    bool ServidorCorreoConfigurado);
