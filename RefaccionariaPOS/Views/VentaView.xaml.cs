using Npgsql;
using RefaccionariaPOS.Data;
using RefaccionariaPOS.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RefaccionariaPOS.Views
{
    public partial class VentaView : Window
    {
        private ObservableCollection<ProductoCarrito> listaCarrito = new ObservableCollection<ProductoCarrito>();
        private decimal totalVenta = 0;
        private Producto productoEnVistaPrevia; // Reutilizamos tu variable perfectamente

        public VentaView()
        {
            InitializeComponent();
            dgCarrito.ItemsSource = listaCarrito;
        }

        // ==========================================================
        // 1. BUSCADOR EN TIEMPO REAL (Reemplaza al BuscarProductoReal)
        // ==========================================================
        private void TxtBuscarId_TextChanged(object sender, TextChangedEventArgs e)
        {
            string busqueda = txtBuscarId.Text.Trim();

            // Si hay menos de 2 letras, ocultamos la tablita flotante y la vista previa
            if (busqueda.Length < 2)
            {
                if (dgResultadosBusqueda != null) dgResultadosBusqueda.Visibility = Visibility.Collapsed;
                OcultarVistaPrevia();
                return;
            }

            try
            {
                List<Producto> resultados = new List<Producto>();
                DatabaseConnection db = new DatabaseConnection();

                using (NpgsqlConnection conexion = db.GetConnection())
                {
                    conexion.Open();
                    // Usamos ILIKE para buscar coincidencias parciales sin importar mayúsculas
                    string query = @"SELECT codigo_barras, nombre, precio_venta, stock_actual 
                                     FROM productos 
                                     WHERE nombre ILIKE @busqueda OR codigo_barras ILIKE @busqueda
                                     ORDER BY nombre ASC LIMIT 15;";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@busqueda", "%" + busqueda + "%");

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                resultados.Add(new Producto
                                {
                                    CodigoBarras = reader["codigo_barras"].ToString(),
                                    Nombre = reader["nombre"].ToString(),
                                    PrecioVenta = Convert.ToDecimal(reader["precio_venta"]),
                                    Stock = Convert.ToInt32(reader["stock_actual"])
                                });
                            }
                        }
                    }
                }

                // Si encontramos algo, mostramos la lista flotante
                if (resultados.Count > 0)
                {
                    dgResultadosBusqueda.ItemsSource = resultados;
                    dgResultadosBusqueda.Visibility = Visibility.Visible;
                    OcultarVistaPrevia(); // Escondemos el preview mientras escoge de la lista
                }
                else
                {
                    dgResultadosBusqueda.Visibility = Visibility.Collapsed;
                }
            }
            catch { /* Manejo silencioso: Ignoramos errores si el usuario teclea extremadamente rápido */ }
        }

        // ==========================================================
        // 2. AL SELECCIONAR UN PRODUCTO DE LA LISTA FLOTANTE
        // ==========================================================
        private void DgResultadosBusqueda_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgResultadosBusqueda.SelectedItem is Producto seleccionado)
            {
                // Pasamos el producto seleccionado a tu variable global
                productoEnVistaPrevia = seleccionado;

                // Llamamos a tu método que ya tenías para llenar la interfaz
                MostrarVistaPrevia(productoEnVistaPrevia);

                // Ocultamos la lista flotante porque ya eligió uno
                dgResultadosBusqueda.Visibility = Visibility.Collapsed;

                // Limpiamos la selección para que pueda volver a elegir el mismo después si quiere
                dgResultadosBusqueda.SelectedItem = null;
            }
        }
        private void TxtBuscarId_KeyDown(object sender, KeyEventArgs e) { }

        // ==========================================================
        // TUS MÉTODOS ORIGINALES (Se mantienen intactos)
        // ==========================================================
        private void MostrarVistaPrevia(Producto p)
        {
            lblPreviewCodigo.Text = p.CodigoBarras;
            lblPreviewNombre.Text = p.Nombre;
            lblPreviewPrecio.Text = string.Format("{0:C}", p.PrecioVenta);
            lblPreviewStock.Text = p.Stock.ToString();
            txtCantidadAgregar.Text = "1";


            brdPreview.Visibility = Visibility.Visible;
            btnAgregarAlCarrito.Visibility = Visibility.Visible;

            txtCantidadAgregar.Focus();
            txtCantidadAgregar.SelectAll();
        }

        private void OcultarVistaPrevia()
        {
            brdPreview.Visibility = Visibility.Collapsed;
            btnAgregarAlCarrito.Visibility = Visibility.Collapsed;
            productoEnVistaPrevia = null;
        }

        private void BtnAgregarAlCarrito_Click(object sender, RoutedEventArgs e)
        {
            if (productoEnVistaPrevia == null) return;

            if (!int.TryParse(txtCantidadAgregar.Text, out int cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Ingresa una cantidad válida a agregar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cantidad > productoEnVistaPrevia.Stock)
            {
                MessageBox.Show($"¡Error de Stock! Solo quedan {productoEnVistaPrevia.Stock} piezas.", "Sin Existencias", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var itemExistente = listaCarrito.FirstOrDefault(i => i.CodigoBarras == productoEnVistaPrevia.CodigoBarras);

            if (itemExistente != null)
            {
                if ((itemExistente.Cantidad + cantidad) > productoEnVistaPrevia.Stock)
                {
                    MessageBox.Show($"Límite excedido. Ya tienes {itemExistente.Cantidad} en el carrito.", "Límite de Stock", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                itemExistente.Cantidad += cantidad;
                dgCarrito.Items.Refresh();
            }
            else
            {
                listaCarrito.Add(new ProductoCarrito
                {
                    CodigoBarras = productoEnVistaPrevia.CodigoBarras,
                    Nombre = productoEnVistaPrevia.Nombre,
                    PrecioVenta = productoEnVistaPrevia.PrecioVenta,
                    Cantidad = cantidad
                });
            }

            ActualizarTotales();
            txtBuscarId.Clear();
            OcultarVistaPrevia();
            txtBuscarId.Focus();
        }

        private void ActualizarTotales()
        {
            totalVenta = listaCarrito.Sum(item => item.Subtotal);
            lblTotalCarrito.Text = string.Format("{0:C}", totalVenta);
        }

        // =========================================================================
        // MOTOR DE COBRO DEFINITIVO: POSTGRESQL GENERA EL FOLIO Y C# LO IMPRIME
        // =========================================================================
        private void BtnCobrar_Click(object sender, RoutedEventArgs e)
        {
            if (listaCarrito.Count == 0)
            {
                MessageBox.Show("El carrito de compras está vacío.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int ventaIdGenerado = 0;
            int folioGeneradoBaseDatos = 0;
            DatabaseConnection db = new DatabaseConnection();

            using (NpgsqlConnection conexion = db.GetConnection())
            {
                conexion.Open();

                using (NpgsqlTransaction transaccion = conexion.BeginTransaction())
                {
                    try
                    {
                        string queryVenta = @"
                    INSERT INTO ventas (total, fecha_venta, estado)
                    VALUES (@total, @fecha, @estado)
                    RETURNING id, folio;";

                        using (NpgsqlCommand cmdVenta = new NpgsqlCommand(queryVenta, conexion, transaccion))
                        {
                            cmdVenta.Parameters.AddWithValue("@total", totalVenta);
                            cmdVenta.Parameters.AddWithValue("@fecha", DateTime.Now);
                            cmdVenta.Parameters.AddWithValue("@estado", "Completada");

                            using (NpgsqlDataReader reader = cmdVenta.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    ventaIdGenerado = Convert.ToInt32(reader["id"]);
                                    folioGeneradoBaseDatos = Convert.ToInt32(reader["folio"]);
                                }
                            }
                        }

                        foreach (var item in listaCarrito)
                        {
                            int productoId = 0;

                            string queryProducto = @"
                        SELECT id, stock_actual
                        FROM productos
                        WHERE codigo_barras = @codigo;";

                            using (NpgsqlCommand cmdProducto = new NpgsqlCommand(queryProducto, conexion, transaccion))
                            {
                                cmdProducto.Parameters.AddWithValue("@codigo", item.CodigoBarras);

                                using (NpgsqlDataReader reader = cmdProducto.ExecuteReader())
                                {
                                    if (!reader.Read())
                                    {
                                        throw new Exception("No se encontró el producto con código: " + item.CodigoBarras);
                                    }

                                    productoId = Convert.ToInt32(reader["id"]);
                                    int stockActual = Convert.ToInt32(reader["stock_actual"]);

                                    if (stockActual < item.Cantidad)
                                    {
                                        throw new Exception("Stock insuficiente para el producto: " + item.Nombre);
                                    }
                                }
                            }

                            string queryDetalle = @"
                        INSERT INTO detalles_venta
                        (venta_id, producto_id, cantidad, precio_unitario, subtotal)
                        VALUES
                        (@ventaId, @productoId, @cantidad, @precioUnitario, @subtotal);";

                            using (NpgsqlCommand cmdDetalle = new NpgsqlCommand(queryDetalle, conexion, transaccion))
                            {
                                cmdDetalle.Parameters.AddWithValue("@ventaId", ventaIdGenerado);
                                cmdDetalle.Parameters.AddWithValue("@productoId", productoId);
                                cmdDetalle.Parameters.AddWithValue("@cantidad", item.Cantidad);
                                cmdDetalle.Parameters.AddWithValue("@precioUnitario", item.PrecioVenta);
                                cmdDetalle.Parameters.AddWithValue("@subtotal", item.Subtotal);

                                cmdDetalle.ExecuteNonQuery();
                            }

                            string queryStock = @"
                        UPDATE productos
                        SET stock_actual = stock_actual - @cantidad
                        WHERE codigo_barras = @codigo;";

                            using (NpgsqlCommand cmdStock = new NpgsqlCommand(queryStock, conexion, transaccion))
                            {
                                cmdStock.Parameters.AddWithValue("@cantidad", item.Cantidad);
                                cmdStock.Parameters.AddWithValue("@codigo", item.CodigoBarras);

                                cmdStock.ExecuteNonQuery();
                            }
                        }

                        transaccion.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaccion.Rollback();
                        MessageBox.Show("Error al procesar la venta en la Base de Datos: " + ex.Message, "Venta Cancelada", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }

            GenerarArchivoTicket(folioGeneradoBaseDatos);

            MessageBox.Show("¡Venta con Folio #" + folioGeneradoBaseDatos + " procesada con éxito!", "Venta Completada", MessageBoxButton.OK, MessageBoxImage.Information);

            listaCarrito.Clear();
            ActualizarTotales();
        }

        private void GenerarArchivoTicket(int folio)
        {
            try
            {
                string rutaCarpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tickets_Refaccionaria");

                if (!Directory.Exists(rutaCarpeta))
                {
                    Directory.CreateDirectory(rutaCarpeta);
                }

                string rutaArchivo = Path.Combine(rutaCarpeta, $"Ticket_{folio}.txt");

                using (StreamWriter ticket = new StreamWriter(rutaArchivo))
                {
                    ticket.WriteLine("========================================");
                    ticket.WriteLine("       🛠️ REFACCIONARIA POS v1.0 🛠️       ");
                    ticket.WriteLine("========================================");
                    ticket.WriteLine($"Folio No:  {folio}"); // Sincronizado con Postgres
                    ticket.WriteLine($"Fecha:     {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    ticket.WriteLine("----------------------------------------");
                    ticket.WriteLine(string.Format("{0,-22} {1,-4} {2,11}", "Producto", "Cant", "Subtotal"));
                    ticket.WriteLine("----------------------------------------");

                    foreach (var item in listaCarrito)
                    {
                        string nombreCorto = item.Nombre.Length > 20 ? item.Nombre.Substring(0, 20) : item.Nombre;
                        ticket.WriteLine(string.Format("{0,-22} {1,-4} {2,11:C}", nombreCorto, item.Cantidad, item.Subtotal));
                    }

                    ticket.WriteLine("----------------------------------------");
                    ticket.WriteLine(string.Format("{0,-27} {1,11:C}", "TOTAL:", totalVenta));
                    ticket.WriteLine("========================================");
                    ticket.WriteLine("    ¡Gracias por su preferencia!       ");
                    ticket.WriteLine("   Conserve este ticket para cambios    ");
                    ticket.WriteLine("========================================");
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(rutaArchivo) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo desplegar el archivo de ticket: " + ex.Message, "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    public class ProductoCarrito
    {
        public string CodigoBarras { get; set; }
        public string Nombre { get; set; }
        public decimal PrecioVenta { get; set; }
        public int Cantidad { get; set; }
        public decimal Subtotal => PrecioVenta * Cantidad;
    }
}