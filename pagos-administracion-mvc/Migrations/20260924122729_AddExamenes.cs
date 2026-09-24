using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AddExamenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Examenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CursoAsignaturaId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaDesde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaHasta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TiempoLimiteMinutos = table.Column<int>(type: "int", nullable: true),
                    NotaMinimaAprobatoria = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    SoloUnIntento = table.Column<bool>(type: "bit", nullable: false),
                    OrdenAleatorio = table.Column<bool>(type: "bit", nullable: false),
                    MostrarResultado = table.Column<bool>(type: "bit", nullable: false),
                    PeriodoId = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPorNombre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Examenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Examenes_CursosAsignaturas_CursoAsignaturaId",
                        column: x => x.CursoAsignaturaId,
                        principalTable: "CursosAsignaturas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Examenes_Periodos_PeriodoId",
                        column: x => x.PeriodoId,
                        principalTable: "Periodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntentosExamen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamenId = table.Column<int>(type: "int", nullable: false),
                    InscripcionId = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotaValor = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    Aprobado = table.Column<bool>(type: "bit", nullable: true),
                    PuntajeObtenido = table.Column<int>(type: "int", nullable: false),
                    PuntajeTotal = table.Column<int>(type: "int", nullable: false),
                    NotaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosExamen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntentosExamen_Examenes_ExamenId",
                        column: x => x.ExamenId,
                        principalTable: "Examenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntentosExamen_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntentosExamen_Notas_NotaId",
                        column: x => x.NotaId,
                        principalTable: "Notas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreguntasExamen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamenId = table.Column<int>(type: "int", nullable: false),
                    Enunciado = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Puntaje = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreguntasExamen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreguntasExamen_Examenes_ExamenId",
                        column: x => x.ExamenId,
                        principalTable: "Examenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpcionesRespuesta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreguntaExamenId = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    EsCorrecta = table.Column<bool>(type: "bit", nullable: false),
                    Letra = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcionesRespuesta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpcionesRespuesta_PreguntasExamen_PreguntaExamenId",
                        column: x => x.PreguntaExamenId,
                        principalTable: "PreguntasExamen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntentosPreguntas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntentoExamenId = table.Column<int>(type: "int", nullable: false),
                    PreguntaExamenId = table.Column<int>(type: "int", nullable: false),
                    OpcionRespuestaId = table.Column<int>(type: "int", nullable: true),
                    EsCorrecta = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosPreguntas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntentosPreguntas_IntentosExamen_IntentoExamenId",
                        column: x => x.IntentoExamenId,
                        principalTable: "IntentosExamen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntentosPreguntas_OpcionesRespuesta_OpcionRespuestaId",
                        column: x => x.OpcionRespuestaId,
                        principalTable: "OpcionesRespuesta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntentosPreguntas_PreguntasExamen_PreguntaExamenId",
                        column: x => x.PreguntaExamenId,
                        principalTable: "PreguntasExamen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Examenes_CursoAsignaturaId",
                table: "Examenes",
                column: "CursoAsignaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_Examenes_PeriodoId",
                table: "Examenes",
                column: "PeriodoId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosExamen_ExamenId",
                table: "IntentosExamen",
                column: "ExamenId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosExamen_InscripcionId",
                table: "IntentosExamen",
                column: "InscripcionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosExamen_NotaId",
                table: "IntentosExamen",
                column: "NotaId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosPreguntas_IntentoExamenId_PreguntaExamenId",
                table: "IntentosPreguntas",
                columns: new[] { "IntentoExamenId", "PreguntaExamenId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntentosPreguntas_OpcionRespuestaId",
                table: "IntentosPreguntas",
                column: "OpcionRespuestaId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosPreguntas_PreguntaExamenId",
                table: "IntentosPreguntas",
                column: "PreguntaExamenId");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesRespuesta_PreguntaExamenId",
                table: "OpcionesRespuesta",
                column: "PreguntaExamenId");

            migrationBuilder.CreateIndex(
                name: "IX_PreguntasExamen_ExamenId",
                table: "PreguntasExamen",
                column: "ExamenId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntentosPreguntas");

            migrationBuilder.DropTable(
                name: "IntentosExamen");

            migrationBuilder.DropTable(
                name: "OpcionesRespuesta");

            migrationBuilder.DropTable(
                name: "PreguntasExamen");

            migrationBuilder.DropTable(
                name: "Examenes");
        }
    }
}
