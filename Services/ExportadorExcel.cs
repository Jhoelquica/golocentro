using ClosedXML.Excel;

namespace GestionAlmacen_Golocentro.Services
{
    public enum FormatoColumna { Texto, Entero, Moneda, Porcentaje, Fecha, FechaHora }

    public record ColumnaExcel(string Titulo, FormatoColumna Formato = FormatoColumna.Texto);

    public class HojaExcel
    {
        public string Nombre { get; init; } = "";
        public List<ColumnaExcel> Columnas { get; init; } = new();
        public List<object?[]> Filas { get; init; } = new();

        // Fila final en negrita (p. ej. "Total"); opcional
        public object?[]? Totales { get; init; }
    }

    // Libros de Excel de los reportes: título, sede y periodo arriba; encabezados fijos y con filtro.
    public static class ExportadorExcel
    {
        public const string TipoContenido = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public static byte[] Generar(string titulo, string subtitulo, IEnumerable<HojaExcel> hojas)
        {
            using var libro = new XLWorkbook();
            foreach (var hoja in hojas)
                AgregarHoja(libro, titulo, subtitulo, hoja);

            using var ms = new MemoryStream();
            libro.SaveAs(ms);
            return ms.ToArray();
        }

        private static void AgregarHoja(XLWorkbook libro, string titulo, string subtitulo, HojaExcel hoja)
        {
            var ws = libro.Worksheets.Add(hoja.Nombre);
            ws.Cell(1, 1).Value = titulo;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(2, 1).Value = subtitulo;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#475569");

            const int filaEncabezado = 4;
            for (var c = 0; c < hoja.Columnas.Count; c++)
            {
                var celda = ws.Cell(filaEncabezado, c + 1);
                celda.Value = hoja.Columnas[c].Titulo;
                celda.Style.Font.Bold = true;
                celda.Style.Font.FontColor = XLColor.White;
                celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#047857");
            }

            var fila = filaEncabezado;
            foreach (var valores in hoja.Filas)
                Escribir(ws, ++fila, hoja.Columnas, valores);

            if (hoja.Filas.Count > 0)
                ws.Range(filaEncabezado, 1, fila, hoja.Columnas.Count).SetAutoFilter();

            if (hoja.Totales != null)
            {
                Escribir(ws, ++fila, hoja.Columnas, hoja.Totales);
                ws.Row(fila).Style.Font.Bold = true;
                ws.Range(fila, 1, fila, hoja.Columnas.Count).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }

            ws.SheetView.FreezeRows(filaEncabezado);
            ws.Columns(1, hoja.Columnas.Count).AdjustToContents(filaEncabezado, fila);
            foreach (var columna in ws.Columns(1, hoja.Columnas.Count))
                if (columna.Width > 60)
                    columna.Width = 60;
        }

        private static void Escribir(IXLWorksheet ws, int fila, List<ColumnaExcel> columnas, object?[] valores)
        {
            for (var c = 0; c < columnas.Count && c < valores.Length; c++)
            {
                var celda = ws.Cell(fila, c + 1);
                // Los textos se guardan como texto: un nombre que empiece con "=" no se vuelve fórmula
                switch (valores[c])
                {
                    case null:
                        continue;
                    case string s:
                        celda.Value = s;
                        break;
                    case int i:
                        celda.Value = i;
                        break;
                    case decimal d:
                        celda.Value = (double)d;
                        break;
                    case DateTime dt:
                        celda.Value = dt;
                        break;
                    case DateOnly fecha:
                        celda.Value = fecha.ToDateTime(TimeOnly.MinValue);
                        break;
                    case bool b:
                        celda.Value = b ? "Sí" : "No";
                        break;
                    default:
                        celda.Value = valores[c]!.ToString();
                        break;
                }

                celda.Style.NumberFormat.Format = columnas[c].Formato switch
                {
                    FormatoColumna.Entero => "#,##0",
                    FormatoColumna.Moneda => "\"S/\" #,##0.00",
                    FormatoColumna.Porcentaje => "0.0%",
                    FormatoColumna.Fecha => "dd/mm/yyyy",
                    FormatoColumna.FechaHora => "dd/mm/yyyy hh:mm",
                    _ => celda.Style.NumberFormat.Format
                };
            }
        }
    }
}
