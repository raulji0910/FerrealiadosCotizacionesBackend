namespace Ferrealiados.Cotizaciones.Negocio.DTOs;

public record PaginaResultado<T>(IReadOnlyList<T> Items, int Total, int Pagina, int TamanoPagina);
