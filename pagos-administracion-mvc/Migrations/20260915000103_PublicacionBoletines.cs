using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class PublicacionBoletines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoletinPublicaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CursoId = table.Column<int>(type: "int", nullable: false),
                    AnioLectivo = table.Column<int>(type: "int", nullable: false),
                    Publicado = table.Column<bool>(type: "bit", nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicadoPorNombre = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoletinPublicaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoletinPublicaciones_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoletinPublicaciones_CursoId_AnioLectivo",
                table: "BoletinPublicaciones",
                columns: new[] { "CursoId", "AnioLectivo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoletinPublicaciones");
        }
    }
}
