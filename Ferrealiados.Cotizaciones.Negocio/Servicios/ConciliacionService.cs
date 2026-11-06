using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Modelo.Entidades;
using Ferrealiados.Cotizaciones.Negocio.Conciliacion;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

// Conciliación XML de proveedores <-> OC de World Office. Los datos los sube el Sincronizador (que
// corre en la red de Ferrealiados, junto a WO); el cruce se calcula al vuelo con MotorConciliacion
// cada vez que se consulta, así un cambio de regla o una OC corregida en WO se refleja sin
// reprocesar nada guardado.
public class ConciliacionService(AppDbContext db, TimeProvider reloj) : IConciliacionService
{
    private static readonly ReglasConciliacion Reglas = new();

    public async Task<RegistroFacturasResultadoDto> RegistrarFacturasAsync(IReadOnlyList<FacturaXmlEntradaDto> facturas, CancellationToken ct = default)
    {
        int nuevas = 0, duplicadas = 0, noReconocidas = 0;
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var cufesEnLote = new HashSet<string>();

        foreach (var entrada in facturas)
        {
            FacturaElectronica? leida;
            try
            {
                leida = LectorFacturaElectronica.LeerXml(entrada.Xml);
            }
            catch (System.Xml.XmlException)
            {
                leida = null;
            }

            if (leida is null || string.IsNullOrWhiteSpace(leida.Cufe))
            {
                noReconocidas++;
                continue;
            }

            if (!cufesEnLote.Add(leida.Cufe) || await db.FacturasProveedor.AnyAsync(f => f.Cufe == leida.Cufe, ct))
            {
                duplicadas++;
                continue;
            }

            db.FacturasProveedor.Add(new FacturaProveedor
            {
                Cufe = Truncar(leida.Cufe, 200),
                Numero = Truncar(leida.Numero, 50),
                TipoDocumento = Truncar(leida.TipoDocumento, 5),
                FechaEmision = leida.FechaEmision,
                NitEmisor = Truncar(leida.NitEmisor, 20),
                NombreEmisor = Truncar(leida.NombreEmisor, 300),
                NitReceptor = Truncar(leida.NitReceptor, 20),
                Subtotal = leida.Subtotal,
                Iva = leida.Iva,
                Total = leida.Total,
                ReferenciaOrden = leida.ReferenciaOrden is null ? null : Truncar(leida.ReferenciaOrden, 100),
                Xml = entrada.Xml,
                NombreArchivo = entrada.NombreArchivo is null ? null : Truncar(entrada.NombreArchivo, 300),
                CorreoAsunto = entrada.CorreoAsunto is null ? null : Truncar(entrada.CorreoAsunto, 500),
                CorreoFecha = entrada.CorreoFecha,
                FechaRegistro = ahora,
                Renglones = leida.Renglones.Select((r, i) => new FacturaProveedorRenglon
                {
                    Orden = i + 1,
                    Descripcion = Truncar(r.Descripcion, 500),
                    Cantidad = r.Cantidad,
                    Subtotal = r.Subtotal,
                    PorcentajeIva = r.PorcentajeIva,
                }).ToList(),
            });
            nuevas++;
        }

        await db.SaveChangesAsync(ct);
        return new RegistroFacturasResultadoDto(nuevas, duplicadas, noReconocidas);
    }

    public async Task<SincronizacionOrdenesResultadoDto> SincronizarOrdenesAsync(IReadOnlyList<OrdenCompraEntradaDto> ordenes, CancellationToken ct = default)
    {
        var ids = ordenes.Select(o => o.IdAsientoContable).ToList();
        var existentes = await db.OrdenesCompraWorldOffice
            .Include(o => o.Renglones)
            .Where(o => ids.Contains(o.IdAsientoContable))
            .ToDictionaryAsync(o => o.IdAsientoContable, ct);
        var ahora = reloj.GetUtcNow().UtcDateTime;
        int nuevas = 0, actualizadas = 0;

        foreach (var entrada in ordenes)
        {
            if (!existentes.TryGetValue(entrada.IdAsientoContable, out var oc))
            {
                oc = new OrdenCompraWorldOffice
                {
                    IdAsientoContable = entrada.IdAsientoContable,
                    NitProveedor = "",
                    NombreProveedor = "",
                };
                db.OrdenesCompraWorldOffice.Add(oc);
                nuevas++;
            }
            else
            {
                db.OrdenCompraWorldOfficeRenglones.RemoveRange(oc.Renglones);
                oc.Renglones.Clear();
                actualizadas++;
            }

            // Mismo cálculo que MotorConciliacion espera: costo = Cantidad x ValorUnitario, IVA por
            // renglón con el % del producto (la línea de WO no trae su propio porcentaje).
            oc.Numero = entrada.Numero;
            oc.Fecha = entrada.Fecha;
            oc.NitProveedor = Truncar(LectorFacturaElectronica.NormalizarNit(entrada.NitProveedor), 20);
            oc.NombreProveedor = Truncar(entrada.NombreProveedor, 300);
            oc.Anulada = entrada.Anulada;
            oc.Subtotal = Math.Round(entrada.Renglones.Sum(r => r.Cantidad * r.ValorUnitario), 2);
            oc.Iva = Math.Round(entrada.Renglones.Sum(r => r.Cantidad * r.ValorUnitario * r.PorcentajeIva / 100m), 2);
            oc.FechaSincronizacion = ahora;
            foreach (var (r, i) in entrada.Renglones.Select((r, i) => (r, i)))
            {
                oc.Renglones.Add(new OrdenCompraWorldOfficeRenglon
                {
                    Orden = i + 1,
                    Codigo = r.Codigo is null ? null : Truncar(r.Codigo, 100),
                    Descripcion = Truncar(r.Descripcion, 500),
                    Cantidad = r.Cantidad,
                    ValorUnitario = r.ValorUnitario,
                    PorcentajeIva = r.PorcentajeIva,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return new SincronizacionOrdenesResultadoDto(nuevas, actualizadas);
    }

    public async Task<SincronizacionFacturasCompraResultadoDto> SincronizarFacturasCompraAsync(
        SincronizacionFacturasCompraEntradaDto entrada, CancellationToken ct = default)
    {
        var existentes = await db.FacturasCompraWorldOffice
            .Where(f => f.Fecha >= entrada.Desde)
            .ToDictionaryAsync(f => f.IdAsientoContable, ct);
        var ids = entrada.Facturas.Select(f => f.IdAsientoContable).ToHashSet();
        var fueraDeVentana = await db.FacturasCompraWorldOffice
            .Where(f => f.Fecha < entrada.Desde && ids.Contains(f.IdAsientoContable))
            .ToListAsync(ct);
        foreach (var f in fueraDeVentana) existentes[f.IdAsientoContable] = f;

        var ahora = reloj.GetUtcNow().UtcDateTime;
        int nuevas = 0, actualizadas = 0;
        foreach (var e in entrada.Facturas)
        {
            if (!existentes.TryGetValue(e.IdAsientoContable, out var fc))
            {
                fc = new FacturaCompraWorldOffice { IdAsientoContable = e.IdAsientoContable, NitProveedor = "", NombreProveedor = "" };
                db.FacturasCompraWorldOffice.Add(fc);
                existentes[e.IdAsientoContable] = fc;
                nuevas++;
            }
            else
            {
                actualizadas++;
            }

            fc.Numero = e.Numero;
            fc.Fecha = e.Fecha;
            fc.NitProveedor = Truncar(LectorFacturaElectronica.NormalizarNit(e.NitProveedor), 20);
            fc.NombreProveedor = Truncar(e.NombreProveedor, 300);
            fc.DocumentoExterno = string.IsNullOrWhiteSpace(e.DocumentoExterno) ? null : Truncar(e.DocumentoExterno.Trim(), 100);
            fc.ReferenciaOrdenes = string.IsNullOrWhiteSpace(e.ReferenciaOrdenes) ? null : Truncar(e.ReferenciaOrdenes.Trim(), 200);
            fc.Anulada = e.Anulada;
            fc.FechaSincronizacion = ahora;
        }

        // Las de la ventana que ya no vinieron se borraron en WO.
        var borradas = existentes.Values.Where(f => f.Fecha >= entrada.Desde && !ids.Contains(f.IdAsientoContable)).ToList();
        db.FacturasCompraWorldOffice.RemoveRange(borradas);

        await db.SaveChangesAsync(ct);
        return new SincronizacionFacturasCompraResultadoDto(nuevas, actualizadas, borradas.Count);
    }

    public async Task<ConciliacionDto> ConciliarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var (resultado, facturasPorCufe) = await CalcularAsync(desde, hasta, ct);

        var facturas = resultado.Facturas
            .Where(r => r.Factura.FechaEmision >= desde && r.Factura.FechaEmision <= hasta)
            .Select(r => MapFactura(r, facturasPorCufe[r.Factura.Cufe]))
            .OrderByDescending(f => f.FechaEmision).ThenBy(f => f.NombreProveedor)
            .ToList();

        // Solo las OC del rango consultado: las de la ventana anterior sirven de contexto para el cruce,
        // pero mostrarlas como "sin factura" confunde cuando sus facturas son de antes del rango.
        var pendientes = resultado.OrdenesPendientes
            .Where(p => p.OrdenCompra.Fecha >= desde && p.OrdenCompra.Fecha <= hasta)
            .Select(p => new OrdenPendienteDto(p.OrdenCompra.Numero, p.OrdenCompra.Fecha, p.OrdenCompra.NitProveedor,
                p.OrdenCompra.NombreProveedor, p.OrdenCompra.Subtotal, p.OrdenCompra.Iva, p.SaldoSubtotal))
            .OrderByDescending(p => p.Fecha)
            .ToList();

        var facturadasSinXml = resultado.OrdenesFacturadasSinXml
            .Where(p => p.OrdenCompra.Fecha >= desde && p.OrdenCompra.Fecha <= hasta)
            .Select(p => new OrdenFacturadaSinXmlDto(p.OrdenCompra.Numero, p.OrdenCompra.Fecha, p.OrdenCompra.NitProveedor,
                p.OrdenCompra.NombreProveedor, p.OrdenCompra.Subtotal, p.OrdenCompra.Iva,
                p.FacturaCompra.Numero, p.FacturaCompra.Fecha, p.FacturaCompra.DocumentoExterno))
            .OrderByDescending(p => p.Fecha)
            .ToList();

        var invalidas = resultado.ReferenciasInvalidas
            .Where(r => r.FacturaCompra.Fecha >= desde && r.FacturaCompra.Fecha <= hasta)
            .Select(r => new ReferenciaOrdenInvalidaDto(r.FacturaCompra.Numero, r.FacturaCompra.Fecha, r.FacturaCompra.NitProveedor,
                r.FacturaCompra.NombreProveedor, r.FacturaCompra.DocumentoExterno, r.FacturaCompra.ReferenciaOrdenes,
                r.NumeroOrdenCitada, r.Motivo))
            .OrderByDescending(r => r.Fecha).ThenBy(r => r.FacturaCompraNumero)
            .ToList();

        var resumen = new ResumenConciliacionDto(
            facturas.Count(f => f.Estado == EstadoConciliacion.Cuadra),
            facturas.Count(f => f.Estado == EstadoConciliacion.Diferencia),
            facturas.Count(f => f.Estado == EstadoConciliacion.Parcial),
            facturas.Count(f => f.Estado == EstadoConciliacion.SinOrdenCompra),
            pendientes.Count,
            facturadasSinXml.Count,
            invalidas.Count);

        var ultimaSincronizacion = await db.OrdenesCompraWorldOffice.MaxAsync(o => (DateTime?)o.FechaSincronizacion, ct);
        var ultimaFactura = await db.FacturasProveedor.MaxAsync(f => (DateTime?)f.FechaRegistro, ct);

        return new ConciliacionDto(desde, hasta, resumen, facturas, pendientes, facturadasSinXml, invalidas, ultimaSincronizacion, ultimaFactura);
    }

    public async Task<DetalleFacturaConciliadaDto?> ObtenerDetalleAsync(int facturaId, CancellationToken ct = default)
    {
        var factura = await db.FacturasProveedor.AsNoTracking()
            .Include(f => f.Renglones)
            .FirstOrDefaultAsync(f => f.Id == facturaId, ct);
        if (factura is null) return null;

        // Se recalcula con el mismo contexto que la pantalla (las facturas anteriores del
        // proveedor afectan los saldos de las OC), no solo esta factura aislada.
        var (resultado, facturasPorCufe) = await CalcularAsync(factura.FechaEmision, factura.FechaEmision, ct);
        var conciliada = resultado.Facturas.FirstOrDefault(r => r.Factura.Cufe == factura.Cufe);
        if (conciliada is null) return null;

        // Si el cruce salió de una FC de WO con varias OC, se muestran los renglones de todas.
        var idsOc = conciliada.TodasLasOrdenes.Select(o => o.IdAsientoContable).ToList();
        var renglonesOc = new List<RenglonComparacionDto>();
        if (idsOc.Count > 0)
        {
            renglonesOc = await db.OrdenCompraWorldOfficeRenglones.AsNoTracking()
                .Where(r => idsOc.Contains(r.OrdenCompraWorldOffice!.IdAsientoContable))
                .OrderBy(r => r.OrdenCompraWorldOffice!.Numero).ThenBy(r => r.Orden)
                .Select(r => new RenglonComparacionDto(r.Codigo, r.Descripcion, r.Cantidad, r.ValorUnitario,
                    Math.Round(r.Cantidad * r.ValorUnitario, 2), r.PorcentajeIva))
                .ToListAsync(ct);
        }

        return new DetalleFacturaConciliadaDto(
            MapFactura(conciliada, facturasPorCufe[factura.Cufe]),
            factura.Renglones.OrderBy(r => r.Orden)
                .Select(r => new RenglonComparacionDto(null, r.Descripcion, r.Cantidad,
                    r.Cantidad == 0 ? r.Subtotal : Math.Round(r.Subtotal / r.Cantidad, 2), r.Subtotal, r.PorcentajeIva))
                .ToList(),
            renglonesOc);
    }

    // Trae las facturas del rango más las de la ventana anterior (DiasAntes): una factura de hace
    // un mes pudo haber consumido ya una OC, y sin ella esa OC volvería a parecer disponible.
    private async Task<(ResultadoConciliacion Resultado, Dictionary<string, int> FacturaIdPorCufe)> CalcularAsync(
        DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var desdeContexto = desde.AddDays(-Reglas.DiasAntes);

        var facturas = await db.FacturasProveedor.AsNoTracking()
            .Where(f => f.FechaEmision >= desdeContexto && f.FechaEmision <= hasta)
            .Select(f => new
            {
                f.Id, f.Cufe, f.Numero, f.TipoDocumento, f.FechaEmision, f.NitEmisor, f.NombreEmisor,
                f.NitReceptor, f.Subtotal, f.Iva, f.Total, f.ReferenciaOrden,
            })
            .ToListAsync(ct);

        // FC de WO (último recurso del cruce). Hasta 60 días después del rango: Contabilidad puede
        // registrar en noviembre la factura de una OC de octubre.
        var facturasCompra = await db.FacturasCompraWorldOffice.AsNoTracking()
            .Where(f => !f.Anulada && f.Fecha >= desdeContexto && f.Fecha <= hasta.AddDays(60) && f.ReferenciaOrdenes != null)
            .Select(f => new FacturaCompraConciliable(f.IdAsientoContable, f.Numero, f.Fecha, f.NitProveedor,
                f.NombreProveedor, f.DocumentoExterno, f.ReferenciaOrdenes))
            .ToListAsync(ct);
        var citadas = facturasCompra.SelectMany(f => f.OrdenesCitadas).Distinct().ToList();

        var ordenes = await db.OrdenesCompraWorldOffice.AsNoTracking()
            .Where(o => !o.Anulada
                        && ((o.Fecha >= desdeContexto.AddDays(-Reglas.DiasAntes) && o.Fecha <= hasta.AddDays(Reglas.DiasDespues))
                            // Las OC citadas en una FC entran aunque estén fuera de la ventana de fechas,
                            // para poder validar de qué proveedor son.
                            || citadas.Contains(o.Numero)))
            .Select(o => new OrdenCompraConciliable(o.IdAsientoContable, o.Numero, o.Fecha, o.NitProveedor,
                o.NombreProveedor, o.Subtotal, o.Iva))
            .ToListAsync(ct);

        var electronicas = facturas.Select(f => new FacturaElectronica(f.Cufe, f.Numero, f.TipoDocumento, f.FechaEmision,
            f.NitEmisor, f.NombreEmisor, f.NitReceptor, f.Subtotal, f.Iva, f.Total, f.ReferenciaOrden, []));

        return (MotorConciliacion.Conciliar(electronicas, ordenes, Reglas, facturasCompra), facturas.ToDictionary(f => f.Cufe, f => f.Id));
    }

    private static FacturaConciliadaDto MapFactura(ResultadoFactura r, int facturaId)
    {
        var ordenes = r.TodasLasOrdenes.ToList();
        return new(facturaId, r.Factura.Numero, r.Factura.FechaEmision, r.Factura.NitEmisor, r.Factura.NombreEmisor,
            r.Factura.Subtotal, r.Factura.Iva, r.Factura.Total, r.Factura.ReferenciaOrden, r.Estado,
            r.OrdenCompra?.Numero, r.OrdenCompra?.Fecha,
            ordenes.Count == 0 ? null : ordenes.Sum(o => o.Subtotal),
            ordenes.Count == 0 ? null : ordenes.Sum(o => o.Iva),
            r.DiferenciaSubtotal, r.DiferenciaIva, r.Detalle,
            ordenes.Select(o => o.Numero).ToList(),
            r.FacturaCompra?.Numero);
    }

    private static string Truncar(string valor, int maximo) => valor.Length <= maximo ? valor : valor[..maximo];
}
