namespace GestionAlmacen_Golocentro.ViewModels
{
        public class AlertaViewModel
        {
            public int IdAlerta { get; set; }
            public string Tipo { get; set; }
            public string Mensaje { get; set; }
            public DateTime? Fecha { get; set; }
            public string ProductoNombre { get; set; }
            public string SedeNombre { get; set; }
        }
    }

