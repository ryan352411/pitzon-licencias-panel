using Npgsql;
using RefaccionariaPOS.Data;
using RefaccionariaPOS.Security;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace RefaccionariaPOS.Views
{
    public partial class MainView : Window
    {
        private const string RolVendedor = "Vendedor";
        private const string CanalAlertaDespacho = "alerta_despacho";

        private readonly int idUsuarioActual;
        private readonly string usuarioActual;
        private readonly string rolUsuarioActual;
        private readonly CancellationTokenSource listenerCancellation = new();
        private readonly DispatcherTimer demoTimer = new DispatcherTimer();
        private NpgsqlConnection? conexionListener;

        public MainView(int idUsuario, string usuario, string rol)
        {
            InitializeComponent();

            idUsuarioActual = idUsuario;
            usuarioActual = usuario;
            rolUsuarioActual = rol;

            ConfigurarPermisosPorRol();
            ConfigurarContadorDemo();
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

        private void ConfigurarContadorDemo()
        {
            demoTimer.Interval = TimeSpan.FromMinutes(1);
            demoTimer.Tick += (_, _) => ActualizarContadorDemo();
            ActualizarContadorDemo();
            demoTimer.Start();
        }

        private void ActualizarContadorDemo()
        {
            TimeSpan restante = DemoLicenseService.GetRemainingTime();
            lblDemoRestante.Text = DemoLicenseService.FormatRemainingTime(restante);

            if (restante > TimeSpan.Zero)
            {
                return;
            }

            demoTimer.Stop();
            BloquearDemoVencido();
        }

        private void BloquearDemoVencido()
        {
            btnVenta.IsEnabled = false;
            btnBandejaDespacho.IsEnabled = false;
            btnInventario.IsEnabled = false;
            btnHistorial.IsEnabled = false;
            btnCorteCaja.IsEnabled = false;
            btnUsuarios.IsEnabled = false;

            MessageBox.Show(
                "El demo de 1 mes ha vencido. Contacta al proveedor para activar la aplicacion.",
                "Demo vencido",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Application.Current.Shutdown();
        }

        private async Task IniciarListenerNotificacionesAsync()
        {
            try
            {
                CancellationToken cancellationToken = listenerCancellation.Token;
                DatabaseConnection db = new DatabaseConnection();
                conexionListener = db.GetConnection();
                await conexionListener.OpenAsync(cancellationToken);

                conexionListener.Notification += (_, e) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"Nueva solicitud de refacciones del Taller.\nPor favor revisa la orden No. {e.Payload} en tu Bandeja de Despacho.",
                            "Alerta Express - Surtido Requerido",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);
                    });
                };

                using (var cmd = new NpgsqlCommand($"LISTEN {CanalAlertaDespacho};", conexionListener))
                {
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    await conexionListener.WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // El listener se cancela al cerrar sesion o cerrar la ventana.
            }
            catch (Exception ex)
            {
                Console.WriteLine("Desconectado de las alertas en tiempo real: " + ex.Message);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            CerrarListenerNotificaciones();
            demoTimer.Stop();
            listenerCancellation.Dispose();
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

        private void CerrarListenerNotificaciones()
        {
            if (!listenerCancellation.IsCancellationRequested)
            {
                listenerCancellation.Cancel();
            }

            if (conexionListener?.State == ConnectionState.Open)
            {
                conexionListener.Dispose();
            }
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
