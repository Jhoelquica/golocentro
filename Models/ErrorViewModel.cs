namespace GestionAlmacen_Golocentro.Models
{
    public class ErrorViewModel
    {
        public int Codigo { get; set; } = 500;

        // Código para reportar una falla interna (el mismo que queda en el log)
        public string? RequestId { get; set; }

        public string? RutaOriginal { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
