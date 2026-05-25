using System;
using System.Windows;
using Npgsql;
using RefaccionariaPOS.Data;

namespace RefaccionariaPOS.Views
{
    public partial class RegistrarProductoView : Window
    {
        public RegistrarProductoView()
        {
            InitializeComponent();
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Validar que los campos obligatorios no estén vacíos
            if (string.IsNullOrWhiteSpace(txtCodigo.Text) || string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MessageBox.Show("Por favor, llena el Código y el Nombre.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                DatabaseConnection db = new DatabaseConnection();
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    // SQL adaptado a tus columnas exactas
                    string query = @"INSERT INTO productos (codigo_barras, nombre, descripcion, costo_proveedor, precio_venta, stock_actual) 
                                     VALUES (@codigo, @nombre, @desc, @costo, @venta, @stock)";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@codigo", txtCodigo.Text);
                        cmd.Parameters.AddWithValue("@nombre", txtNombre.Text);
                        cmd.Parameters.AddWithValue("@desc", txtDescripcion.Text);
                        cmd.Parameters.AddWithValue("@costo", Convert.ToDecimal(txtCosto.Text));
                        cmd.Parameters.AddWithValue("@venta", Convert.ToDecimal(txtPrecioVenta.Text));
                        cmd.Parameters.AddWithValue("@stock", Convert.ToInt32(txtStock.Text));

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Producto registrado con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true; // Indica que se guardó correctamente
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar el producto: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}