using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrdenNotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notas_InscripcionId_CursoAsignaturaId_PeriodoId",
                table: "Notas");

            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "Notas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Notas_InscripcionId_CursoAsignaturaId_PeriodoId_Orden",
                table: "Notas",
                columns: new[] { "InscripcionId", "CursoAsignaturaId", "PeriodoId", "Orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notas_InscripcionId_CursoAsignaturaId_PeriodoId_Orden",
                table: "Notas");

            migrationBuilder.DropColumn(
                name: "Orden",
                table: "Notas");

            migrationBuilder.CreateIndex(
                name: "IX_Notas_InscripcionId_CursoAsignaturaId_PeriodoId",
                table: "Notas",
                columns: new[] { "InscripcionId", "CursoAsignaturaId", "PeriodoId" },
                unique: true);
        }
    }
}
