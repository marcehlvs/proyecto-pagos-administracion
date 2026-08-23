using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarActivoAPago : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "Pagos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Activo",
                table: "Pagos");
        }
    }
}
