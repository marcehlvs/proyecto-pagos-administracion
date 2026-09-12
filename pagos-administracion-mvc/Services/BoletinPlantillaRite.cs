using Microsoft.Extensions.Configuration;
using pagos_administracion_mvc.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static pagos_administracion_mvc.Models.Enums;

namespace pagos_administracion_mvc.Services
{
    // Plantilla "RITE" (Registro Institucional de Trayectorias Educativas, el formulario oficial
    // de la DGCyE de la Pcia. de Buenos Aires para Nivel Secundario). Fase 1 imitaba el layout
    // con lo que ya se calculaba (Materia, Calificación 1º/2º Cuatrimestre, Calificación Final).
    // Fase 2 sumó: TIPO (C/R, desde Alumno.EsRecursante), AÑO (Alumno.GradoAnio), Valoración
    // Preliminar TEA/TEP/TED (Nota.ValoracionPreliminar, cargada por el Docente por cuatrimestre),
    // Distrito y Sección (ConfiguracionSitio.Distrito / Curso.Nombre), y Días hábiles/Inasistencias
    // cruzando Asistencia con el rango FechaInicio/FechaFin de cada Periodo Cuatrimestral.
    // Todavía en blanco (quedan para una Fase 3): Intensificación (diciembre/febrero) y la tabla
    // de "Materias pendientes de aprobación" (arrastre de años anteriores) — ninguna de las dos
    // tiene todavía un lugar donde cargarse en el sistema.
    //
    // Se registra en Program.cs junto a IBoletinPlantilla, bajo su propio tipo concreto
    // (BoletinPlantillaRite), no como el default: BoletinesController inyecta ambas
    // (IBoletinPlantilla para la que ya había, BoletinPlantillaRite para esta) y elige según el
    // parámetro "plantilla" de la URL. Implementa la misma interfaz que la plantilla por
    // defecto para que el controller las trate igual, aunque el RITE no use el color
    // institucional de ConfiguracionSitio (es un formulario oficial, blanco y negro).
    public class BoletinPlantillaRite : IBoletinPlantilla
    {
        private readonly IConfiguration _configuracion;
        public BoletinPlantillaRite(IConfiguration configuracion) => _configuracion = configuracion;

        public byte[] Generar(BoletinData datos, ConfiguracionSitio? configuracionSitio)
        {
            var nombreEscuela = _configuracion["DatosBancarios:Titular"] ?? "—";

            // El RITE separa Calificación de 1º y 2º Cuatrimestre (no cualquier Periodo
            // "contenedor" del año): si el Admin armó Trimestres en vez de Cuatrimestres para
            // este año lectivo, esas columnas quedan vacías — es una limitación real, no un bug:
            // el formulario oficial pide cuatrimestres.
            var cuatrimestres = datos.Columnas
                .Where(c => c.Tipo == TipoPeriodo.Cuatrimestral)
                .OrderBy(c => c.Nombre)
                .Take(2)
                .ToList();
            var primerCuatrimestre = cuatrimestres.ElementAtOrDefault(0);
            var segundoCuatrimestre = cuatrimestres.ElementAtOrDefault(1);
            var columnaFinal = datos.Columnas.FirstOrDefault(c => c.Tipo == TipoPeriodo.Anual);

            var filasPorCurso = datos.Filas.GroupBy(f => f.CursoEtiqueta).ToList();

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Black));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("REGISTRO INSTITUCIONAL DE TRAYECTORIAS EDUCATIVAS")
                            .FontSize(12).Bold();
                        col.Item().AlignCenter().PaddingBottom(8).Text("NIVEL DE EDUCACIÓN SECUNDARIA")
                            .FontSize(10).Bold();

                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem(2).Text(t => { t.Span("ESCUELA: ").Bold(); t.Span(nombreEscuela); });
                            row.RelativeItem(1).Text(t => { t.Span("DISTRITO: ").Bold(); t.Span(configuracionSitio?.Distrito ?? ""); });
                        });
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem(1).Text(t => { t.Span("CICLO LECTIVO: ").Bold(); t.Span(datos.AnioLectivo.ToString()); });
                            row.RelativeItem(1).Text(t => { t.Span("AÑO: ").Bold(); t.Span($"{datos.Alumno.GradoAnio}° {datos.Alumno.Nivel}"); });
                        });
                        col.Item().PaddingTop(4).Text(t => { t.Span("SECCIÓN: ").Bold(); t.Span(datos.Seccion ?? ""); });
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem(2).Text(t => { t.Span("ESTUDIANTE: ").Bold(); t.Span($"{datos.Alumno.Apellido}, {datos.Alumno.Nombre}"); });
                            row.RelativeItem(1).Text(t => { t.Span("DNI: ").Bold(); t.Span(datos.Alumno.Dni); });
                            row.RelativeItem(1).Text(t => { t.Span("TURNO: ").Bold(); t.Span(datos.Alumno.Turno.ToString()); });
                        });

                        col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Black);
                    });

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        if (!filasPorCurso.Any())
                        {
                            col.Item().PaddingTop(10).Text("Todavía no hay notas cargadas para este año lectivo.")
                                .Italic().FontColor(Colors.Grey.Darken1);
                            return;
                        }

                        col.Item().Table(table =>
                        {
                            // TIPO(1) | MATERIAS(3) | AÑO(1) | 1ºC Val(1) | 1ºC Calif(1) |
                            // 2ºC Val(1) | 2ºC Calif(1) | Intens.Dic(1) | Intens.Feb(1) |
                            // Calif.Final(1) | Observaciones(3) — mismo orden que el formulario.
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);  // TIPO (C-R)
                                columns.RelativeColumn(3);  // MATERIAS
                                columns.RelativeColumn(1);  // AÑO
                                columns.RelativeColumn(1);  // 1º Valoración preliminar
                                columns.RelativeColumn(1);  // 1º Calificación
                                columns.RelativeColumn(1);  // 2º Valoración preliminar
                                columns.RelativeColumn(1);  // 2º Calificación
                                columns.RelativeColumn(1);  // Intensificación diciembre
                                columns.RelativeColumn(1);  // Intensificación febrero
                                columns.RelativeColumn(1);  // Calificación final
                                columns.RelativeColumn(3);  // Observaciones
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CeldaEncabezado).AlignCenter().Text("TIPO\n(C-R)");
                                header.Cell().Element(CeldaEncabezado).Text("MATERIAS");
                                header.Cell().Element(CeldaEncabezado).AlignCenter().Text("AÑO");
                                header.Cell().Element(CeldaEncabezado).AlignCenter().Text("1º VALORACIÓN\nPRELIMINAR");
                                header.Cell().Element(CeldaEncabezado).AlignCenter().Text("CALIFICACIÓN\n1º CUATR.");
                                header.Cell().Element(CeldaEncabezado).AlignCenter().Text("2º VALORACIÓN\nPRELIMINAR");
                                header.Cell().Element(CeldaEncabezado).AlignCenter().Text("CALIFICACIÓN\n2º CUATR.");
                                header.Cell().Element(CeldaEncabezado).AlignCenter().Text("INTENSIF.\nDICIEMBRE");
                                header.Cell().Element(CeldaEncabezado).AlignCenter().Text("INTENSIF.\nFEBRERO");
                                header.Cell().Element(CeldaEncabezado).AlignCenter().Text("CALIFICACIÓN\nFINAL");
                                header.Cell().Element(CeldaEncabezado).Text("OBSERVACIONES");
                            });

                            var filaImpar = false;
                            var tipoAlumno = datos.Alumno.EsRecursante ? "R" : "C";
                            foreach (var grupoCurso in filasPorCurso)
                            {
                                foreach (var fila in grupoCurso)
                                {
                                    var fondo = filaImpar ? Colors.Grey.Lighten4 : Colors.White;
                                    filaImpar = !filaImpar;

                                    var calif1 = primerCuatrimestre != null ? fila.ValoresPorPeriodoId.GetValueOrDefault(primerCuatrimestre.Id) : null;
                                    var calif2 = segundoCuatrimestre != null ? fila.ValoresPorPeriodoId.GetValueOrDefault(segundoCuatrimestre.Id) : null;
                                    var califFinal = columnaFinal != null ? fila.ValoresPorPeriodoId.GetValueOrDefault(columnaFinal.Id) : null;
                                    var valoracion1 = primerCuatrimestre != null ? fila.ValoracionesPorPeriodoId.GetValueOrDefault(primerCuatrimestre.Id) : null;
                                    var valoracion2 = segundoCuatrimestre != null ? fila.ValoracionesPorPeriodoId.GetValueOrDefault(segundoCuatrimestre.Id) : null;

                                    table.Cell().Element(c => Celda(c, fondo)).AlignCenter().Text(tipoAlumno);
                                    table.Cell().Element(c => Celda(c, fondo)).Text(fila.AsignaturaNombre);
                                    table.Cell().Element(c => Celda(c, fondo)).AlignCenter().Text(datos.Alumno.GradoAnio.ToString());
                                    table.Cell().Element(c => Celda(c, fondo)).AlignCenter().Text(valoracion1?.ToString() ?? "");
                                    table.Cell().Element(c => Celda(c, fondo)).AlignCenter().Text(calif1?.ToString("0.##") ?? "");
                                    table.Cell().Element(c => Celda(c, fondo)).AlignCenter().Text(valoracion2?.ToString() ?? "");
                                    table.Cell().Element(c => Celda(c, fondo)).AlignCenter().Text(calif2?.ToString("0.##") ?? "");
                                    table.Cell().Element(c => Celda(c, fondo)); // Intensificación diciembre: sin dato todavía.
                                    table.Cell().Element(c => Celda(c, fondo)); // Intensificación febrero: sin dato todavía.
                                    table.Cell().Element(c => Celda(c, fondo)).AlignCenter().Text(califFinal?.ToString("0.##") ?? "");
                                    table.Cell().Element(c => Celda(c, fondo)).Text("1°C:\n2°C:");
                                }
                            }
                        });

                        col.Item().PaddingTop(10).Text(
                            "NOTAS: 1) TIPO indica «C» (primera vez) o «R» (recursada). 2) VALORACIÓN PRELIMINAR: TEA/TEP/TED. " +
                            "3) CALIFICACIÓN FINAL: promedio de ambos cuatrimestres cuando los dos son de 7 a 10.")
                            .FontSize(6).Italic().FontColor(Colors.Grey.Darken1);

                        // Los totales de días hábiles/inasistencias solo se completan cuando el
                        // Cuatrimestre correspondiente tiene FechaInicio/FechaFin cargadas (ver
                        // BoletinService) — si al Admin no le interesa este cruce automático,
                        // el campo queda en blanco en vez de mostrar un 0 que no dice nada.
                        var resumen1 = primerCuatrimestre != null ? datos.AsistenciasPorPeriodoId.GetValueOrDefault(primerCuatrimestre.Id) : null;
                        var resumen2 = segundoCuatrimestre != null ? datos.AsistenciasPorPeriodoId.GetValueOrDefault(segundoCuatrimestre.Id) : null;
                        var totalDiasHabiles = (resumen1?.DiasHabiles ?? 0) + (resumen2?.DiasHabiles ?? 0);
                        var totalInasistencias = (resumen1?.Inasistencias ?? 0) + (resumen2?.Inasistencias ?? 0);
                        var hayAlgunResumen = resumen1 != null || resumen2 != null;

                        col.Item().PaddingTop(14).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(c => Celda(c, Colors.White));
                            table.Cell().Element(CeldaEncabezado).AlignCenter().Text("1º CUATRIMESTRE");
                            table.Cell().Element(CeldaEncabezado).AlignCenter().Text("2º CUATRIMESTRE");
                            table.Cell().Element(CeldaEncabezado).AlignCenter().Text("TOTAL");

                            table.Cell().Element(c => Celda(c, Colors.White)).Text("DÍAS HÁBILES").Bold();
                            table.Cell().Element(c => Celda(c, Colors.White)).AlignCenter().Text(resumen1?.DiasHabiles.ToString() ?? "");
                            table.Cell().Element(c => Celda(c, Colors.White)).AlignCenter().Text(resumen2?.DiasHabiles.ToString() ?? "");
                            table.Cell().Element(c => Celda(c, Colors.White)).AlignCenter().Text(hayAlgunResumen ? totalDiasHabiles.ToString() : "");

                            table.Cell().Element(c => Celda(c, Colors.White)).Text("INASISTENCIAS").Bold();
                            table.Cell().Element(c => Celda(c, Colors.White)).AlignCenter().Text(resumen1?.Inasistencias.ToString() ?? "");
                            table.Cell().Element(c => Celda(c, Colors.White)).AlignCenter().Text(resumen2?.Inasistencias.ToString() ?? "");
                            table.Cell().Element(c => Celda(c, Colors.White)).AlignCenter().Text(hayAlgunResumen ? totalInasistencias.ToString() : "");
                        });

                        col.Item().PaddingTop(20).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().LineHorizontal(0.5f).LineColor(Colors.Black);
                                c.Item().PaddingTop(2).AlignCenter().Text("DIRECTORA / DIRECTOR").FontSize(7);
                            });
                            row.ConstantItem(20);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().LineHorizontal(0.5f).LineColor(Colors.Black);
                                c.Item().PaddingTop(2).AlignCenter().Text("ADULTO/A RESPONSABLE").FontSize(7);
                            });
                            row.ConstantItem(20);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().LineHorizontal(0.5f).LineColor(Colors.Black);
                                c.Item().PaddingTop(2).AlignCenter().Text("ESTUDIANTE").FontSize(7);
                            });
                        });
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1));
                            t.Span("Página ");
                            t.CurrentPageNumber();
                            t.Span(" de ");
                            t.TotalPages();
                        });
                    });
                });
            });

            return documento.GeneratePdf();
        }

        private static IContainer CeldaEncabezado(IContainer contenedor) =>
            contenedor.Border(0.5f).BorderColor(Colors.Black).Background(Colors.Grey.Lighten2)
                .PaddingVertical(3).PaddingHorizontal(2).DefaultTextStyle(x => x.FontSize(6).Bold());

        private static IContainer Celda(IContainer contenedor, string colorFondo) =>
            contenedor.Border(0.5f).BorderColor(Colors.Black).Background(colorFondo)
                .PaddingVertical(3).PaddingHorizontal(2).DefaultTextStyle(x => x.FontSize(7));
    }
}