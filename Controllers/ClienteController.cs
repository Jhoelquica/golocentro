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
    public class ClienteController : Controller
    {
        private const int TamanoPagina = 10;
        private const string VistaLista = "~/Views/Shared/Contraparte/Lista.cshtml";
        private const string VistaFormulario = "~/Views/Shared/Contraparte/Formulario.cshtml";

        private readonly AppDbContext _context;

        public ClienteController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? q, int pagina = 1)
        {
            var query = _context.Clientes.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var patron = $"%{EscaparLike(q.Trim())}%";
                query = query.Where(c => EF.Functions.ILike(c.Nombre, patron) || EF.Functions.ILike(c.RucDni, patron));
            }

            var total = await query.CountAsync();
            var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)TamanoPagina));
            pagina = Math.Clamp(pagina, 1, totalPaginas);

            var filas = await query
                .OrderBy(c => c.Nombre)
                .Skip((pagina - 1) * TamanoPagina)
                .Take(TamanoPagina)
                .Select(c => new ContraparteFila(
                    c.IdCliente, c.Nombre, c.RucDni, c.Contacto, c.Direccion, c.Celular,
                    c.Movimientos.Count,
                    c.Movimientos.Max(m => (DateTime?)m.Fecha)))
                .ToListAsync();

            return View(VistaLista, new ContraparteListaViewModel
            {
                Tipo = ContraparteTipo.Cliente,
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

            var cliente = new Cliente();
            Copiar(model, cliente);
            _context.Clientes.Add(cliente);
            if (!await GuardarAsync())
                return Formulario(model);

            TempData["Exito"] = $"Cliente «{cliente.Nombre}» registrado.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Editar(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();
            if (EsClienteGeneral(cliente))
                return ClienteGeneralProtegido();

            return Formulario(new ContraparteFormViewModel
            {
                Id = cliente.IdCliente,
                Nombre = cliente.Nombre,
                Documento = cliente.RucDni,
                Contacto = cliente.Contacto,
                Direccion = cliente.Direccion,
                Celular = cliente.Celular
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, ContraparteFormViewModel model)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();
            if (EsClienteGeneral(cliente))
                return ClienteGeneralProtegido();

            model.Id = id;
            await Validar(model, id);
            if (!ModelState.IsValid)
                return Formulario(model);

            Copiar(model, cliente);
            if (!await GuardarAsync())
                return Formulario(model);

            TempData["Exito"] = $"Cliente «{cliente.Nombre}» actualizado.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();
            if (EsClienteGeneral(cliente))
                return ClienteGeneralProtegido();

            // Borrarlo dejaría ventas sin cliente y rompería el historial
            var ventas = await _context.Movimientos.CountAsync(m => m.IdCliente == id);
            if (ventas > 0)
            {
                TempData["Error"] = $"No se puede eliminar «{cliente.Nombre}»: tiene {ventas} {(ventas == 1 ? "venta registrada" : "ventas registradas")}.";
                return RedirectToAction(nameof(Index));
            }

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
            TempData["Exito"] = $"Cliente «{cliente.Nombre}» eliminado.";
            return RedirectToAction(nameof(Index));
        }

        [NonAction]
        private IActionResult Formulario(ContraparteFormViewModel model)
        {
            model.Tipo = ContraparteTipo.Cliente;
            return View(VistaFormulario, model);
        }

        // chk_cliente_rucdni pide entre 8 y 11 caracteres; además se exigen solo dígitos (DNI, CE o RUC)
        [NonAction]
        private async Task Validar(ContraparteFormViewModel model, int? idActual)
        {
            model.Documento = (model.Documento ?? "").Trim();
            if (model.Documento.Length == 0)
            {
                ModelState.AddModelError(nameof(model.Documento), "Ingresa el RUC o DNI.");
                return;
            }

            if (!Regex.IsMatch(model.Documento, @"^\d{8,11}$"))
            {
                ModelState.AddModelError(nameof(model.Documento), "El RUC o DNI debe tener entre 8 y 11 dígitos, sin letras ni espacios.");
                return;
            }

            var duplicados = _context.Clientes.Where(c => c.RucDni == model.Documento);
            if (idActual.HasValue)
                duplicados = duplicados.Where(c => c.IdCliente != idActual.Value);

            var existente = await duplicados.Select(c => c.Nombre).FirstOrDefaultAsync();
            if (existente != null)
                ModelState.AddModelError(nameof(model.Documento), $"Ya existe un cliente con este RUC/DNI: {existente}.");
        }

        [NonAction]
        private static void Copiar(ContraparteFormViewModel model, Cliente cliente)
        {
            cliente.Nombre = model.Nombre.Trim();
            cliente.RucDni = model.Documento!;
            cliente.Contacto = string.IsNullOrWhiteSpace(model.Contacto) ? null : model.Contacto.Trim();
            cliente.Direccion = string.IsNullOrWhiteSpace(model.Direccion) ? null : model.Direccion.Trim();
            cliente.Celular = string.IsNullOrWhiteSpace(model.Celular) ? null : model.Celular.Trim();
        }

        // «Público en general» es el cliente por defecto de las ventas: no se edita ni se borra
        [NonAction]
        private static bool EsClienteGeneral(Cliente cliente) => cliente.RucDni == ClienteGeneral.RucDni;

        [NonAction]
        private IActionResult ClienteGeneralProtegido()
        {
            TempData["Error"] = "«Público en general» es el cliente por defecto de las ventas: no se puede editar ni eliminar.";
            return RedirectToAction(nameof(Index));
        }

        // Si otro usuario registró el mismo documento entre la validación y el guardado
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
                ModelState.AddModelError(nameof(ContraparteFormViewModel.Documento), "Ya existe un cliente con este RUC/DNI.");
                return false;
            }
        }

        [NonAction]
        private static string EscaparLike(string texto) =>
            texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
