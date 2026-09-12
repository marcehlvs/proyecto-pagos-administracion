using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRiteFase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsRecursante",
                table: "Alumnos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Distrito",
                table: "ConfiguracionSitio",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicio",
                table: "Periodos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFin",
                table: "Periodos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValoracionPreliminar",
                table: "Notas",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsRecursante",
                table: "Alumnos");

            migrationBuilder.DropColumn(
                name: "Distrito",
                table: "ConfiguracionSitio");

            migrationBuilder.DropColumn(
                name: "FechaInicio",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "FechaFin",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "ValoracionPreliminar",
                table: "Notas");
        }
    }
}
