using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ferrealiados.Cotizaciones.Modelo.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarCarteraClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientesCartera",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CorreosWorldOffice = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    CorreosCobro = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Contacto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FechaActualizacionContacto = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientesCartera", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionCartera",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Remitente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NombreRemitente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Copias = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SaldoMinimo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Asunto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Cuerpo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DatosPago = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionCartera", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnviosCartera",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NombreCliente = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Para = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Copias = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Asunto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Documentos = table.Column<int>(type: "int", nullable: false),
                    Saldo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaldoVencido = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Exitoso = table.Column<bool>(type: "bit", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnviosCartera", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartidasCartera",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdDocumentoWorldOffice = table.Column<int>(type: "int", nullable: false),
                    CodigoCuenta = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NombreCliente = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TipoDocumento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prefijo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Numero = table.Column<long>(type: "bigint", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Vencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    Saldo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EsRemision = table.Column<bool>(type: "bit", nullable: false),
                    FechaSincronizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartidasCartera", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionCartera",
                columns: new[] { "Id", "Asunto", "Copias", "Cuerpo", "DatosPago", "NombreRemitente", "Remitente", "SaldoMinimo" },
                values: new object[] { 1, "Estado de cuenta FERREALIADOS JV - {cliente} - corte {fechaCorte}", "ventas@ferrealiadosjv.com; gerencia.comercial@jimaco.com.co", "Señores {cliente}:\n\nCordial saludo. Les compartimos el estado de cuenta con FERREALIADOS JV con corte al {fechaCorte}, por un saldo total de {total}.\n\nAgradecemos su pronta respuesta ya que, revisando nuestros extractos bancarios, no contamos con soportes de pago para los siguientes documentos. Si ya realizaron el pago, por favor envíennos el soporte respondiendo este correo.", null, "Cartera FERREALIADOS JV", "cartera@ferrealiadosjv.com", 10000m });

            migrationBuilder.CreateIndex(
                name: "IX_ClientesCartera_Nit",
                table: "ClientesCartera",
                column: "Nit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnviosCartera_Fecha",
                table: "EnviosCartera",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosCartera_Nit",
                table: "EnviosCartera",
                column: "Nit");

            migrationBuilder.CreateIndex(
                name: "IX_PartidasCartera_IdDocumentoWorldOffice_CodigoCuenta",
                table: "PartidasCartera",
                columns: new[] { "IdDocumentoWorldOffice", "CodigoCuenta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartidasCartera_Nit",
                table: "PartidasCartera",
                column: "Nit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientesCartera");

            migrationBuilder.DropTable(
                name: "ConfiguracionCartera");

            migrationBuilder.DropTable(
                name: "EnviosCartera");

            migrationBuilder.DropTable(
                name: "PartidasCartera");
        }
    }
}
