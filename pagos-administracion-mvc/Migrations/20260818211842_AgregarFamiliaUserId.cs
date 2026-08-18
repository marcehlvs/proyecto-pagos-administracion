using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFamiliaUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamiliaUserId",
                table: "Alumnos",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alumnos_FamiliaUserId",
                table: "Alumnos",
                column: "FamiliaUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Alumnos_AspNetUsers_FamiliaUserId",
                table: "Alumnos",
                column: "FamiliaUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alumnos_AspNetUsers_FamiliaUserId",
                table: "Alumnos");

            migrationBuilder.DropIndex(
                name: "IX_Alumnos_FamiliaUserId",
                table: "Alumnos");

            migrationBuilder.DropColumn(
                name: "FamiliaUserId",
                table: "Alumnos");
        }
    }
}
