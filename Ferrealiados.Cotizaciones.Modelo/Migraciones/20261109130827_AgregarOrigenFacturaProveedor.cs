using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ferrealiados.Cotizaciones.Modelo.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarOrigenFacturaProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "FacturasProveedor",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Correo");

            // Las que entraron con la carga única del Sincronizador (--carpeta) se reconocen por el
            // asunto que les pone CarpetaFacturasReader.
            migrationBuilder.Sql(
                "UPDATE FacturasProveedor SET Origen = 'Carpeta' WHERE CorreoAsunto LIKE 'Carga desde carpeta:%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Origen",
                table: "FacturasProveedor");
        }
    }
}
