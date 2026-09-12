using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFeriados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Feriados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feriados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Feriados_Fecha",
                table: "Feriados",
                column: "Fecha",
                unique: true);

            // Calendario oficial 2026 (Ley 27.399 "Ley de Establecimiento de Feriados y Fines de
            // Semana" + Resolución 164/2025 de Jefatura de Gabinete, que fija los 3 puentes
            // turísticos): 12 feriados inamovibles, 4 trasladables ya observados en su fecha
            // definitiva, y 3 puentes turísticos. Fuente: argentina.gob.ar/feriados?year=2026.
            // Los años siguientes se cargan a mano desde /Feriados (los puentes recién se
            // decretan a fines del año anterior, no hay forma de anticiparlos acá).
            migrationBuilder.InsertData(
                table: "Feriados",
                columns: new[] { "Fecha", "Descripcion", "Activo" },
                values: new object[,]
                {
                    { new DateTime(2026, 1, 1), "Año Nuevo", true },
                    { new DateTime(2026, 2, 16), "Carnaval", true },
                    { new DateTime(2026, 2, 17), "Carnaval", true },
                    { new DateTime(2026, 3, 23), "Feriado puente turístico", true },
                    { new DateTime(2026, 3, 24), "Día Nacional de la Memoria por la Verdad y la Justicia", true },
                    { new DateTime(2026, 4, 2), "Día del Veterano y de los Caídos en la Guerra de Malvinas", true },
                    { new DateTime(2026, 4, 3), "Viernes Santo", true },
                    { new DateTime(2026, 5, 1), "Día del Trabajador", true },
                    { new DateTime(2026, 5, 25), "Día de la Revolución de Mayo", true },
                    { new DateTime(2026, 6, 15), "Paso a la Inmortalidad del Gral. Güemes (observado)", true },
                    { new DateTime(2026, 6, 20), "Paso a la Inmortalidad del Gral. Belgrano", true },
                    { new DateTime(2026, 7, 9), "Día de la Independencia", true },
                    { new DateTime(2026, 7, 10), "Feriado puente turístico", true },
                    { new DateTime(2026, 8, 17), "Paso a la Inmortalidad del Gral. San Martín (observado)", true },
                    { new DateTime(2026, 10, 12), "Día del Respeto a la Diversidad Cultural", true },
                    { new DateTime(2026, 11, 23), "Día de la Soberanía Nacional (observado)", true },
                    { new DateTime(2026, 12, 7), "Feriado puente turístico", true },
                    { new DateTime(2026, 12, 8), "Inmaculada Concepción de María", true },
                    { new DateTime(2026, 12, 25), "Navidad", true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Feriados");
        }
    }
}
