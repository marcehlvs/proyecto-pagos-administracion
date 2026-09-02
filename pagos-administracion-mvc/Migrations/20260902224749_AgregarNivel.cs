using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNivel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArancelesNivel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nivel = table.Column<int>(type: "int", nullable: false),
                    VigenteDesde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Curricular = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraCurricular = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EquipamientoDidactico = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Mantenimiento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmergenciaMedica = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BonificacionPagoATiempo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadaPorNombre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadaPorNombre = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArancelesNivel", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArancelesNivel");
        }
    }
}
