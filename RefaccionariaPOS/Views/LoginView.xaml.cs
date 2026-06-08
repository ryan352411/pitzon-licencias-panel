using System;
using System.Windows;
using System.Windows.Input;
using Npgsql;
using RefaccionariaPOS.Data;
using RefaccionariaPOS.Security;

namespace RefaccionariaPOS.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private void BtnEntrar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtUsuario.Text.Trim();
            string password = txtPassword.Password.Trim();

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Por favor, ingresa tu usuario y contraseña.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                DatabaseConnection db = new DatabaseConnection();
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    string query = "SELECT id, username, rol, password_hash FROM usuarios WHERE username ILIKE @user LIMIT 1";
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@user", usuario);

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read() && PasswordHasher.Verify(password, reader["password_hash"].ToString() ?? string.Empty))
                            {
                                int idUsuario = Convert.ToInt32(reader["id"]);
                                string username = reader["username"].ToString() ?? usuario;
                                string rolObtenido = reader["rol"] != DBNull.Value ? reader["rol"].ToString() ?? "Vendedor" : "Vendedor";

                                MainView mainWindow = new MainView(idUsuario, username, rolObtenido);
                                mainWindow.Show();
                                Close();
                            }
                            else
                            {
                                MessageBox.Show("Usuario o contraseña incorrectos.", "Error de Acceso", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo establecer comunicación con el servidor de datos: " + ex.Message, "Error del Sistema", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Credentials_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            BtnEntrar_Click(sender, new RoutedEventArgs());
        }
    }
}
