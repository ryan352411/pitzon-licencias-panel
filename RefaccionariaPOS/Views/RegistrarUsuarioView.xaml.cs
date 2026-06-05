using Npgsql;
using RefaccionariaPOS.Data;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace RefaccionariaPOS.Views
{
    public partial class RegistrarUsuarioView : Window
    {
        public RegistrarUsuarioView()
        {
            InitializeComponent();
        }

        private void BtnGuardarUsuario_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtNuevoUsuario.Text.Trim();
            string password = txtNuevaPassword.Password.Trim();
            string? rolSeleccionado = (cmbRol.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(rolSeleccionado))
            {
                MessageBox.Show("Por favor, llena todos los campos para continuar.", "Campos Vacíos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string passwordEncriptada = EncriptarSHA256(password);

            try
            {
                DatabaseConnection db = new DatabaseConnection();
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    string queryInsertar = @"INSERT INTO usuarios (username, password_hash, rol) 
                                            VALUES (@user, @pass, @rol)
                                            ON CONFLICT (username) DO NOTHING;";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(queryInsertar, conexion))
                    {
                        cmd.Parameters.AddWithValue("@user", usuario);
                        cmd.Parameters.AddWithValue("@pass", passwordEncriptada);
                        cmd.Parameters.AddWithValue("@rol", rolSeleccionado);

                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                        {
                            MessageBox.Show($"¡Usuario '{usuario}' registrado como {rolSeleccionado}!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                            txtNuevoUsuario.Clear();
                            txtNuevaPassword.Clear();
                            cmbRol.SelectedIndex = 0;
                        }
                        else
                        {
                            MessageBox.Show("Ese nombre de usuario ya existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string EncriptarSHA256(string textoPlano)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(textoPlano));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
