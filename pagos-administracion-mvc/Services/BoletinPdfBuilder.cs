using pagos_administracion_mvc.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace pagos_administracion_mvc.Services
{
    // Arma el PDF a partir de BoletinData (BoletinService ya hizo todo el trabajo de ir a buscar
    // y calcular las notas; acá solo se dibuja). Separado a propósito, para poder cambiar el
    // diseño del boletín sin tocar la lógica de negocio.
    //
    // Nota: no pude compilar/probar esto en el entorno donde lo armé (sin acceso a NuGet para
    // instalar QuestPDF), así que revisalo con un build local antes de darlo por definitivo —
    // la lógica y la estructura de la tabla están bien, pero puede haber algún detalle de la API
    // de QuestPDF (nombres de métodos, overloads) para ajustar según la versión que termines usando.
    public static class BoletinPdfBuilder
    {
        public static byte[] Generar(BoletinData datos)
        {
            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Boletín de calificaciones").FontSize(18).Bold();
                        col.Item().Text($"{datos.Alumno.Apellido}, {datos.Alumno.Nombre} — DNI {datos.Alumno.Dni}");
                        col.Item().Text($"Año lectivo {datos.AnioLectivo}");
                    });

                    page.Content().PaddingTop(15).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Curso
                            columns.RelativeColumn(3); // Materia
                            foreach (var _ in datos.Columnas)
                                columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CeldaEncabezado).Text("Curso");
                            header.Cell().Element(CeldaEncabezado).Text("Materia");
                            foreach (var columna in datos.Columnas)
                                header.Cell().Element(CeldaEncabezado).Text(columna.Nombre);
                        });

                        foreach (var fila in datos.Filas)
                        {
                            table.Cell().Element(Celda).Text(fila.CursoEtiqueta);
                            table.Cell().Element(Celda).Text(fila.AsignaturaNombre);
                            foreach (var columna in datos.Columnas)
                            {
                                var valor = fila.ValoresPorPeriodoId.GetValueOrDefault(columna.Id);
                                table.Cell().Element(Celda).AlignCenter().Text(valor.HasValue ? valor.Value.ToString("0.##") : "-");
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Generado el ");
                        x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    });
                });
            });

            return documento.GeneratePdf();
        }

        private static IContainer CeldaEncabezado(IContainer contenedor) =>
            contenedor.Background(Colors.Grey.Lighten2).Padding(5).DefaultTextStyle(x => x.Bold());

        private static IContainer Celda(IContainer contenedor) =>
            contenedor.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
    }
}
