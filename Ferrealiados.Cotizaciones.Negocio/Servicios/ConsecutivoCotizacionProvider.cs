using Ferrealiados.Cotizaciones.Modelo;
using Ferrealiados.Cotizaciones.Negocio.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ferrealiados.Cotizaciones.Negocio.Servicios;

// Consume la SQL SEQUENCE definida en AppDbContext.OnModelCreating vía "NEXT VALUE FOR", que
// garantiza atomicidad real ante concurrencia (a diferencia del patrón read-then-write que usa
// ConfiguracionService, no apto para un consecutivo oficial de cotización).
public class ConsecutivoCotizacionProvider(AppDbContext db) : IConsecutivoCotizacionProvider
{
    // No se puede usar Database.SqlQuery<int>(...) aquí: EF Core lo ejecuta envuelto como
    // subconsulta al materializarlo, y SQL Server rechaza "NEXT VALUE FOR" dentro de una
    // subconsulta (error 11719) — solo se permite como sentencia de nivel superior. Por eso se
    // ejecuta con ADO.NET directo (ExecuteScalar), sin pasar por la canalización de LINQ de EF.
    public async Task<int> ObtenerSiguienteAsync(CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            using var command = connection.CreateCommand();
            // Nombre literal a propósito: debe coincidir con AppDbContext.SecuenciaConsecutivoCotizacion.
            command.CommandText = "SELECT NEXT VALUE FOR dbo.SecuenciaConsecutivoCotizacion";
            var resultado = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(resultado);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
