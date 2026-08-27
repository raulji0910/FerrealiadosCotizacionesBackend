using Ferrealiados.Cotizaciones.Negocio.DTOs;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ferrealiados.Cotizaciones.Negocio.Documentos;

// Genera el PDF de una cotización ya emitida. No muestra el proveedor por ítem (información
// interna, no debe verla el cliente) — solo Producto, Cantidad, Precio unitario y Subtotal.
public class CotizacionPdfBuilder(IOptions<DatosEmpresaOptions> datosEmpresaOptions) : ICotizacionPdfBuilder
{
    private const string AzulMarca = "#0A578D";
    private const string NaranjaMarca = "#F38138";

    private readonly DatosEmpresaOptions empresa = datosEmpresaOptions.Value;

    public byte[] Generar(CotizacionDetalleDto cotizacion)
    {
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComponerEncabezado(c, cotizacion));
                page.Content().Element(c => ComponerContenido(c, cotizacion));
                page.Footer().Element(c => ComponerPie(c, cotizacion));
            });
        });

        return documento.GeneratePdf();
    }

    private void ComponerEncabezado(IContainer container, CotizacionDetalleDto cotizacion)
    {
        container.Background(AzulMarca).Padding(15).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(empresa.Nombre).FontSize(18).Bold().FontColor(Colors.White);
                if (!string.IsNullOrWhiteSpace(empresa.Nit))
                    col.Item().Text($"NIT: {empresa.Nit}").FontColor(Colors.White);
            });

            row.ConstantItem(220).Column(col =>
            {
                col.Item().AlignRight().Text($"COTIZACIÓN N.° {cotizacion.Consecutivo}").FontSize(14).Bold().FontColor(Colors.White);
                col.Item().AlignRight().Text($"Fecha: {cotizacion.FechaEmision:dd/MM/yyyy}").FontColor(Colors.White);
            });
        });
    }

    private void ComponerContenido(IContainer container, CotizacionDetalleDto cotizacion)
    {
        container.PaddingTop(20).Column(col =>
        {
            col.Spacing(15);

            col.Item().Column(datos =>
            {
                datos.Item().Text("Cliente").Bold().FontSize(11);
                datos.Item().Text(cotizacion.ClienteNombre ?? "-");
                if (!string.IsNullOrWhiteSpace(cotizacion.ClienteNit))
                    datos.Item().Text($"NIT: {cotizacion.ClienteNit}");
                if (!string.IsNullOrWhiteSpace(cotizacion.FormaPago))
                    datos.Item().Text($"Forma de pago: {cotizacion.FormaPago}");
            });

            col.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(columnas =>
                {
                    columnas.RelativeColumn(4);
                    columnas.RelativeColumn(1);
                    columnas.RelativeColumn(2);
                    columnas.RelativeColumn(2);
                });

                tabla.Header(header =>
                {
                    EncabezadoCelda(header.Cell(), "Producto");
                    EncabezadoCelda(header.Cell(), "Cant.");
                    EncabezadoCelda(header.Cell(), "Precio unitario");
                    EncabezadoCelda(header.Cell(), "Subtotal");
                });

                foreach (var item in cotizacion.Items)
                {
                    var nombreProducto = item.ProductoCodigo is null
                        ? item.ProductoNombre
                        : $"{item.ProductoNombre} ({item.ProductoCodigo})";

                    CeldaTexto(tabla.Cell(), nombreProducto);
                    CeldaTexto(tabla.Cell(), item.Cantidad.ToString());
                    CeldaTexto(tabla.Cell(), FormatearMoneda(item.PrecioUnitario));
                    CeldaTexto(tabla.Cell(), FormatearMoneda(item.Subtotal));
                }
            });

            col.Item().AlignRight().Background(NaranjaMarca).Padding(10).Text($"TOTAL: {FormatearMoneda(cotizacion.Total)}")
                .FontSize(13).Bold().FontColor(Colors.White);

            if (!string.IsNullOrWhiteSpace(cotizacion.Nota))
            {
                col.Item().Column(nota =>
                {
                    nota.Item().Text("Nota").Bold();
                    nota.Item().Text(cotizacion.Nota);
                });
            }
        });
    }

    private void ComponerPie(IContainer container, CotizacionDetalleDto cotizacion)
    {
        container.PaddingTop(10).Column(col =>
        {
            col.Item().LineHorizontal(0.5f);
            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span(empresa.Nombre).Bold();
                    if (!string.IsNullOrWhiteSpace(empresa.Direccion))
                        text.Span($" · {empresa.Direccion}");
                    if (!string.IsNullOrWhiteSpace(empresa.Ciudad))
                        text.Span($" · {empresa.Ciudad}");
                    if (!string.IsNullOrWhiteSpace(empresa.Telefono))
                        text.Span($" · Tel: {empresa.Telefono}");
                });
                row.ConstantItem(80).AlignRight().Text($"Código: {cotizacion.Codigo}").FontSize(8);
            });
        });
    }

    private static void EncabezadoCelda(IContainer container, string texto)
        => container.Background("#E8EEF3").Padding(5).Text(texto).Bold();

    private static void CeldaTexto(IContainer container, string texto)
        => container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(texto);

    private static string FormatearMoneda(decimal valor)
        => $"$ {valor.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"))}";
}
