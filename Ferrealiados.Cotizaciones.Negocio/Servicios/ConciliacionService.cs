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

        var resumen = new ResumenConciliacionDto(
            facturas.Count(f => f.Estado == EstadoConciliacion.Cuadra),
            facturas.Count(f => f.Estado == EstadoConciliacion.Diferencia),
            facturas.Count(f => f.Estado == EstadoConciliacion.Parcial),
            facturas.Count(f => f.Estado == EstadoConciliacion.SinOrdenCompra),
            pendientes.Count);

        var ultimaSincronizacion = await db.OrdenesCompraWorldOffice.MaxAsync(o => (DateTime?)o.FechaSincronizacion, ct);
        var ultimaFactura = await db.FacturasProveedor.MaxAsync(f => (DateTime?)f.FechaRegistro, ct);

        return new ConciliacionDto(desde, hasta, resumen, facturas, pendientes, ultimaSincronizacion, ultimaFactura);
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

        var renglonesOc = new List<RenglonComparacionDto>();
        if (conciliada.OrdenCompra is not null)
        {
            renglonesOc = await db.OrdenCompraWorldOfficeRenglones.AsNoTracking()
                .Where(r => r.OrdenCompraWorldOffice!.IdAsientoContable == conciliada.OrdenCompra.IdAsientoContable)
                .OrderBy(r => r.Orden)
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

        var ordenes = await db.OrdenesCompraWorldOffice.AsNoTracking()
            .Where(o => !o.Anulada
                        && o.Fecha >= desdeContexto.AddDays(-Reglas.DiasAntes)
                        && o.Fecha <= hasta.AddDays(Reglas.DiasDespues))
            .Select(o => new OrdenCompraConciliable(o.IdAsientoContable, o.Numero, o.Fecha, o.NitProveedor,
                o.NombreProveedor, o.Subtotal, o.Iva))
            .ToListAsync(ct);

        var electronicas = facturas.Select(f => new FacturaElectronica(f.Cufe, f.Numero, f.TipoDocumento, f.FechaEmision,
            f.NitEmisor, f.NombreEmisor, f.NitReceptor, f.Subtotal, f.Iva, f.Total, f.ReferenciaOrden, []));

        return (MotorConciliacion.Conciliar(electronicas, ordenes, Reglas), facturas.ToDictionary(f => f.Cufe, f => f.Id));
    }

    private static FacturaConciliadaDto MapFactura(ResultadoFactura r, int facturaId)
        => new(facturaId, r.Factura.Numero, r.Factura.FechaEmision, r.Factura.NitEmisor, r.Factura.NombreEmisor,
            r.Factura.Subtotal, r.Factura.Iva, r.Factura.Total, r.Factura.ReferenciaOrden, r.Estado,
            r.OrdenCompra?.Numero, r.OrdenCompra?.Fecha, r.OrdenCompra?.Subtotal, r.OrdenCompra?.Iva,
            r.DiferenciaSubtotal, r.DiferenciaIva, r.Detalle);

    private static string Truncar(string valor, int maximo) => valor.Length <= maximo ? valor : valor[..maximo];
}
