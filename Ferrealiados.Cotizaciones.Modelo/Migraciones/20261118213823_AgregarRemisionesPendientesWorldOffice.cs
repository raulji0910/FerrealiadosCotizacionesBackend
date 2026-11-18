using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ferrealiados.Cotizaciones.Modelo.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarRemisionesPendientesWorldOffice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemisionesPendientesWorldOffice",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdAsientoContable = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<long>(type: "bigint", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NombreCliente = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Facturado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Pagado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Otros = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaSincronizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemisionesPendientesWorldOffice", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemisionesPendientesWorldOffice_IdAsientoContable",
                table: "RemisionesPendientesWorldOffice",
                column: "IdAsientoContable",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemisionesPendientesWorldOffice_Nit",
                table: "RemisionesPendientesWorldOffice",
                column: "Nit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemisionesPendientesWorldOffice");
        }
    }
}
