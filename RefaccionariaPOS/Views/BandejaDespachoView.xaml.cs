using Npgsql;
using RefaccionariaPOS.Data;
using RefaccionariaPOS.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace RefaccionariaPOS.Views
{
    public partial class BandejaDespachoView : Window
    {
        private readonly int usuarioId;

        public BandejaDespachoView(int usuarioId)
        {
            InitializeComponent();
            this.usuarioId = usuarioId;
            CargarSolicitudes();
        }

        public BandejaDespachoView() : this(0)
        {
        }

        private void ProcesarSurtido(SolicitudDespacho solicitud)
        {
            DatabaseConnection db = new DatabaseConnection();

            using (NpgsqlConnection conexion = db.GetConnection())
            {
                conexion.Open();
                using (NpgsqlTransaction transaccion = conexion.BeginTransaction())
                {
                    try
                    {
                        string queryCheck = "SELECT stock_actual, precio_venta FROM productos WHERE codigo_barras = @codigo FOR UPDATE;";
                        int stockDisponible = 0;
                        decimal precioProducto = 0;

                        using (NpgsqlCommand cmdCheck = new NpgsqlCommand(queryCheck, conexion, transaccion))
                        {
                            cmdCheck.Parameters.AddWithValue("@codigo", solicitud.CodigoBarras);

                            // Usamos el reader dentro de su propio bloque usando para forzar su cierre inmediato al terminar
                            using (NpgsqlDataReader reader = cmdCheck.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    stockDisponible = Convert.ToInt32(reader["stock_actual"]);
                                    precioProducto = reader["precio_venta"] != DBNull.Value ? Convert.ToDecimal(reader["precio_venta"]) : 0m;
                                }
                                else
                                {
                                    MessageBox.Show("No se encontró el producto en el inventario.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaccion.Rollback();
                                    return;
                                }
                            } // <-- AQUÍ SE CIERRA EL READER DE FORMA SEGURA LIBERANDO LA CONEXIÓN
                        }

                        if (stockDisponible < solicitud.Cantidad)
                        {
                            MessageBox.Show($"No hay stock suficiente para entregar.\nStock actual: {stockDisponible}", "Stock Insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                            transaccion.Rollback();
                            return;
                        }

                        // Cambiamos el estado en la orden a 'Surtido'
                        string updateOrden = @"UPDATE orden_refacciones
                                               SET estado_despacho = 'Surtido',
                                                   precio_cotizado = @precio
                                               WHERE id_detalle = @idDetalle
                                                 AND estado_despacho = 'Solicitado';";
                        using (NpgsqlCommand cmdOrden = new NpgsqlCommand(updateOrden, conexion, transaccion))
                        {
                            cmdOrden.Parameters.AddWithValue("@idDetalle", solicitud.IdDetalle);
                            cmdOrden.Parameters.AddWithValue("@precio", precioProducto);
                            if (cmdOrden.ExecuteNonQuery() == 0)
                            {
                                throw new Exception("La solicitud ya fue procesada o no existe.");
                            }
                        }

                        // Descontamos el stock físico
                        string updateStock = @"UPDATE productos
                                               SET stock_actual = stock_actual - @cantidad
                                               WHERE codigo_barras = @codigo
                                                 AND stock_actual >= @cantidad;";
                        using (NpgsqlCommand cmdStock = new NpgsqlCommand(updateStock, conexion, transaccion))
                        {
                            cmdStock.Parameters.AddWithValue("@cantidad", solicitud.Cantidad);
                            cmdStock.Parameters.AddWithValue("@codigo", solicitud.CodigoBarras);
                            if (cmdStock.ExecuteNonQuery() == 0)
                            {
                                throw new Exception("El stock cambió antes de completar el surtido.");
                            }
                        }

                        // Registramos la venta unificada en la BD
                        decimal totalVenta = precioProducto * solicitud.Cantidad;
                        string queryVenta = @"INSERT INTO ventas (usuario_id, total, fecha_venta, estado, metodo_pago)
                                              VALUES (@usuarioId, @total, CURRENT_TIMESTAMP, 'Completada', 'Surtido Taller');";
                        using (NpgsqlCommand cmdVenta = new NpgsqlCommand(queryVenta, conexion, transaccion))
                        {
                            cmdVenta.Parameters.AddWithValue("@usuarioId", usuarioId == 0 ? DBNull.Value : (object)usuarioId);
                            cmdVenta.Parameters.AddWithValue("@total", totalVenta);
                            cmdVenta.ExecuteNonQuery();
                        }

                        transaccion.Commit();
                        MessageBox.Show("Pieza surtida, descontada del inventario y registrada en ventas.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                        CargarSolicitudes();
                    }
                    catch (Exception ex)
                    {
                        transaccion.Rollback();
                        MessageBox.Show("Error al procesar el surtido: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void CargarSolicitudes()
        {
            try
            {
                List<SolicitudDespacho> listaSolicitudes = new List<SolicitudDespacho>();
                DatabaseConnection db = new DatabaseConnection();

                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();

                    // CORREGIDO: Traemos también el id_orden (o id_detalle si funge como identificador visual)
                    string query = @"SELECT o.id_detalle, o.id_orden, o.codigo_barras, o.cantidad, p.nombre
                                     FROM orden_refacciones o
                                     LEFT JOIN productos p ON o.codigo_barras = p.codigo_barras
                                     WHERE o.estado_despacho = 'Solicitado';";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                listaSolicitudes.Add(new SolicitudDespacho
                                {
                                    IdDetalle = Convert.ToInt32(reader["id_detalle"]),
                                    IdOrden = Convert.ToInt32(reader["id_orden"]),
                                    CodigoBarras = reader["codigo_barras"].ToString() ?? string.Empty,
                                    Cantidad = Convert.ToInt32(reader["cantidad"]),
                                    NombreProducto = reader["nombre"] != DBNull.Value ? reader["nombre"].ToString() ?? "Desconocido" : "Desconocido"
                                });
                            }
                        }
                    }
                }

                dgSolicitudes.ItemsSource = null;
                dgSolicitudes.ItemsSource = listaSolicitudes;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar las peticiones del taller: " + ex.Message, "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            CargarSolicitudes();
        }

        private void BtnSurtir_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button boton && boton.DataContext is SolicitudDespacho solicitudFila)
            {
                ProcesarSurtido(solicitudFila);
            }
            else if (dgSolicitudes.SelectedItem is SolicitudDespacho solicitudSeleccionada)
            {
                ProcesarSurtido(solicitudSeleccionada);
            }
            else
            {
                MessageBox.Show("Por favor, selecciona una solicitud de la tabla para surtir.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
