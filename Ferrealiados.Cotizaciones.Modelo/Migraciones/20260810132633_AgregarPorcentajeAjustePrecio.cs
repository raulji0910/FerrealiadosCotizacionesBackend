using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ferrealiados.Cotizaciones.Modelo.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarPorcentajeAjustePrecio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostoBase",
                table: "ProductoProveedorPrecios",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PorcentajeAjuste",
                table: "ProductoProveedorPrecios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Para el histórico ya existente no hubo ajuste de porcentaje: CostoBase es igual al Costo final.
            migrationBuilder.Sql("UPDATE ProductoProveedorPrecios SET CostoBase = Costo;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostoBase",
                table: "ProductoProveedorPrecios");

            migrationBuilder.DropColumn(
                name: "PorcentajeAjuste",
                table: "ProductoProveedorPrecios");
        }
    }
}
