using Ferrealiados.Cotizaciones.Negocio.Conciliacion;

namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

// ---- Lo que sube el Sincronizador ----

// Un XML tal como salió del ZIP adjunto al correo. La API lo interpreta (no el Sincronizador),
// así las reglas de lectura viven en un solo lugar.
public record FacturaXmlEntradaDto(string Xml, string? NombreArchivo, string? CorreoAsunto, DateTime? CorreoFecha);

// Resultado de subir ZIP/XML desde la pantalla (descargados de la DIAN): un renglón por XML leído.
// Estado: "Nueva", "Duplicada" (ya estaba, por correo o por otra carga), "NoReconocida" (no es una
// factura electrónica) u "OtroReceptor" (factura que no es para Ferrealiados).
public record FacturaCargadaDto(string? NombreArchivo, string? Numero, string? NitProveedor, string? NombreProveedor,
    DateOnly? FechaEmision, decimal? Total, string Estado, string? OrigenExistente);

public static class EstadoCarga
{
    public const string Nueva = "Nueva";
    public const string Duplicada = "Duplicada";
    public const string NoReconocida = "NoReconocida";
    public const string OtroReceptor = "OtroReceptor";
}

// Un archivo subido desde la pantalla (lo arma el controlador a partir del IFormFile).
public record ArchivoCargado(string Nombre, Stream Contenido);

public record CargaArchivosResultadoDto(int Archivos, int Nuevas, int Duplicadas, int NoReconocidas, int OtroReceptor,
    IReadOnlyList<FacturaCargadaDto> Facturas, IReadOnlyList<string> Avisos);

public record RegistroFacturasResultadoDto(int Nuevas, int Duplicadas, int NoReconocidas);

public record OrdenCompraRenglonEntradaDto(string? Codigo, string Descripcion, decimal Cantidad, decimal ValorUnitario, decimal PorcentajeIva);

public record OrdenCompraEntradaDto(
    int IdAsientoContable,
    int Numero,
    DateOnly Fecha,
    string NitProveedor,
    string NombreProveedor,
    bool Anulada,
    IReadOnlyList<OrdenCompraRenglonEntradaDto> Renglones);

public record SincronizacionOrdenesResultadoDto(int Nuevas, int Actualizadas);

// Encabezado de una FC de World Office. Desde: la ventana que leyó el Sincronizador; la API borra
// las FC de esa ventana que ya no vinieron (anuladas a mano, borradas).
public record FacturaCompraEntradaDto(
    int IdAsientoContable,
    int Numero,
    DateOnly Fecha,
    string NitProveedor,
    string NombreProveedor,
    string? DocumentoExterno,
    string? ReferenciaOrdenes,
    bool Anulada);

public record SincronizacionFacturasCompraEntradaDto(DateOnly Desde, IReadOnlyList<FacturaCompraEntradaDto> Facturas);

public record SincronizacionFacturasCompraResultadoDto(int Nuevas, int Actualizadas, int Eliminadas);

// ---- Lo que consulta la pantalla de conciliación ----

public record FacturaConciliadaDto(
    int FacturaId,
    string Numero,
    DateOnly FechaEmision,
    string NitProveedor,
    string NombreProveedor,
    decimal Subtotal,
    decimal Iva,
    decimal Total,
    string? ReferenciaOrden,
    EstadoConciliacion Estado,
    int? OrdenCompraNumero,
    DateOnly? OrdenCompraFecha,
    decimal? OrdenCompraSubtotal,
    decimal? OrdenCompraIva,
    decimal? DiferenciaSubtotal,
    decimal? DiferenciaIva,
    string Detalle,
    // Todas las OC del cruce (más de una solo cuando salió de una FC de WO que cita varias) y la FC
    // de WO de donde salió el cruce, si fue por esa vía. OrdenCompraSubtotal/Iva suman todas.
    IReadOnlyList<int> OrdenesCompra,
    int? FacturaCompraNumero,
    // Por dónde entró la factura: "Correo", "Carpeta" o "Dian" (ZIP subido desde la pantalla).
    string Origen);

public record OrdenPendienteDto(int Numero, DateOnly Fecha, string NitProveedor, string NombreProveedor, decimal Subtotal, decimal Iva, decimal SaldoSubtotal);

public record OrdenFacturadaSinXmlDto(
    int Numero,
    DateOnly Fecha,
    string NitProveedor,
    string NombreProveedor,
    decimal Subtotal,
    decimal Iva,
    int FacturaCompraNumero,
    DateOnly FacturaCompraFecha,
    string? DocumentoExterno);

public record ReferenciaOrdenInvalidaDto(
    int FacturaCompraNumero,
    DateOnly Fecha,
    string NitProveedor,
    string NombreProveedor,
    string? DocumentoExterno,
    string? ReferenciaOrdenes,
    int OrdenCitada,
    string Motivo);

public record ResumenConciliacionDto(
    int Cuadran,
    int ConDiferencia,
    int Parciales,
    int SinOrdenCompra,
    int OrdenesPendientes,
    int OrdenesFacturadasSinXml,
    int ReferenciasInvalidas);

public record ConciliacionDto(
    DateOnly Desde,
    DateOnly Hasta,
    ResumenConciliacionDto Resumen,
    IReadOnlyList<FacturaConciliadaDto> Facturas,
    IReadOnlyList<OrdenPendienteDto> OrdenesPendientes,
    IReadOnlyList<OrdenFacturadaSinXmlDto> OrdenesFacturadasSinXml,
    IReadOnlyList<ReferenciaOrdenInvalidaDto> ReferenciasInvalidas,
    DateTime? UltimaSincronizacionOrdenes,
    DateTime? UltimaFacturaRecibida);

public record RenglonComparacionDto(string? Codigo, string Descripcion, decimal Cantidad, decimal ValorUnitario, decimal Subtotal, decimal? PorcentajeIva);

public record DetalleFacturaConciliadaDto(
    FacturaConciliadaDto Conciliacion,
    IReadOnlyList<RenglonComparacionDto> RenglonesFactura,
    IReadOnlyList<RenglonComparacionDto> RenglonesOrdenCompra);
