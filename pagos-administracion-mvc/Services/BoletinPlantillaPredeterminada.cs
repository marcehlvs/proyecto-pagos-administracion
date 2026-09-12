using pagos_administracion_mvc.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace pagos_administracion_mvc.Services
{
    // Implementación por defecto de IBoletinPlantilla. Reemplaza al viejo BoletinPdfBuilder
    // estático: mismo espíritu (separar el dibujo del PDF de la lógica de negocio, que sigue
    // viviendo en BoletinService/NotaCalculadora), pero:
    //  - agrupa las filas por Curso en vez de repetir la etiqueta del curso en cada renglón,
    //  - usa el color institucional de ConfiguracionSitio en vez de un gris genérico,
    //  - separa visualmente cada materia sin líneas por todos lados,
    //  - agrega numeración de página, para boletines de varias hojas.
    //
    // Nota: no pude compilar/probar esto en el entorno donde lo armé (sin acceso a NuGet), así
    // que conviene correr un build local antes de darlo por definitivo — la estructura y la
    // lógica están pensadas para la API de QuestPDF, pero puede haber algún ajuste puntual de
    // nombres/overloads según la versión que termines usando.
    public class BoletinPlantillaPredeterminada : IBoletinPlantilla
    {
        // Institucional por defecto, mismo valor que ConfiguracionSitio.ColorPrimario trae de
        // fábrica — así el PDF no queda con un color "roto" si todavía no existe la fila de
        // configuración (instalación nueva, sin que el Admin haya entrado a Estilo del sitio).
        private const string ColorInstitucionalPorDefecto = "#1A365D";

        public byte[] Generar(BoletinData datos, ConfiguracionSitio? configuracionSitio)
        {
            var colorPrimario = Color.FromHex(configuracionSitio?.ColorPrimario ?? ColorInstitucionalPorDefecto);
            var filasPorCurso = datos.Filas
                .GroupBy(f => f.CursoEtiqueta)
                .ToList();

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                    page.Header().Column(col =>
                    {
                        col.Item().Background(colorPrimario).Padding(15).Row(row =>
                        {
                            row.RelativeItem().Column(inner =>
                            {
                                inner.Item().Text("Boletín de calificaciones")
                                    .FontSize(18).Bold().FontColor(Colors.White);
                                inner.Item().PaddingTop(2).Text($"Año lectivo {datos.AnioLectivo}")
                                    .FontSize(10).FontColor(Colors.White);
                            });
                        });

                        col.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.Span("Alumno/a: ").SemiBold();
                                text.Span($"{datos.Alumno.Apellido}, {datos.Alumno.Nombre}");
                            });
                            row.RelativeItem().AlignRight().Text(text =>
                            {
                                text.Span("DNI: ").SemiBold();
                                text.Span(datos.Alumno.Dni);
                            });
                        });
                    });

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        if (!filasPorCurso.Any())
                        {
                            col.Item().PaddingTop(10).Text("Todavía no hay notas cargadas para este año lectivo.")
                                .Italic().FontColor(Colors.Grey.Medium);
                            return;
                        }

                        var esPrimerCurso = true;
                        foreach (var grupoCurso in filasPorCurso)
                        {
                            if (!esPrimerCurso) col.Item().PaddingTop(14);
                            esPrimerCurso = false;

                            col.Item().Text(grupoCurso.Key).FontSize(12).Bold().FontColor(colorPrimario);
                            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(colorPrimario);

                            col.Item().PaddingTop(6).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3); // Materia
                                    foreach (var _ in datos.Columnas)
                                        columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CeldaEncabezado).Text("Materia");
                                    foreach (var columna in datos.Columnas)
                                        header.Cell().Element(CeldaEncabezado).AlignCenter().Text(columna.Nombre);
                                });

                                var filaImpar = false;
                                foreach (var fila in grupoCurso)
                                {
                                    var fondo = filaImpar ? Colors.Grey.Lighten4 : Colors.White;
                                    filaImpar = !filaImpar;

                                    table.Cell().Element(c => Celda(c, fondo)).Text(fila.AsignaturaNombre);
                                    foreach (var columna in datos.Columnas)
                                    {
                                        var valor = fila.ValoresPorPeriodoId.GetValueOrDefault(columna.Id);
                                        table.Cell().Element(c => Celda(c, fondo)).AlignCenter()
                                            .Text(valor.HasValue ? valor.Value.ToString("0.##") : "-");
                                    }
                                }
                            });
                        }
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium));
                            text.Span("Página ");
                            text.CurrentPageNumber();
                            text.Span(" de ");
                            text.TotalPages();
                        });
                    });
                });
            });

            return documento.GeneratePdf();
        }

        private static IContainer CeldaEncabezado(IContainer contenedor) =>
            contenedor.Background(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(5).DefaultTextStyle(x => x.SemiBold());

        private static IContainer Celda(IContainer contenedor, string colorFondo) =>
            contenedor.Background(colorFondo).PaddingVertical(6).PaddingHorizontal(5)
                .BorderBottom(1).BorderColor(Colors.Grey.Lighten3);
    }
}
