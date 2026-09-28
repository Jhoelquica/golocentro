using GestionAlmacen_Golocentro.Data;
using GestionAlmacen_Golocentro.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace GestionAlmacen_Golocentro.Services
{
    public static class NegocioInfo
    {
        // La tabla negocio tiene una sola fila; si faltara, la nota sale con los datos por defecto
        public static async Task<DatosNegocio> Cargar(AppDbContext db)
        {
            var n = await db.Negocios.AsNoTracking().FirstOrDefaultAsync();
            if (n == null)
                return DatosNegocio.PorDefecto;

            return new DatosNegocio(
                n.NombreComercial,
                n.RazonSocial,
                n.Ruc,
                n.Telefono,
                n.Correo,
                string.IsNullOrWhiteSpace(n.MensajeNota) ? DatosNegocio.MensajePorDefecto : n.MensajeNota);
        }
    }
}
