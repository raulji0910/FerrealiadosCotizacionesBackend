using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ferrealiados.Cotizaciones.Modelo.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarFacturasCompraWorldOffice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacturasCompraWorldOffice",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAsientoContable = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    NitProveedor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NombreProveedor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DocumentoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenciaOrdenes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Anulada = table.Column<bool>(type: "bit", nullable: false),
                    FechaSincronizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasCompraWorldOffice", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacturasCompraWorldOffice_Fecha",
                table: "FacturasCompraWorldOffice",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasCompraWorldOffice_IdAsientoContable",
                table: "FacturasCompraWorldOffice",
                column: "IdAsientoContable",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturasCompraWorldOffice_NitProveedor",
                table: "FacturasCompraWorldOffice",
                column: "NitProveedor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacturasCompraWorldOffice");
        }
    }
}
