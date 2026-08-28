using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ferrealiados.Cotizaciones.Modelo.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarContactoEmailDescuentoIva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IvaSnapshot",
                table: "CotizacionItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClienteCiudadSnapshot",
                table: "Cotizaciones",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClienteContactoSnapshot",
                table: "Cotizaciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClienteDireccionSnapshot",
                table: "Cotizaciones",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClienteEmailSnapshot",
                table: "Cotizaciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Descuento",
                table: "Cotizaciones",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Contacto",
                table: "Clientes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Clientes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IvaSnapshot",
                table: "CotizacionItems");

            migrationBuilder.DropColumn(
                name: "ClienteCiudadSnapshot",
                table: "Cotizaciones");

            migrationBuilder.DropColumn(
                name: "ClienteContactoSnapshot",
                table: "Cotizaciones");

            migrationBuilder.DropColumn(
                name: "ClienteDireccionSnapshot",
                table: "Cotizaciones");

            migrationBuilder.DropColumn(
                name: "ClienteEmailSnapshot",
                table: "Cotizaciones");

            migrationBuilder.DropColumn(
                name: "Descuento",
                table: "Cotizaciones");

            migrationBuilder.DropColumn(
                name: "Contacto",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Clientes");
        }
    }
}
