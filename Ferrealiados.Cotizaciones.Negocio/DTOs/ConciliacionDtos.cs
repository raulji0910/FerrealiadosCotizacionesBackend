using Ferrealiados.Cotizaciones.Negocio.Conciliacion;

namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

// ---- Lo que sube el Sincronizador ----

// Un XML tal como salió del ZIP adjunto al correo. La API lo interpreta (no el Sincronizador),
// así las reglas de lectura viven en un solo lugar.
public record FacturaXmlEntradaDto(string Xml, string? NombreArchivo, string? CorreoAsunto, DateTime? CorreoFecha);

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
    string Detalle);

public record OrdenPendienteDto(int Numero, DateOnly Fecha, string NitProveedor, string NombreProveedor, decimal Subtotal, decimal Iva, decimal SaldoSubtotal);

public record ResumenConciliacionDto(int Cuadran, int ConDiferencia, int Parciales, int SinOrdenCompra, int OrdenesPendientes);

public record ConciliacionDto(
    DateOnly Desde,
    DateOnly Hasta,
    ResumenConciliacionDto Resumen,
    IReadOnlyList<FacturaConciliadaDto> Facturas,
    IReadOnlyList<OrdenPendienteDto> OrdenesPendientes,
    DateTime? UltimaSincronizacionOrdenes,
    DateTime? UltimaFacturaRecibida);

public record RenglonComparacionDto(string? Codigo, string Descripcion, decimal Cantidad, decimal ValorUnitario, decimal Subtotal, decimal? PorcentajeIva);

public record DetalleFacturaConciliadaDto(
    FacturaConciliadaDto Conciliacion,
    IReadOnlyList<RenglonComparacionDto> RenglonesFactura,
    IReadOnlyList<RenglonComparacionDto> RenglonesOrdenCompra);
