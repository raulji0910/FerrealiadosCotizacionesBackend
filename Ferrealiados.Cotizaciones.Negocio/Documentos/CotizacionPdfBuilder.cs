using System.Globalization;
using System.Reflection;
using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ferrealiados.Cotizaciones.Negocio.Documentos;

// Genera el PDF de una cotización ya emitida, siguiendo el formato histórico de la empresa
// (encabezado con logo + datos de la empresa, grilla de datos del cliente, tabla de ítems,
// observaciones fijas + desglose de totales). No muestra el proveedor por ítem (información
// interna, no debe verla el cliente) — solo Producto, Cantidad, Precio unitario y Subtotal.
public class CotizacionPdfBuilder(IOptions<DatosEmpresaOptions> datosEmpresaOptions) : ICotizacionPdfBuilder
{
    private const string AzulMarca = "#0A578D";
    private const string NaranjaMarca = "#F38138";
    private const string AzulClaro = "#EAF1F5";
    private const string GrisTexto = "#4A4A4A";

    private static readonly CultureInfo CulturaMoneda = CultureInfo.GetCultureInfo("es-CO");

    private static readonly string[] Observaciones =
    [
        "Revise cuidadosamente su mercancía, no se acepta devolución, ni cambios.",
        "Material cortado o fabricado no tiene devolución, ni cambio.",
        "La disponibilidad de los elementos de la oferta está sujeta al inventario al momento de su compra.",
        "Los precios pueden cambiar en cualquier momento, sin previo aviso y según sea el comportamiento de las divisas.",
        "Entrega 1 día hábil, luego de la orden de compra (sujeto a ciudad)."
    ];

    private static readonly byte[] LogoBytes = CargarLogo();

    private readonly DatosEmpresaOptions empresa = datosEmpresaOptions.Value;

    public byte[] Generar(CotizacionDetalleDto cotizacion)
    {
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(GrisTexto));

                page.Header().Element(c => ComponerEncabezado(c, cotizacion));
                page.Content().PaddingTop(15).Element(c => ComponerContenido(c, cotizacion));
            });
        });

        return documento.GeneratePdf();
    }

    private void ComponerEncabezado(IContainer container, CotizacionDetalleDto cotizacion)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(70).Image(LogoBytes);

                row.RelativeItem().PaddingLeft(10).Column(datos =>
                {
                    datos.Item().Text(empresa.Nombre).FontSize(20).Bold().FontColor(AzulMarca);
                    if (!string.IsNullOrWhiteSpace(empresa.Nit))
                        datos.Item().Text($"NIT {empresa.Nit}").FontSize(9).Bold();
                    if (!string.IsNullOrWhiteSpace(empresa.Direccion))
                        datos.Item().Text(empresa.Direccion).FontSize(9).Bold();
                    if (!string.IsNullOrWhiteSpace(empresa.Ciudad))
                        datos.Item().Text(empresa.Ciudad).FontSize(9).Bold();
                });

                row.ConstantItem(150).Column(badge =>
                {
                    badge.Item().Background(AzulMarca).Padding(6).AlignCenter()
                        .Text("COTIZACIÓN").FontColor(Colors.White).Bold().FontSize(11);
                    badge.Item().BorderColor(AzulMarca).Border(1).PaddingVertical(6).AlignCenter()
                        .Text(cotizacion.ConsecutivoFormateado ?? "-").FontColor(AzulMarca).Bold().FontSize(11);
                });
            });

            col.Item().PaddingTop(12).Element(c => ComponerGrillaCliente(c, cotizacion));
        });
    }

    private void ComponerGrillaCliente(IContainer container, CotizacionDetalleDto cotizacion)
    {
        container.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.RelativeColumn(2);
                columnas.RelativeColumn(3);
                columnas.RelativeColumn(2);
                columnas.RelativeColumn(2);
                columnas.RelativeColumn(2);
            });

            EtiquetaCelda(tabla.Cell(), "NIT");
            EtiquetaCelda(tabla.Cell(), "CLIENTE");
            EtiquetaCelda(tabla.Cell(), "CONTACTO");
            EtiquetaCelda(tabla.Cell(), "FECHA");
            EtiquetaCelda(tabla.Cell(), "CIUDAD");

            ValorCelda(tabla.Cell(), cotizacion.ClienteNit);
            ValorCelda(tabla.Cell(), cotizacion.ClienteNombre);
            ValorCelda(tabla.Cell(), cotizacion.ClienteContacto);
            ValorCelda(tabla.Cell(), cotizacion.FechaEmision?.ToString("dd/MM/yyyy"));
            ValorCelda(tabla.Cell(), cotizacion.ClienteCiudad);

            EtiquetaCelda(tabla.Cell(), "TEL");
            EtiquetaCelda(tabla.Cell(), "DIRECCIÓN");
            EtiquetaCelda(tabla.Cell(), "EMAIL");
            EtiquetaCelda(tabla.Cell(), "DESCUENTO");
            EtiquetaCelda(tabla.Cell(), "FORMA DE PAGO");

            ValorCelda(tabla.Cell(), null); // El teléfono del cliente no se pide hoy en el sistema
            ValorCelda(tabla.Cell(), cotizacion.ClienteDireccion);
            ValorCelda(tabla.Cell(), cotizacion.ClienteEmail);
            ValorCelda(tabla.Cell(), cotizacion.Descuento > 0 ? FormatearMoneda(cotizacion.Descuento) : null);
            ValorCelda(tabla.Cell(), cotizacion.FormaPago);
        });
    }

    private void ComponerContenido(IContainer container, CotizacionDetalleDto cotizacion)
    {
        container.Column(col =>
        {
            col.Spacing(15);

            col.Item().Element(c => ComponerTablaItems(c, cotizacion));
            col.Item().Element(c => ComponerPie(c, cotizacion));
        });
    }

    private void ComponerTablaItems(IContainer container, CotizacionDetalleDto cotizacion)
    {
        container.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.RelativeColumn(1);
                columnas.RelativeColumn(4.2f);
                columnas.RelativeColumn(1.3f);
                columnas.RelativeColumn(1.8f);
                columnas.RelativeColumn(1);
                columnas.RelativeColumn(1.8f);
            });

            tabla.Header(header =>
            {
                EncabezadoItemCelda(header.Cell(), "Ítem");
                EncabezadoItemCelda(header.Cell(), "Descripción");
                EncabezadoItemCelda(header.Cell(), "Cantidad");
                EncabezadoItemCelda(header.Cell(), "Valor unitario");
                EncabezadoItemCelda(header.Cell(), "IVA");
                EncabezadoItemCelda(header.Cell(), "Valor Total");
            });

            for (var i = 0; i < cotizacion.Items.Count; i++)
            {
                var item = cotizacion.Items[i];
                var fondo = i % 2 == 0 ? AzulClaro : "#FFFFFF";
                var nombreProducto = item.ProductoCodigo is null
                    ? item.ProductoNombre
                    : $"{item.ProductoNombre} ({item.ProductoCodigo})";
                // Solo informativo — no participa en el cálculo del precio del ítem, que sigue
                // siendo el costo con el % de ajuste ya aplicado, sin IVA (ver decisión de diseño
                // documentada en CotizacionItem.PrecioUnitario).
                var iva = item.IvaSnapshot is null ? "-" : $"{item.IvaSnapshot}%";

                FilaItemCelda(tabla.Cell(), fondo, (i + 1).ToString(), alinearDerecha: false);
                FilaItemCelda(tabla.Cell(), fondo, nombreProducto, alinearDerecha: false);
                FilaItemCelda(tabla.Cell(), fondo, item.Cantidad.ToString(), alinearDerecha: true);
                FilaItemCelda(tabla.Cell(), fondo, FormatearMoneda(item.PrecioUnitario), alinearDerecha: true);
                FilaItemCelda(tabla.Cell(), fondo, iva, alinearDerecha: true);
                FilaItemCelda(tabla.Cell(), fondo, FormatearMoneda(item.Subtotal), alinearDerecha: true);
            }
        });
    }

    private void ComponerPie(IContainer container, CotizacionDetalleDto cotizacion)
    {
        container.Row(row =>
        {
            row.RelativeItem(3).Column(col =>
            {
                col.Item().Background(AzulMarca).Padding(5).Text("Observaciones").FontColor(Colors.White).Bold();
                col.Item().PaddingTop(5).Column(obs =>
                {
                    foreach (var texto in Observaciones)
                        obs.Item().Text($"* {texto}").FontSize(7.5f);

                    if (!string.IsNullOrWhiteSpace(cotizacion.Nota))
                        obs.Item().PaddingTop(3).Text($"* Nota: {cotizacion.Nota}").FontSize(7.5f).Bold();
                });
            });

            row.ConstantItem(15);

            row.RelativeItem(2).Column(col =>
            {
                FilaTotal(col, "Subtotal ítems", cotizacion.Total);
                if (cotizacion.Descuento > 0)
                {
                    FilaTotal(col, "Descuento", -cotizacion.Descuento);
                    FilaTotal(col, "Subtotal", cotizacion.Subtotal);
                }

                foreach (var tramo in cotizacion.IvaDesglose)
                    FilaTotal(col, $"IVA ({tramo.Tarifa}%)", tramo.Valor);

                col.Item().PaddingTop(4).Background(NaranjaMarca).Padding(8).Row(total =>
                {
                    total.RelativeItem().Text("TOTAL").FontColor(Colors.White).Bold().FontSize(12);
                    total.RelativeItem().AlignRight().Text(FormatearMoneda(cotizacion.TotalGeneral)).FontColor(Colors.White).Bold().FontSize(12);
                });
            });
        });
    }

    private static void FilaTotal(ColumnDescriptor col, string etiqueta, decimal valor)
    {
        col.Item().Row(fila =>
        {
            fila.RelativeItem().Text(etiqueta).FontSize(9);
            fila.RelativeItem().AlignRight().Text(FormatearMoneda(valor)).FontSize(9).Bold();
        });
    }

    private static void EtiquetaCelda(IContainer container, string texto)
        => container.Background(AzulMarca).Padding(4).Text(texto).FontColor(Colors.White).Bold().FontSize(8);

    private static void ValorCelda(IContainer container, string? texto)
        => container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(texto ?? "-").FontSize(8.5f);

    private static void EncabezadoItemCelda(IContainer container, string texto)
        => container.Background(AzulMarca).Padding(6).Text(texto).FontColor(Colors.White).Bold().FontSize(9);

    private static void FilaItemCelda(IContainer container, string fondo, string texto, bool alinearDerecha)
    {
        var celda = container.Background(fondo).Padding(6);
        var celdaAlineada = alinearDerecha ? celda.AlignRight() : celda;
        celdaAlineada.Text(texto).FontSize(8.5f);
    }

    private static string FormatearMoneda(decimal valor)
    {
        var signo = valor < 0 ? "-" : "";
        return $"{signo}$ {Math.Abs(valor).ToString("N0", CulturaMoneda)}";
    }

    private static byte[] CargarLogo()
    {
        var assembly = typeof(CotizacionPdfBuilder).Assembly;
        var nombreRecurso = assembly.GetManifestResourceNames().First(n => n.EndsWith("ferrealiados-logo.png", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(nombreRecurso)!;
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    }
}
