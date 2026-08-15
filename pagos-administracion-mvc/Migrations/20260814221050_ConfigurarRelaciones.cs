using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurarRelaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cuotas_Alumnos_AlumnoId",
                table: "Cuotas");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Cuotas_CuotaId",
                table: "Pagos");

            migrationBuilder.AddForeignKey(
                name: "FK_Cuotas_Alumnos_AlumnoId",
                table: "Cuotas",
                column: "AlumnoId",
                principalTable: "Alumnos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Cuotas_CuotaId",
                table: "Pagos",
                column: "CuotaId",
                principalTable: "Cuotas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cuotas_Alumnos_AlumnoId",
                table: "Cuotas");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagos_Cuotas_CuotaId",
                table: "Pagos");

            migrationBuilder.AddForeignKey(
                name: "FK_Cuotas_Alumnos_AlumnoId",
                table: "Cuotas",
                column: "AlumnoId",
                principalTable: "Alumnos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagos_Cuotas_CuotaId",
                table: "Pagos",
                column: "CuotaId",
                principalTable: "Cuotas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
