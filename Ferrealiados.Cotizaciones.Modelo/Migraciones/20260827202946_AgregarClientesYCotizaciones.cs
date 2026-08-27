using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ferrealiados.Cotizaciones.Modelo.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarClientesYCotizaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateSequence<int>(
                name: "SecuenciaConsecutivoCotizacion",
                schema: "dbo");

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cotizaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Consecutivo = table.Column<int>(type: "int", nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    ClienteNombreSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClienteNitSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FormaPago = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaEmision = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cotizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cotizaciones_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CotizacionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CotizacionId = table.Column<int>(type: "int", nullable: false),
                    ProductoProveedorPrecioId = table.Column<int>(type: "int", nullable: true),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    ProductoNombreSnapshot = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProductoCodigoSnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProveedorId = table.Column<int>(type: "int", nullable: false),
                    ProveedorNombreSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    FechaMarcado = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MarcadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CotizacionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CotizacionItems_Cotizaciones_CotizacionId",
                        column: x => x.CotizacionId,
                        principalTable: "Cotizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Nombre",
                table: "Clientes",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cotizaciones_ClienteId",
                table: "Cotizaciones",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizaciones_Codigo",
                table: "Cotizaciones",
                column: "Codigo",
                unique: true,
                filter: "[Estado] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizaciones_Consecutivo",
                table: "Cotizaciones",
                column: "Consecutivo",
                unique: true,
                filter: "[Consecutivo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizaciones_Estado",
                table: "Cotizaciones",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_CotizacionItems_CotizacionId",
                table: "CotizacionItems",
                column: "CotizacionId");

            migrationBuilder.CreateIndex(
                name: "IX_CotizacionItems_ProductoProveedorPrecioId",
                table: "CotizacionItems",
                column: "ProductoProveedorPrecioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CotizacionItems");

            migrationBuilder.DropTable(
                name: "Cotizaciones");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropSequence(
                name: "SecuenciaConsecutivoCotizacion",
                schema: "dbo");
        }
    }
}
