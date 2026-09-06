using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionSitio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ColorPrimario = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColorPrimarioOscuro = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColorExito = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColorAdvertencia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NombrePreset = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModificadaPorNombre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionSitio", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionSitio",
                columns: new[] { "Id", "ColorAdvertencia", "ColorExito", "ColorPrimario", "ColorPrimarioOscuro", "FechaModificacion", "ModificadaPorNombre", "NombrePreset" },
                values: new object[] { 1, "#F59E0B", "#10B981", "#1A365D", "#002045", null, null, "Institucional (por defecto)" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionSitio");
        }
    }
}
