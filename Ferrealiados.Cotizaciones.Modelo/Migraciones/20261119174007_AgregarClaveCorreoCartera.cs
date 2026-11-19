using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ferrealiados.Cotizaciones.Modelo.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarClaveCorreoCartera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClaveCorreoActualizada",
                table: "ConfiguracionCartera",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveCorreoActualizadaPor",
                table: "ConfiguracionCartera",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveCorreoCifrada",
                table: "ConfiguracionCartera",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveCorreoCuenta",
                table: "ConfiguracionCartera",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ConfiguracionCartera",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "ClaveCorreoActualizada", "ClaveCorreoActualizadaPor", "ClaveCorreoCifrada", "ClaveCorreoCuenta" },
                values: new object[] { null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaveCorreoActualizada",
                table: "ConfiguracionCartera");

            migrationBuilder.DropColumn(
                name: "ClaveCorreoActualizadaPor",
                table: "ConfiguracionCartera");

            migrationBuilder.DropColumn(
                name: "ClaveCorreoCifrada",
                table: "ConfiguracionCartera");

            migrationBuilder.DropColumn(
                name: "ClaveCorreoCuenta",
                table: "ConfiguracionCartera");
        }
    }
}
