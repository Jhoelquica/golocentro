using ClosedXML.Excel;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace GestionAlmacen_Golocentro.Controllers
{
    [Authorize(Roles = "duena,encargada")]
    public class ReporteController : Controller
    {
        private readonly AppDbContext _context;

        public ReporteController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Reporte/Index (pantalla de selección)
        public IActionResult Index()
        {
            return View();
        }

        // GET: Reporte/Movimientos (PDF)
        public async Task<IActionResult> Movimientos(DateTime? desde, DateTime? hasta, string tipo)
        {
            // Obtener sede del usuario
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            var query = _context.Movimientos
                .Include(m => m.IdUsuarioNavigation)
                .Include(m => m.IdSedeNavigation)
                .AsQueryable();

            if (sedeId.HasValue)
                query = query.Where(m => m.IdSede == sedeId.Value);
            if (desde.HasValue)
                query = query.Where(m => m.Fecha >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(m => m.Fecha <= hasta.Value.AddDays(1));
            if (!string.IsNullOrEmpty(tipo))
                query = query.Where(m => m.Tipo == tipo);

            var movimientos = await query.OrderByDescending(m => m.Fecha).ToListAsync();

            // Generar PDF
            using (var ms = new MemoryStream())
            {
                iTextSharp.text.Document doc = new iTextSharp.text.Document();
                iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
                doc.Open();
                doc.Add(new Paragraph("Reporte de Movimientos - Golocentro"));
                doc.Add(new Paragraph($"Sede: {(sedeId.HasValue ? (await _context.Sedes.FindAsync(sedeId.Value))?.Nombre : "Todas")}"));
                doc.Add(new Paragraph($"Desde: {desde:dd/MM/yyyy} - Hasta: {hasta:dd/MM/yyyy}"));
                doc.Add(new Paragraph("\n"));

                PdfPTable table = new PdfPTable(5);
                table.AddCell("Fecha");
                table.AddCell("Tipo");
                table.AddCell("Usuario");
                table.AddCell("Sede");
                table.AddCell("Factura/Pedido");

                foreach (var m in movimientos)
                {
                    table.AddCell(m.Fecha.ToString("dd/MM/yyyy HH:mm"));
                    table.AddCell(m.Tipo);
                    table.AddCell(m.IdUsuarioNavigation?.Nombre);
                    table.AddCell(m.IdSedeNavigation?.Nombre);
                    table.AddCell(m.ComprobanteEmitido ? "Sí" : "No");
                }
                doc.Add(table);
                doc.Close();

                return File(ms.ToArray(), "application/pdf", "Movimientos.pdf");
            }
        }

        // GET: Reporte/Stock (Excel)
        public async Task<IActionResult> Stock()
        {
            string sedeIdClaim = User.FindFirst("SedeId")?.Value;
            int? sedeId = string.IsNullOrEmpty(sedeIdClaim) ? (int?)null : int.Parse(sedeIdClaim);

            var query = _context.ProductoUbicacions
                .Include(pu => pu.IdProductoNavigation)
                .Include(pu => pu.IdUbicacionNavigation)
                    .ThenInclude(u => u.IdSedeNavigation)
                .AsQueryable();

            if (sedeId.HasValue)
                query = query.Where(pu => pu.IdUbicacionNavigation.IdSede == sedeId.Value);

            var datos = await query.Select(pu => new
            {
                Producto = pu.IdProductoNavigation.Nombre,
                Ubicacion = pu.IdUbicacionNavigation.CodigoEstante,
                Sede = pu.IdUbicacionNavigation.IdSedeNavigation.Nombre,
                Stock = pu.CantidadActual
            }).ToListAsync();

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Stock");
                ws.Cell(1, 1).Value = "Producto";
                ws.Cell(1, 2).Value = "Ubicación";
                ws.Cell(1, 3).Value = "Sede";
                ws.Cell(1, 4).Value = "Stock";

                for (int i = 0; i < datos.Count; i++)
                {
                    ws.Cell(i + 2, 1).Value = datos[i].Producto;
                    ws.Cell(i + 2, 2).Value = datos[i].Ubicacion;
                    ws.Cell(i + 2, 3).Value = datos[i].Sede;
                    ws.Cell(i + 2, 4).Value = datos[i].Stock;
                }

                ws.Columns().AdjustToContents();
                using (var ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Stock.xlsx");
                }
            }
        }
    }
}