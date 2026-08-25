using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AvisosIdemPotentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvisoProximoVencimientoEnviado",
                table: "Cuotas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AvisoVencidaEnviado",
                table: "Cuotas",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvisoProximoVencimientoEnviado",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "AvisoVencidaEnviado",
                table: "Cuotas");
        }
    }
}
