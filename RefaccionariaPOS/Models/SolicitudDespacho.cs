namespace RefaccionariaPOS.Models
{
    public class SolicitudDespacho
    {
        public int IdDetalle { get; set; }
        public int IdOrden { get; set; }
        public string CodigoBarras { get; set; } = string.Empty;
        public int Cantidad { get; set; }

        // Agrego el nombre por si lo necesitas mostrar en la tabla de tu pantalla
        public string NombreProducto { get; set; } = string.Empty;
    }
}
