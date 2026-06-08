using Npgsql;
using RefaccionariaPOS.Data;
using RefaccionariaPOS.Security;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace RefaccionariaPOS.Views
{
    public partial class RegistrarUsuarioView : Window
    {
        public RegistrarUsuarioView()
        {
            InitializeComponent();
            CargarUsuarios();
        }

        private void BtnGuardarUsuario_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtNuevoUsuario.Text.Trim();
            string password = txtNuevaPassword.Password.Trim();
            string? rolSeleccionado = (cmbRol.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(rolSeleccionado))
            {
                MessageBox.Show("Por favor, llena todos los campos para continuar.", "Campos vacíos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string passwordEncriptada = PasswordHasher.Hash(password);

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
                            MessageBox.Show($"Usuario '{usuario}' registrado como {rolSeleccionado}.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                            txtNuevoUsuario.Clear();
                            txtNuevaPassword.Clear();
                            cmbRol.SelectedIndex = 0;
                            CargarUsuarios();
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

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            CargarUsuarios();
        }

        private void CargarUsuarios()
        {
            List<UsuarioSistema> usuarios = new List<UsuarioSistema>();

            try
            {
                DatabaseConnection db = new DatabaseConnection();
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();
                    string query = @"SELECT id, username, COALESCE(rol, 'Vendedor') AS rol, COALESCE(fecha_alta, CURRENT_TIMESTAMP) AS fecha_alta
                                     FROM usuarios
                                     ORDER BY fecha_alta DESC, username ASC;";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            usuarios.Add(new UsuarioSistema
                            {
                                Id = Convert.ToInt32(reader["id"]),
                                Username = reader["username"].ToString() ?? string.Empty,
                                Rol = reader["rol"].ToString() ?? "Vendedor",
                                FechaAlta = Convert.ToDateTime(reader["fecha_alta"])
                            });
                        }
                    }
                }

                dgUsuarios.ItemsSource = usuarios;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar usuarios: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class UsuarioSistema
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public DateTime FechaAlta { get; set; }
    }
}
