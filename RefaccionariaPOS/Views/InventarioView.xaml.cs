using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using RefaccionariaPOS.Data;
using RefaccionariaPOS.Models;

namespace RefaccionariaPOS.Views
{
    public partial class InventarioView : Window
    {
        private readonly ObservableCollection<string> categorias = new ObservableCollection<string>();
        private bool filtrosListos;

        public InventarioView()
        {
            InitializeComponent();
            VerificarColumnasInventario();
            cmbCategoria.ItemsSource = categorias;
            CargarCategorias();
            CargarProductos();
            filtrosListos = true;
        }

        private void CargarProductos(string terminoBusqueda = "")
        {
            List<Producto> listaProductos = new List<Producto>();

            try
            {
                DatabaseConnection db = new DatabaseConnection();
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    string categoriaSeleccionada = cmbCategoria.SelectedItem?.ToString() ?? "Todas";
                    bool soloBajoStock = chkBajoStock.IsChecked == true;

                    string query = @"SELECT id, codigo_barras, nombre, descripcion, costo_proveedor, precio_venta,
                                            stock_actual, stock_minimo, categoria
                                     FROM productos
                                     WHERE (nombre ILIKE @busqueda OR codigo_barras ILIKE @busqueda OR descripcion ILIKE @busqueda)
                                       AND (@categoria = 'Todas' OR categoria = @categoria)
                                       AND (@soloBajoStock = false OR stock_actual <= stock_minimo)
                                     ORDER BY nombre ASC;";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@busqueda", "%" + terminoBusqueda + "%");
                        cmd.Parameters.AddWithValue("@categoria", categoriaSeleccionada);
                        cmd.Parameters.AddWithValue("@soloBajoStock", soloBajoStock);

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                listaProductos.Add(new Producto
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    CodigoBarras = reader["codigo_barras"].ToString() ?? string.Empty,
                                    Nombre = reader["nombre"].ToString() ?? string.Empty,
                                    Descripcion = reader["descripcion"].ToString() ?? string.Empty,
                                    Categoria = reader["categoria"].ToString() ?? "General",
                                    PrecioCompra = Convert.ToDecimal(reader["costo_proveedor"]),
                                    PrecioVenta = Convert.ToDecimal(reader["precio_venta"]),
                                    Stock = Convert.ToInt32(reader["stock_actual"]),
                                    StockMinimo = Convert.ToInt32(reader["stock_minimo"])
                                });
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
            frm.Owner = this;

            if (frm.ShowDialog() == true)
            {
                CargarCategorias();
                CargarProductos(txtBuscar.Text.Trim());
            }
        }

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            CargarProductos(txtBuscar.Text.Trim());
        }

        private void CmbCategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (filtrosListos)
            {
                CargarProductos(txtBuscar.Text.Trim());
            }
        }

        private void ChkBajoStock_Click(object sender, RoutedEventArgs e)
        {
            CargarProductos(txtBuscar.Text.Trim());
        }

        private void BtnLimpiarFiltros_Click(object sender, RoutedEventArgs e)
        {
            txtBuscar.Clear();
            cmbCategoria.SelectedIndex = 0;
            chkBajoStock.IsChecked = false;
            CargarProductos();
        }

        private void MenuActualizarStock_Click(object sender, RoutedEventArgs e)
        {
            if (dgInventario.SelectedItem is Producto productoSeleccionado)
            {
                string? nuevoStockStr = PedirValor("Actualizar Stock",
                                                  $"Ingresa el nuevo stock físico para:\n{productoSeleccionado.Nombre}",
                                                  productoSeleccionado.Stock.ToString());

                if (int.TryParse(nuevoStockStr, out int nuevoStock) && nuevoStock >= 0)
                {
                    ActualizarStockEnBaseDeDatos(productoSeleccionado.CodigoBarras, nuevoStock);
                    CargarProductos(txtBuscar.Text.Trim());
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

        private void CargarCategorias()
        {
            string categoriaActual = cmbCategoria.SelectedItem?.ToString() ?? "Todas";
            categorias.Clear();
            categorias.Add("Todas");

            try
            {
                DatabaseConnection db = new DatabaseConnection();
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();
                    string query = "SELECT DISTINCT categoria FROM productos WHERE categoria IS NOT NULL AND categoria <> '' ORDER BY categoria;";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            categorias.Add(reader["categoria"].ToString() ?? "General");
                        }
                    }
                }
            }
            catch
            {
                if (!categorias.Contains("General"))
                {
                    categorias.Add("General");
                }
            }

            cmbCategoria.SelectedItem = categorias.Contains(categoriaActual) ? categoriaActual : "Todas";
        }

        private void VerificarColumnasInventario()
        {
            try
            {
                DatabaseConnection db = new DatabaseConnection();
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();
                    string query = @"SELECT COUNT(*)
                                     FROM information_schema.columns
                                     WHERE table_schema = 'public'
                                       AND table_name = 'productos'
                                       AND column_name IN ('stock_minimo', 'categoria');";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        int columnas = Convert.ToInt32(cmd.ExecuteScalar());
                        if (columnas < 2)
                        {
                            MessageBox.Show("Faltan las columnas categoria o stock_minimo en productos. Ejecuta la migración de inventario antes de usar estos filtros.", "Inventario", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo verificar la estructura de inventario: " + ex.Message, "Inventario", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string? PedirValor(string titulo, string mensaje, string valorActual)
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

            Button btnAceptar = new Button { Content = "Guardar Stock", Width = 110, Margin = new Thickness(0, 15, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            btnAceptar.IsDefault = true;
            btnAceptar.Click += (s, ev) => ventana.DialogResult = true;

            panel.Children.Add(btnAceptar);
            ventana.Content = panel;

            txtInput.Focus();

            if (ventana.ShowDialog() == true)
            {
                return txtInput.Text;
            }

            return null;
        }
    }
}
