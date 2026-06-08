using Npgsql;
using RefaccionariaPOS.Data;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace RefaccionariaPOS.Views
{
    public partial class MainView : Window
    {
        private const string RolVendedor = "Vendedor";
        private const string CanalAlertaDespacho = "alerta_despacho";

        private readonly int idUsuarioActual;
        private readonly string usuarioActual;
        private readonly string rolUsuarioActual;
        private NpgsqlConnection? conexionListener;

        public MainView(int idUsuario, string usuario, string rol)
        {
            InitializeComponent();

            idUsuarioActual = idUsuario;
            usuarioActual = usuario;
            rolUsuarioActual = rol;

            ConfigurarPermisosPorRol();
            _ = IniciarListenerNotificacionesAsync();
        }

        private void ConfigurarPermisosPorRol()
        {
            lblRolVisual.Text = $"{usuarioActual} - {rolUsuarioActual}";

            if (!EsVendedorExacto())
            {
                return;
            }

            btnHistorial.IsEnabled = false;
            btnCorteCaja.IsEnabled = false;
            btnUsuarios.IsEnabled = false;
            lblSubtitulo.Text = "Terminal de cobro activa. Registra ventas y consulta inventario.";
        }

        private async Task IniciarListenerNotificacionesAsync()
        {
            try
            {
                DatabaseConnection db = new DatabaseConnection();
                conexionListener = db.GetConnection();
                await conexionListener.OpenAsync();

                conexionListener.Notification += (_, e) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"¡Nueva solicitud de refacciones del Taller!\nPor favor revisa la orden No. {e.Payload} en tu Bandeja de Despacho.",
                            "Alerta Express - Surtido Requerido",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);
                    });
                };

                using (var cmd = new NpgsqlCommand($"LISTEN {CanalAlertaDespacho};", conexionListener))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                while (true)
                {
                    await conexionListener.WaitAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Desconectado de las alertas en tiempo real: " + ex.Message);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (conexionListener?.State == System.Data.ConnectionState.Open)
            {
                conexionListener.Dispose();
            }
        }

        private void BtnBandejaDespacho_Click(object sender, RoutedEventArgs e)
        {
            MostrarDialogo(new BandejaDespachoView(idUsuarioActual));
        }

        private void BtnUsuarios_Click(object sender, RoutedEventArgs e)
        {
            MostrarDialogo(new RegistrarUsuarioView());
        }

        private void BtnInventario_Click(object sender, RoutedEventArgs e)
        {
            MostrarDialogo(new InventarioView(EsVendedorInventario()));
        }

        private void BtnVenta_Click(object sender, RoutedEventArgs e)
        {
            MostrarDialogo(new VentaView(idUsuarioActual));
        }

        private void BtnHistorial_Click(object sender, RoutedEventArgs e)
        {
            MostrarDialogo(new HistorialVentasView());
        }

        private void BtnCorteCaja_Click(object sender, RoutedEventArgs e)
        {
            MostrarDialogo(new CorteCajaView());
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult resultado = MessageBox.Show(
                "¿Seguro que deseas cerrar la sesión actual?",
                "Cerrar Sesión",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resultado != MessageBoxResult.Yes)
            {
                return;
            }

            LoginView pantallaLogin = new LoginView();
            pantallaLogin.Show();
            Close();
        }

        private bool EsVendedorExacto()
        {
            return rolUsuarioActual == RolVendedor;
        }

        private bool EsVendedorInventario()
        {
            return rolUsuarioActual.Equals(RolVendedor, StringComparison.OrdinalIgnoreCase);
        }

        private static void MostrarDialogo(Window ventana)
        {
            ventana.ShowDialog();
        }
    }
}
