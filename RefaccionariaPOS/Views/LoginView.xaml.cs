using System;
using System.Text;
using System.Security.Cryptography; // Biblioteca oficial de Microsoft para criptografía segura
using System.Windows;
using Npgsql;
using RefaccionariaPOS.Data;

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
            // .Trim() elimina espacios accidentales al inicio o final del texto
            string usuario = txtUsuario.Text.Trim();
            string password = txtPassword.Password.Trim();

            // Validación previa en el cliente para evitar consultas innecesarias a la BD
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Por favor, ingresa tu usuario y contraseña.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Encriptación unidireccional de la contraseña antes de viajar a la BD
            string passwordEncriptada = EncriptarSHA256(password);

            try
            {
                DatabaseConnection db = new DatabaseConnection();

                // Los bloques 'using' garantizan que la conexión se cierre y libere incluso si ocurre un error inesperado
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    // 2. Consulta Parametrizada: Mitiga ataques de Inyección SQL.
                    // ILIKE flexibiliza el nombre de usuario (case-insensitive) pero la contraseña sigue siendo estricta.
                    string query = "SELECT rol FROM usuarios WHERE username ILIKE @user AND password_hash = @pass";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        // Los parámetros limpian y escapan automáticamente cualquier carácter peligroso
                        cmd.Parameters.AddWithValue("@user", usuario);
                        cmd.Parameters.AddWithValue("@pass", passwordEncriptada);

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Recuperamos el rol de forma segura manejando posibles nulos en BD
                                string rolObtenido = reader["rol"] != DBNull.Value ? reader["rol"].ToString() : "Vendedor";

                                // 3. Inicio de sesión exitoso: Redirección al módulo principal con privilegios asignados
                                MainView mainWindow = new MainView(rolObtenido);
                                mainWindow.Show();

                                // Destruimos la ventana de login actual de la memoria del sistema
                                this.Close();
                            }
                            else
                            {
                                // Mensaje genérico de error (Buena práctica de seguridad: No le dice al atacante si falló el usuario o la contraseña)
                                MessageBox.Show("Usuario o contraseña incorrectos.", "Error de Acceso", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Error a nivel de infraestructura/red (no revela datos sensibles de la consulta)
                MessageBox.Show("No se pudo establecer comunicación segura con el servidor de datos: " + ex.Message, "Error del Sistema", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Computa el Hash SHA-256 de una cadena en texto plano de forma segura en memoria.
        /// </summary>
        private string EncriptarSHA256(string textoPlano)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // Convierte la cadena de texto a un flujo de bytes UTF-8 estándar
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(textoPlano));

                // Convierte el arreglo de bytes resultante en una cadena Hexadecimal formateada en minúsculas
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