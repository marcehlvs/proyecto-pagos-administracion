using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTareas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tareas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CursoAsignaturaId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaEntrega = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodoId = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadaPorNombre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tareas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tareas_CursosAsignaturas_CursoAsignaturaId",
                        column: x => x.CursoAsignaturaId,
                        principalTable: "CursosAsignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tareas_Periodos_PeriodoId",
                        column: x => x.PeriodoId,
                        principalTable: "Periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Entregas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TareaId = table.Column<int>(type: "int", nullable: false),
                    InscripcionId = table.Column<int>(type: "int", nullable: false),
                    Entregada = table.Column<bool>(type: "bit", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArchivoRuta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArchivoNombreOriginal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaEntrega = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Calificacion = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    ObservacionDocente = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NotaId = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CalificadaPorNombre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCalificacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entregas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entregas_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entregas_Notas_NotaId",
                        column: x => x.NotaId,
                        principalTable: "Notas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Entregas_Tareas_TareaId",
                        column: x => x.TareaId,
                        principalTable: "Tareas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_InscripcionId",
                table: "Entregas",
                column: "InscripcionId");

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_NotaId",
                table: "Entregas",
                column: "NotaId");

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_TareaId_InscripcionId",
                table: "Entregas",
                columns: new[] { "TareaId", "InscripcionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_CursoAsignaturaId",
                table: "Tareas",
                column: "CursoAsignaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tareas_PeriodoId",
                table: "Tareas",
                column: "PeriodoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Entregas");

            migrationBuilder.DropTable(
                name: "Tareas");
        }
    }
}
