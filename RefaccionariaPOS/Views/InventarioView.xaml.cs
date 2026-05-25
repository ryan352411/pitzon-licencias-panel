using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls; // Agregado para el TextChangedEventArgs
using Npgsql;
using RefaccionariaPOS.Data;    // Para conectar a la BD
using RefaccionariaPOS.Models;  // Para usar nuestra clase Producto

namespace RefaccionariaPOS.Views
{
    public partial class InventarioView : Window
    {
        public InventarioView()
        {
            InitializeComponent();
            // Le decimos que cargue los productos en cuanto la ventana se abra
            CargarProductos();
        }

        // ==========================================================
        // 1. MÉTODO DE CARGA UNIFICADO CON EL BUSCADOR
        // ==========================================================
        private void CargarProductos(string terminoBusqueda = "")
        {
            List<Producto> listaProductos = new List<Producto>();

            try
            {
                DatabaseConnection db = new DatabaseConnection();
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    // Consulta combinada: trae tus datos originales pero filtra si hay texto
                    string query = @"SELECT id, codigo_barras, nombre, descripcion, costo_proveedor, precio_venta, stock_actual 
                                     FROM productos 
                                     WHERE nombre ILIKE @busqueda OR codigo_barras ILIKE @busqueda
                                     ORDER BY nombre ASC;";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@busqueda", "%" + terminoBusqueda + "%");

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Producto prod = new Producto
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    CodigoBarras = reader["codigo_barras"].ToString(),
                                    Nombre = reader["nombre"].ToString(),
                                    Descripcion = reader["descripcion"].ToString(),
                                    PrecioCompra = Convert.ToDecimal(reader["costo_proveedor"]),
                                    PrecioVenta = Convert.ToDecimal(reader["precio_venta"]),
                                    Stock = Convert.ToInt32(reader["stock_actual"])
                                };
                                listaProductos.Add(prod);
                            }
                        }
                    }
                }

                dgInventario.ItemsSource = listaProductos;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar el inventario: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            RegistrarProductoView frm = new RegistrarProductoView();
            frm.Owner = this; // Vincula la ventana flotante con el catálogo

            // Si el formulario se cerró tras guardar con éxito (DialogResult = true)
            if (frm.ShowDialog() == true)
            {
                // Recargamos manteniendo lo que esté escrito en el buscador
                CargarProductos(txtBuscar.Text.Trim());
            }
        }

        // ==========================================================
        // 2. EVENTO DEL BUSCADOR EN TIEMPO REAL
        // ==========================================================
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            CargarProductos(txtBuscar.Text.Trim());
        }

        // ==========================================================
        // 3. EVENTOS Y LÓGICA DE ACTUALIZACIÓN DE STOCK (CLIC DERECHO)
        // ==========================================================
        private void MenuActualizarStock_Click(object sender, RoutedEventArgs e)
        {
            if (dgInventario.SelectedItem is Producto productoSeleccionado)
            {
                string nuevoStockStr = PedirValor("Actualizar Stock",
                                                  $"Ingresa el nuevo stock físico para:\n{productoSeleccionado.Nombre}",
                                                  productoSeleccionado.Stock.ToString());

                if (int.TryParse(nuevoStockStr, out int nuevoStock) && nuevoStock >= 0)
                {
                    ActualizarStockEnBaseDeDatos(productoSeleccionado.CodigoBarras, nuevoStock);
                    CargarProductos(txtBuscar.Text.Trim()); // Recargamos para reflejar cambios
                }
                else if (nuevoStockStr != null)
                {
                    MessageBox.Show("Por favor, ingresa un número entero válido (no negativo).", "Dato inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ActualizarStockEnBaseDeDatos(string codigo, int nuevoStock)
        {
            try
            {
                DatabaseConnection db = new DatabaseConnection();
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();
                    string query = "UPDATE productos SET stock_actual = @stock WHERE codigo_barras = @codigo;";
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@stock", nuevoStock);
                        cmd.Parameters.AddWithValue("@codigo", codigo);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al actualizar el stock en la BD: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================================
        // 4. VENTANA FLOTANTE GENERADA EN CÓDIGO
        // ==========================================================
        private string PedirValor(string titulo, string mensaje, string valorActual)
        {
            Window ventana = new Window
            {
                Title = titulo,
                Width = 350,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock { Text = mensaje, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });

            TextBox txtInput = new TextBox { Text = valorActual };
            txtInput.SelectAll();
            panel.Children.Add(txtInput);

            Button btnAceptar = new Button { Content = "Guardar Stock", Width = 100, Margin = new Thickness(0, 15, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            btnAceptar.IsDefault = true;
            btnAceptar.Click += (s, ev) => ventana.DialogResult = true;

            panel.Children.Add(btnAceptar);
            ventana.Content = panel;

            txtInput.Focus();

            if (ventana.ShowDialog() == true)
                return txtInput.Text;

            return null;
        }
    }
}