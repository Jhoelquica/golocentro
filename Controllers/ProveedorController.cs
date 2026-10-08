using System.Text.RegularExpressions;
using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.Models;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Controllers
{
    [Authorize(Roles = "duena,encargada")]
    public class ProveedorController : Controller
    {
        private const int TamanoPagina = 10;
        private const string VistaLista = "~/Views/Shared/Contraparte/Lista.cshtml";
        private const string VistaFormulario = "~/Views/Shared/Contraparte/Formulario.cshtml";

        private readonly AppDbContext _context;

        public ProveedorController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? q, int pagina = 1)
        {
            var query = _context.Proveedores.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var patron = $"%{EscaparLike(q.Trim())}%";
                query = query.Where(p => EF.Functions.ILike(p.Nombre, patron) || EF.Functions.ILike(p.Ruc, patron));
            }

            var total = await query.CountAsync();
            var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)TamanoPagina));
            pagina = Math.Clamp(pagina, 1, totalPaginas);

            var filas = await query
                .OrderBy(p => p.Nombre)
                .Skip((pagina - 1) * TamanoPagina)
                .Take(TamanoPagina)
                .Select(p => new ContraparteFila(
                    p.IdProveedor, p.Nombre, p.Ruc, p.Contacto, p.Direccion, p.Celular,
                    p.Movimientos.Count,
                    p.Movimientos.Max(m => (DateTime?)m.Fecha)))
                .ToListAsync();

            return View(VistaLista, new ContraparteListaViewModel
            {
                Tipo = ContraparteTipo.Proveedor,
                Busqueda = q,
                Pagina = pagina,
                TotalPaginas = totalPaginas,
                Total = total,
                Filas = filas
            });
        }

        public IActionResult Crear() => Formulario(new ContraparteFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(ContraparteFormViewModel model)
        {
            await Validar(model, null);
            if (!ModelState.IsValid)
                return Formulario(model);

            var proveedor = new Proveedor();
            Copiar(model, proveedor);
            _context.Proveedores.Add(proveedor);
            if (!await GuardarAsync())
                return Formulario(model);

            TempData["Exito"] = $"Proveedor «{proveedor.Nombre}» registrado.";
            return RedirectToAction(nameof(Index));
        }

        // Alta rápida desde el modal de Entrada: responde JSON para no recargar ni perder el carrito
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearRapido(ContraparteFormViewModel model)
        {
            await Validar(model, null);
            if (ModelState.IsValid)
            {
                var proveedor = new Proveedor();
                Copiar(model, proveedor);
                _context.Proveedores.Add(proveedor);
                if (await GuardarAsync())
                    return Json(new { success = true, id = proveedor.IdProveedor, nombre = proveedor.Nombre, celular = proveedor.Celular });
            }

            var errores = ModelState
                .Where(kv => kv.Value!.Errors.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors[0].ErrorMessage);
            return Json(new { success = false, errores });
        }

        public async Task<IActionResult> Editar(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
                return NotFound();

            return Formulario(new ContraparteFormViewModel
            {
                Id = proveedor.IdProveedor,
                Nombre = proveedor.Nombre,
                Documento = proveedor.Ruc,
                Contacto = proveedor.Contacto,
                Direccion = proveedor.Direccion,
                Celular = proveedor.Celular
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, ContraparteFormViewModel model)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
                return NotFound();

            model.Id = id;
            await Validar(model, id);
            if (!ModelState.IsValid)
                return Formulario(model);

            Copiar(model, proveedor);
            if (!await GuardarAsync())
                return Formulario(model);

            TempData["Exito"] = $"Proveedor «{proveedor.Nombre}» actualizado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
                return NotFound();

            // Borrarlo dejaría entradas sin proveedor y rompería el historial
            var entradas = await _context.Movimientos.CountAsync(m => m.IdProveedor == id);
            if (entradas > 0)
            {
                TempData["Error"] = $"No se puede eliminar «{proveedor.Nombre}»: tiene {entradas} {(entradas == 1 ? "entrada registrada" : "entradas registradas")}.";
                return RedirectToAction(nameof(Index));
            }

            _context.Proveedores.Remove(proveedor);
            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Proveedor «{proveedor.Nombre}» eliminado.";
            return RedirectToAction(nameof(Index));
        }

        [NonAction]
        private IActionResult Formulario(ContraparteFormViewModel model)
        {
            model.Tipo = ContraparteTipo.Proveedor;
            return View(VistaFormulario, model);
        }

        // En Perú el RUC siempre tiene 11 dígitos (la BD no lo restringe, solo exige que sea único)
        [NonAction]
        private async Task Validar(ContraparteFormViewModel model, int? idActual)
        {
            model.Documento = (model.Documento ?? "").Trim();
            if (model.Documento.Length == 0)
            {
                ModelState.AddModelError(nameof(model.Documento), "Ingresa el RUC.");
                return;
            }

            if (!Regex.IsMatch(model.Documento, @"^\d{11}$"))
            {
                ModelState.AddModelError(nameof(model.Documento), "El RUC debe tener 11 dígitos, sin letras ni espacios.");
                return;
            }

            var duplicados = _context.Proveedores.Where(p => p.Ruc == model.Documento);
            if (idActual.HasValue)
                duplicados = duplicados.Where(p => p.IdProveedor != idActual.Value);

            var existente = await duplicados.Select(p => p.Nombre).FirstOrDefaultAsync();
            if (existente != null)
                ModelState.AddModelError(nameof(model.Documento), $"Ya existe un proveedor con este RUC: {existente}.");
        }

        [NonAction]
        private static void Copiar(ContraparteFormViewModel model, Proveedor proveedor)
        {
            proveedor.Nombre = model.Nombre.Trim();
            proveedor.Ruc = model.Documento!;
            proveedor.Contacto = string.IsNullOrWhiteSpace(model.Contacto) ? null : model.Contacto.Trim();
            proveedor.Direccion = string.IsNullOrWhiteSpace(model.Direccion) ? null : model.Direccion.Trim();
            proveedor.Celular = string.IsNullOrWhiteSpace(model.Celular) ? null : model.Celular.Trim();
        }

        // Si otro usuario registró el mismo RUC entre la validación y el guardado
        [NonAction]
        private async Task<bool> GuardarAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                ModelState.AddModelError(nameof(ContraparteFormViewModel.Documento), "Ya existe un proveedor con este RUC.");
                return false;
            }
        }

        [NonAction]
        private static string EscaparLike(string texto) =>
            texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
