namespace Ferrealiados.Cotizaciones.Negocio.Interfaces;

// Aislado en su propia interfaz porque el proveedor InMemory de EF Core (usado en los tests
// unitarios) no soporta SQL SEQUENCE ni SQL crudo — así CotizacionService se puede testear con
// un fake/mock, mientras la garantía real de atomicidad se valida contra SQL Server real.
public interface IConsecutivoCotizacionProvider
{
    Task<int> ObtenerSiguienteAsync(CancellationToken ct = default);
}
