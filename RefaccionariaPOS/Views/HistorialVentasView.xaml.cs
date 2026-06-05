using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Npgsql;
using RefaccionariaPOS.Data;
using RefaccionariaPOS.Models;

namespace RefaccionariaPOS.Views
{
    public partial class HistorialVentasView : Window
    {
        public HistorialVentasView()
        {
            InitializeComponent();
            CargarHistorial();
        }

        private void CargarHistorial()
        {
            List<Venta> listaVentas = new List<Venta>();

            try
            {
                DatabaseConnection db = new DatabaseConnection();

                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    string query = "SELECT id, folio, fecha_venta, total, estado, metodo_pago FROM ventas ORDER BY fecha_venta DESC";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Venta v = new Venta
                            {
                                Id = Convert.ToInt32(reader["id"]),
                                Folio = Convert.ToInt32(reader["folio"]),
                                Fecha = Convert.ToDateTime(reader["fecha_venta"]),
                                Total = Convert.ToDecimal(reader["total"]),
                                Estado = reader["estado"] == DBNull.Value ? "Completada" : reader["estado"].ToString() ?? "Completada",
                                MetodoPago = reader["metodo_pago"] == DBNull.Value ? "Mostrador" : reader["metodo_pago"].ToString() ?? "Mostrador"
                            };

                            listaVentas.Add(v);
                        }
                    }
                }

                dgHistorial.ItemsSource = listaVentas;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar el historial: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgHistorial_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? dep = e.OriginalSource as DependencyObject;

            while (dep != null && !(dep is DataGridRow))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep is DataGridRow row)
            {
                row.IsSelected = true;
                dgHistorial.SelectedItem = row.Item;
            }
        }

        private void MenuVerDetalles_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgHistorial.SelectedItem is Venta ventaSeleccionada))
            {
                MessageBox.Show("Por favor, haz clic derecho sobre una fila válida.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int ventaId = ventaSeleccionada.Id;
            StringBuilder detalleTexto = new StringBuilder();
            DatabaseConnection db = new DatabaseConnection();

            try
            {
                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    string query = @"
                        SELECT dv.cantidad, dv.precio_unitario, dv.subtotal, p.nombre AS producto_nombre
                        FROM detalles_venta dv
                        INNER JOIN productos p ON dv.producto_id = p.id
                        WHERE dv.venta_id = @ventaId";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@ventaId", ventaId);

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            detalleTexto.AppendLine("--- DETALLE DE LA VENTA ---");
                            detalleTexto.AppendLine("ID: " + ventaSeleccionada.Id);
                            detalleTexto.AppendLine("Folio: " + ventaSeleccionada.Folio);
                            detalleTexto.AppendLine("Fecha: " + ventaSeleccionada.Fecha.ToString("dd/MM/yyyy HH:mm"));
                            detalleTexto.AppendLine();
                            detalleTexto.AppendLine(string.Format("{0,-30} | {1,-8} | {2,-10} | {3,-10}", "Producto", "Cant.", "P. Unit", "Subtotal"));
                            detalleTexto.AppendLine(new string('-', 68));

                            bool tieneArticulos = false;

                            while (reader.Read())
                            {
                                tieneArticulos = true;

                                string nombreProd = reader["producto_nombre"].ToString() ?? string.Empty;
                                int cantidad = Convert.ToInt32(reader["cantidad"]);
                                decimal precioUnit = Convert.ToDecimal(reader["precio_unitario"]);
                                decimal subtotal = Convert.ToDecimal(reader["subtotal"]);

                                if (nombreProd.Length > 28)
                                {
                                    nombreProd = nombreProd.Substring(0, 25) + "...";
                                }

                                detalleTexto.AppendLine(string.Format("{0,-30} | {1,-8} | {2,-10:C} | {3,-10:C}", nombreProd, cantidad, precioUnit, subtotal));
                            }

                            if (!tieneArticulos)
                            {
                                detalleTexto.AppendLine("No se encontraron artículos registrados para esta venta.");
                            }
                            else
                            {
                                detalleTexto.AppendLine(new string('-', 68));
                                detalleTexto.AppendLine("TOTAL DE LA VENTA: " + ventaSeleccionada.Total.ToString("C"));
                            }
                        }
                    }
                }

                MessageBox.Show(detalleTexto.ToString(), "Artículos Vendidos", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al recuperar los detalles de los productos: " + ex.Message, "Error de Lectura", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
