using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace pagos_administracion_mvc.Migrations
{
    /// <inheritdoc />
    public partial class Models : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActualizadoPorNombre",
                table: "Pagos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaActualizacion",
                table: "Pagos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRegistro",
                table: "Pagos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RegistradoPorNombre",
                table: "Pagos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistradoPorUserId",
                table: "Pagos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "Cuotas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreadaPorNombre",
                table: "Cuotas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "Cuotas",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaModificacion",
                table: "Cuotas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificadaPorNombre",
                table: "Cuotas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistradoPorNombre",
                table: "ContactosManuales",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualizadoPorNombre",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "FechaActualizacion",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "FechaRegistro",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "RegistradoPorNombre",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "RegistradoPorUserId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "CreadaPorNombre",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "FechaModificacion",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "ModificadaPorNombre",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "RegistradoPorNombre",
                table: "ContactosManuales");
        }
    }
}
