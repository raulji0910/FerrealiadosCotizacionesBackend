using Ferrealiados.Cotizaciones.Negocio.DTOs;

namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

public interface ICotizacionPdfBuilder
{
    byte[] Generar(CotizacionDetalleDto cotizacion);
}
