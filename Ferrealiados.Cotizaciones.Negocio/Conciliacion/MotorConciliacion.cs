namespace Ferrealiados.Cotizaciones.Negocio.Conciliacion;

// Orden de compra tal como sale de World Office. Subtotal = SUM(Cantidad * MontoMonetarioUnitario),
// NO SUM(TotalRenglon): en Ferrealiados TotalRenglon trae un valor con margen (1.16-1.25x), no el
// costo que se le paga al proveedor. Las OC no llevan retenciones, solo IVA.
public sealed record OrdenCompraConciliable(
    int IdAsientoContable,
    int Numero,
    DateOnly Fecha,
    string NitProveedor,
    string NombreProveedor,
    decimal Subtotal,
    decimal Iva);

public enum EstadoConciliacion
{
    Cuadra,
    Diferencia,
    Parcial,
    SinOrdenCompra,
}

public sealed record ResultadoFactura(
    FacturaElectronica Factura,
    EstadoConciliacion Estado,
    OrdenCompraConciliable? OrdenCompra,
    decimal? DiferenciaSubtotal,
    decimal? DiferenciaIva,
    string Detalle);

public sealed record OrdenPendiente(OrdenCompraConciliable OrdenCompra, decimal SaldoSubtotal);

public sealed record ResultadoConciliacion(
    IReadOnlyList<ResultadoFactura> Facturas,
    IReadOnlyList<OrdenPendiente> OrdenesPendientes);

// PorcentajeMinimoParcial: para tratar una factura como entrega parcial, el saldo que deja en la OC
// debe ser al menos ese % de la OC. Una factura 1-2% por debajo de la OC casi siempre es un precio
// distinto (caso real Imporinox, -435 sobre 73.109), no media entrega.
public sealed record ReglasConciliacion(
    decimal ToleranciaPesos = 100m,
    int DiasAntes = 90,
    int DiasDespues = 15,
    decimal PorcentajeMinimoParcial = 0.05m);

// Cruce XML del proveedor <-> OC de World Office. Orden de prioridad:
//   1. Número de OC que el proveedor puso en el XML (OrderReference), si es del mismo NIT.
//   2. Mismo NIT + mismo subtotal e IVA (± tolerancia) dentro de la ventana de fechas.
//   3. Entregas parciales: la factura cabe en el saldo de una OC del mismo NIT.
//   4. Hay OC del proveedor pero ningún valor encaja -> Diferencia contra la más cercana.
//   5. Ninguna OC del proveedor en la ventana -> SinOrdenCompra.
// La tolerancia existe porque WO guarda precios digitados con IVA y divididos por 1,19 (decimales
// largos): el XML y la OC difieren 1-2 pesos de redondeo aunque sea la misma compra.
public static class MotorConciliacion
{
    public static ResultadoConciliacion Conciliar(
        IEnumerable<FacturaElectronica> facturas,
        IEnumerable<OrdenCompraConciliable> ordenes,
        ReglasConciliacion? reglas = null)
    {
        reglas ??= new ReglasConciliacion();

        var porNit = ordenes
            .GroupBy(o => LectorFacturaElectronica.NormalizarNit(o.NitProveedor))
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Fecha).ThenBy(o => o.Numero).ToList());
        var saldos = porNit.Values.SelectMany(l => l).ToDictionary(o => o.IdAsientoContable, o => o.Subtotal);

        var pendientes = facturas
            .Where(f => f.EsFactura)
            .OrderBy(f => f.FechaEmision).ThenBy(f => f.Numero, StringComparer.Ordinal)
            .ToList();
        var resultados = new Dictionary<FacturaElectronica, ResultadoFactura>();

        List<OrdenCompraConciliable> Candidatas(FacturaElectronica f)
            => porNit.TryGetValue(LectorFacturaElectronica.NormalizarNit(f.NitEmisor), out var lista)
                ? lista.Where(o => o.Fecha >= f.FechaEmision.AddDays(-reglas.DiasAntes)
                                && o.Fecha <= f.FechaEmision.AddDays(reglas.DiasDespues)).ToList()
                : [];

        bool Igual(decimal a, decimal b) => Math.Abs(a - b) <= reglas.ToleranciaPesos;
        bool Intacta(OrdenCompraConciliable o) => saldos[o.IdAsientoContable] == o.Subtotal;
        bool DejaSaldoParcial(FacturaElectronica f, OrdenCompraConciliable o)
        {
            var restante = saldos[o.IdAsientoContable] - f.Subtotal;
            return restante > reglas.ToleranciaPesos && restante >= o.Subtotal * reglas.PorcentajeMinimoParcial;
        }

        void Asignar(FacturaElectronica f, OrdenCompraConciliable o, EstadoConciliacion estado, string detalle)
        {
            var saldoAntes = saldos[o.IdAsientoContable];
            saldos[o.IdAsientoContable] = Math.Max(0m, saldoAntes - f.Subtotal);
            // El IVA solo se compara cuando la factura cubre la OC completa; en parciales no hay
            // contra qué (la OC no reparte el IVA por entrega).
            var ocCompleta = saldoAntes == o.Subtotal && estado != EstadoConciliacion.Parcial;
            resultados[f] = new ResultadoFactura(f, estado, o,
                DiferenciaSubtotal: f.Subtotal - saldoAntes,
                DiferenciaIva: ocCompleta ? f.Iva - o.Iva : null,
                detalle);
        }

        // 1. OC referenciada en el XML.
        foreach (var f in pendientes.Where(f => f.NumeroOrdenReferenciada is not null).ToList())
        {
            var oc = Candidatas(f).FirstOrDefault(o => o.Numero == f.NumeroOrdenReferenciada)
                     ?? porNit.GetValueOrDefault(LectorFacturaElectronica.NormalizarNit(f.NitEmisor))?
                         .FirstOrDefault(o => o.Numero == f.NumeroOrdenReferenciada);
            if (oc is null) continue;

            var saldo = saldos[oc.IdAsientoContable];
            if (Igual(f.Subtotal, saldo) && (!Intacta(oc) || Igual(f.Iva, oc.Iva)))
                Asignar(f, oc, EstadoConciliacion.Cuadra, $"OC {oc.Numero} indicada por el proveedor en la factura.");
            else if (DejaSaldoParcial(f, oc))
                Asignar(f, oc, EstadoConciliacion.Parcial, $"OC {oc.Numero} indicada por el proveedor; factura parcial.");
            else
                Asignar(f, oc, EstadoConciliacion.Diferencia, $"OC {oc.Numero} indicada por el proveedor, pero los valores no coinciden.");
            pendientes.Remove(f);
        }

        // 2. Coincidencia exacta NIT + subtotal + IVA contra una OC sin facturar.
        foreach (var f in pendientes.ToList())
        {
            var oc = Candidatas(f)
                .Where(o => Intacta(o) && Igual(f.Subtotal, o.Subtotal) && Igual(f.Iva, o.Iva))
                .OrderBy(o => Math.Abs(o.Fecha.DayNumber - f.FechaEmision.DayNumber))
                .FirstOrDefault();
            if (oc is null) continue;

            Asignar(f, oc, EstadoConciliacion.Cuadra, $"Mismo proveedor y valores que la OC {oc.Numero}.");
            pendientes.Remove(f);
        }

        // 3 y 4. Parciales (la factura completa o cabe en el saldo de una OC) y diferencias.
        foreach (var f in pendientes)
        {
            var candidatas = Candidatas(f);
            if (candidatas.Count == 0)
            {
                resultados[f] = new ResultadoFactura(f, EstadoConciliacion.SinOrdenCompra, null, null, null,
                    "No hay OC de este proveedor en la ventana de fechas.");
                continue;
            }

            var completaSaldo = candidatas
                .Where(o => !Intacta(o) && saldos[o.IdAsientoContable] > 0 && Igual(f.Subtotal, saldos[o.IdAsientoContable]))
                .FirstOrDefault();
            if (completaSaldo is not null)
            {
                Asignar(f, completaSaldo, EstadoConciliacion.Cuadra, $"Completa el saldo de la OC {completaSaldo.Numero} (entrega parcial).");
                continue;
            }

            var cabe = candidatas
                .Where(o => DejaSaldoParcial(f, o))
                .OrderBy(o => saldos[o.IdAsientoContable] - f.Subtotal)
                .FirstOrDefault();
            if (cabe is not null)
            {
                Asignar(f, cabe, EstadoConciliacion.Parcial, $"Posible entrega parcial de la OC {cabe.Numero}: confirmar productos.");
                continue;
            }

            var cercana = candidatas.OrderBy(o => Math.Abs(f.Subtotal - saldos[o.IdAsientoContable])).First();
            var diferencia = Math.Abs(f.Subtotal - saldos[cercana.IdAsientoContable]);
            if (Intacta(cercana) && diferencia < cercana.Subtotal * reglas.PorcentajeMinimoParcial)
            {
                // Tan cerca que es esa OC con otro precio (caso Imporinox): se liga y se consume,
                // para que no quede además como "OC sin factura".
                Asignar(f, cercana, EstadoConciliacion.Diferencia, $"Corresponde a la OC {cercana.Numero}, pero el proveedor facturó un valor distinto.");
                saldos[cercana.IdAsientoContable] = 0m;
                continue;
            }

            resultados[f] = new ResultadoFactura(f, EstadoConciliacion.Diferencia, cercana,
                f.Subtotal - saldos[cercana.IdAsientoContable], f.Iva - cercana.Iva,
                $"El valor no coincide con ninguna OC del proveedor; la más cercana es la {cercana.Numero}.");
        }

        var ordenesPendientes = porNit.Values.SelectMany(l => l)
            .Where(o => saldos[o.IdAsientoContable] > reglas.ToleranciaPesos)
            .Select(o => new OrdenPendiente(o, saldos[o.IdAsientoContable]))
            .OrderBy(p => p.OrdenCompra.Fecha)
            .ToList();

        return new ResultadoConciliacion(
            resultados.Values.OrderBy(r => r.Factura.FechaEmision).ThenBy(r => r.Factura.Numero, StringComparer.Ordinal).ToList(),
            ordenesPendientes);
    }
}
