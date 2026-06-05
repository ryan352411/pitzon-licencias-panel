using Npgsql;
using RefaccionariaPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; // <-- Necesario para tareas asíncronas
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RefaccionariaPOS.Views
{
    /// <summary>
    /// Lógica de interacción para MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        // Variable para almacenar el rol del usuario que inició sesión
        private string rolUsuarioActual;

        // Objeto de conexión dedicado exclusivamente a escuchar alertas de PostgreSQL
        private NpgsqlConnection? conexionListener;

        // Modificamos el constructor para recibir el rol desde el Login
        public MainView(string rol)
        {
            InitializeComponent();

            this.rolUsuarioActual = rol;
            ConfigurarPermisosPorRol();

            // Iniciamos el motor de escucha en segundo plano en cuanto carga el menú
            _ = IniciarListenerNotificacionesAsync();
        }

        /// <summary>
        /// Evalúa el rol y desactiva las funciones prohibidas si es Vendedor
        /// </summary>
        private void ConfigurarPermisosPorRol()
        {
            lblRolVisual.Text = rolUsuarioActual;

            if (rolUsuarioActual == "Vendedor")
            {
                btnInventario.IsEnabled = false;
                btnHistorial.IsEnabled = false;
                btnCorteCaja.IsEnabled = false;
                btnUsuarios.IsEnabled = false; // <-- BLOQUEADO PARA EL VENDEDOR

                lblSubtitulo.Text = "Terminal de cobro activa. Registra las ventas del mostrador.";
            }
        }

        // =========================================================================
        // MOTOR EN TIEMPO REAL: ESCUCHA DE ALERTAS DEL TALLER
        // =========================================================================
        private async Task IniciarListenerNotificacionesAsync()
        {
            try
            {
                DatabaseConnection db = new DatabaseConnection();
                conexionListener = db.GetConnection();
                await conexionListener.OpenAsync();

                // Suscribimos el evento que reaccionará cuando Postgres lance un aviso
                conexionListener.Notification += (sender, e) =>
                {
                    string idOrden = e.Payload;

                    // Usamos Dispatcher.Invoke porque estamos en un hilo secundario y necesitamos tocar la UI
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"¡Nueva solicitud de refacciones del Taller!\nPor favor revisa la orden No. {idOrden} en tu Bandeja de Despacho.",
                                        "Alerta Express - Surtido Requerido", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    });
                };

                // Le decimos a Postgres a qué "canal" nos queremos suscribir
                using (var cmd = new NpgsqlCommand("LISTEN alerta_despacho;", conexionListener))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // Ciclo infinito que mantiene la conexión dormida y esperando (no consume recursos)
                while (true)
                {
                    await conexionListener.WaitAsync();
                }
            }
            catch (Exception ex)
            {
                // Si la red parpadea y se desconecta, lo registramos pero no crasheamos la app
                Console.WriteLine("Desconectado de las alertas en tiempo real: " + ex.Message);
            }
        }

        // Importante: Liberar la conexión asíncrona si cerramos la aplicación
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (conexionListener != null && conexionListener.State == System.Data.ConnectionState.Open)
            {
                conexionListener.Dispose();
            }
        }

        // =========================================================================
        // EVENTOS DE BOTONES (Originales y Nuevos)
        // =========================================================================

        // Botón (que agregaremos al XAML después) para abrir la Bandeja de Despacho
        private void BtnBandejaDespacho_Click(object sender, RoutedEventArgs e)
        {
            BandejaDespachoView ventanaBandeja = new BandejaDespachoView();
            ventanaBandeja.ShowDialog();
        }

        private void BtnUsuarios_Click(object sender, RoutedEventArgs e)
        {
            RegistrarUsuarioView ventanaUsuarios = new RegistrarUsuarioView();
            ventanaUsuarios.ShowDialog();
        }

        private void BtnInventario_Click(object sender, RoutedEventArgs e)
        {
            InventarioView ventanaInventario = new InventarioView();
            ventanaInventario.ShowDialog();
        }

        private void BtnVenta_Click(object sender, RoutedEventArgs e)
        {
            VentaView ventanaVenta = new VentaView();
            ventanaVenta.ShowDialog();
        }

        private void BtnHistorial_Click(object sender, RoutedEventArgs e)
        {
            HistorialVentasView ventanaHistorial = new HistorialVentasView();
            ventanaHistorial.ShowDialog();
        }

        private void BtnCorteCaja_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DatabaseConnection db = new DatabaseConnection();

                List<CorteCajaResumen> resumenes = new List<CorteCajaResumen>();

                string fechaHoyTexto = DateTime.Now.ToString("yyyy-MM-dd");

                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    string queryCorte = @"
                        SELECT
                            CASE
                                WHEN metodo_pago ILIKE 'Taller - Refacciones%' THEN 'Taller - Refacciones'
                                WHEN metodo_pago ILIKE 'Taller -%' THEN 'Taller - Servicio'
                                WHEN metodo_pago = 'Surtido Taller' THEN 'Surtido a taller'
                                ELSE 'Mostrador'
                            END AS origen,
                            COUNT(*) AS tickets,
                            COALESCE(SUM(total), 0) AS total
                        FROM ventas
                        WHERE to_char(fecha_venta, 'YYYY-MM-DD') = @hoy
                        GROUP BY origen
                        ORDER BY origen;";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(queryCorte, conexion))
                    {
                        cmd.Parameters.AddWithValue("@hoy", fechaHoyTexto);

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                resumenes.Add(new CorteCajaResumen
                                {
                                    Origen = reader["origen"].ToString() ?? "Sin clasificar",
                                    Tickets = Convert.ToInt32(reader["tickets"]),
                                    Total = Convert.ToDecimal(reader["total"])
                                });
                            }
                        }
                    }
                }

                int totalVentas = resumenes.Sum(item => item.Tickets);
                decimal dineroAcumuladoCaja = resumenes.Sum(item => item.Total);
                StringBuilder detalleOrigenes = new StringBuilder();

                foreach (CorteCajaResumen item in resumenes)
                {
                    detalleOrigenes.AppendLine($"{item.Origen}: {item.Tickets} ticket(s) | {item.Total:C}");
                }

                if (resumenes.Count == 0)
                {
                    detalleOrigenes.AppendLine("Sin ventas registradas hoy.");
                }

                string mensajeReporte = $"=== CORTE DE CAJA DIARIO ===\n\n" +
                                        $"Fecha: {DateTime.Now:dd/MM/yyyy}\n" +
                                        $"----------------------------------------\n" +
                                        $"Tickets Emitidos: {totalVentas}\n" +
                                        $"Total General: {dineroAcumuladoCaja:C}\n\n" +
                                        $"=== DESGLOSE POR ORIGEN ===\n" +
                                        detalleOrigenes +
                                        $"\n" +
                                        $"¿El dinero coincide con físico?";

                MessageBox.Show(mensajeReporte, "Corte de Caja Exitoso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar el corte de caja: " + ex.Message, "Error de Reportes", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult resultado = MessageBox.Show("¿Seguro que deseas cerrar la sesión actual?", "Cerrar Sesión", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (resultado == MessageBoxResult.Yes)
            {
                LoginView pantallaLogin = new LoginView();
                pantallaLogin.Show();
                this.Close();
            }
        }

        private class CorteCajaResumen
        {
            public string Origen { get; set; } = string.Empty;
            public int Tickets { get; set; }
            public decimal Total { get; set; }
        }
    }
}
