using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Decorlim.Desktop.Models;
using Decorlim.Desktop.Services;
using Decorlim.Desktop.ViewModels;

namespace Decorlim.Desktop.Views
{
    public partial class SalesView : UserControl
    {
        private readonly MainViewModel _vm;
        private List<ProductoItem> _todosProductos = new();
        private List<ClienteItem> _todosClientes = new();
        private readonly ObservableCollection<CarritoItem> _carrito = new();
        private decimal _totalActual;

        public SalesView(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            GridCarrito.ItemsSource = _carrito;
            _carrito.CollectionChanged += Carrito_CollectionChanged;
            PreviewKeyDown += SalesView_PreviewKeyDown;
            Loaded += async (_, _) =>
            {
                await CargarCatalogosAsync();
                EnfocarBuscador();
            };
        }

        public void EnfocarBuscador()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TxtBuscarProducto.Focus();
                TxtBuscarProducto.SelectAll();
            }));
        }

        public bool ManejarAtajoGlobal(Key key)
        {
            switch (key)
            {
                case Key.F4:
                    CmbTarifa.SelectedIndex = CmbTarifa.SelectedIndex == 0 ? 1 : 0;
                    return true;
                case Key.F6:
                    CmbClientes.Focus();
                    CmbClientes.IsDropDownOpen = true;
                    return true;
                case Key.F9:
                    if (BtnCobrar.IsEnabled)
                    {
                        BtnCobrar_Click(BtnCobrar, new RoutedEventArgs());
                    }
                    return true;
                case Key.Escape:
                    CancelarVentaConConfirmacion(pedirConfirmacion: true);
                    return true;
                default:
                    return false;
            }
        }

        private void SalesView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                EnfocarBuscador();
                e.Handled = true;
            }
            else if (ManejarAtajoGlobal(e.Key))
            {
                e.Handled = true;
            }
        }

        public async Task CargarCatalogosAsync()
        {
            _todosProductos = await _vm.Api.ObtenerProductosAsync();
            _todosClientes = await _vm.Api.ObtenerClientesAsync();
            _vm.RefrescarEstadoOffline();

            GridResultados.ItemsSource = _todosProductos;
            CmbClientes.ItemsSource = _todosClientes;

            var consumidorFinal = _todosClientes.FirstOrDefault(c => c.Identificacion == "9999999999999")
                                  ?? _todosClientes.FirstOrDefault();
            if (consumidorFinal != null)
            {
                CmbClientes.SelectedItem = consumidorFinal;
            }
        }

        private void TxtBuscarProducto_KeyDown(object sender, KeyEventArgs e)
        {
            string filtro = TxtBuscarProducto.Text.Trim();

            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(filtro))
            {
                var exacto = _todosProductos.FirstOrDefault(p =>
                    string.Equals(p.CodigoBarras, filtro, StringComparison.OrdinalIgnoreCase));

                if (exacto != null)
                {
                    AgregarAlCarrito(exacto);
                    TxtBuscarProducto.Clear();
                    GridResultados.ItemsSource = _todosProductos;
                    e.Handled = true;
                    return;
                }

                var listaFiltrada = GridResultados.ItemsSource as IEnumerable<ProductoItem>;
                var primero = listaFiltrada?.FirstOrDefault();
                if (primero != null)
                {
                    AgregarAlCarrito(primero);
                    TxtBuscarProducto.Clear();
                    GridResultados.ItemsSource = _todosProductos;
                    e.Handled = true;
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(filtro))
            {
                GridResultados.ItemsSource = _todosProductos;
            }
            else
            {
                GridResultados.ItemsSource = _todosProductos
                    .Where(p => p.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                             || p.CodigoBarras.Contains(filtro, StringComparison.OrdinalIgnoreCase)
                             || p.Presentacion.Contains(filtro, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        private void BtnAgregarSeleccionado_Click(object sender, RoutedEventArgs e)
        {
            if (GridResultados.SelectedItem is ProductoItem prod)
            {
                AgregarAlCarrito(prod);
                TxtBuscarProducto.Focus();
            }
            else
            {
                MessageBox.Show("Seleccione un producto de la lista superior.", "DECORLIM POS", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void GridResultados_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridResultados.SelectedItem is ProductoItem prod)
            {
                AgregarAlCarrito(prod);
                TxtBuscarProducto.Focus();
            }
        }

        private void AgregarAlCarrito(ProductoItem prod)
        {
            if (prod.StockActual <= 0)
            {
                MessageBox.Show($"El producto '{prod.Nombre}' no tiene stock disponible.", "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool esMayorista = GetTarifaActual() == "Mayorista";
            decimal precio = esMayorista && prod.PrecioMayorista > 0 ? prod.PrecioMayorista : prod.PrecioMinorista;

            var existente = _carrito.FirstOrDefault(i => i.IdProducto == prod.IdProducto);
            if (existente != null)
            {
                if (existente.Cantidad + 1 > prod.StockActual)
                {
                    MessageBox.Show($"Solo hay {prod.StockActual} unidad(es) disponibles de '{prod.Nombre}'.", "Stock máximo alcanzado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                existente.Cantidad += 1;
            }
            else
            {
                var nuevo = new CarritoItem
                {
                    IdProducto = prod.IdProducto,
                    CodigoBarras = prod.CodigoBarras,
                    Nombre = prod.Nombre,
                    Presentacion = prod.Presentacion,
                    StockDisponible = prod.StockActual,
                    PrecioMinorista = prod.PrecioMinorista,
                    PrecioMayorista = prod.PrecioMayorista,
                    PrecioUnitario = precio,
                    Cantidad = 1,
                    Descuento = 0m
                };
                nuevo.PropertyChanged += CarritoItem_PropertyChanged;
                _carrito.Add(nuevo);
            }

            RecalcularTotales();
        }

        private void BtnQuitarItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CarritoItem item)
            {
                item.PropertyChanged -= CarritoItem_PropertyChanged;
                _carrito.Remove(item);
                RecalcularTotales();
            }
        }

        private void Carrito_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RecalcularTotales();

        private void CarritoItem_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RecalcularTotales();

        private void CmbTarifa_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            bool esMayorista = GetTarifaActual() == "Mayorista";
            foreach (var item in _carrito)
            {
                item.PrecioUnitario = esMayorista && item.PrecioMayorista > 0 ? item.PrecioMayorista : item.PrecioMinorista;
            }
            RecalcularTotales();
        }

        private void CmbTipoDocumento_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            RecalcularTotales();
        }

        private void CmbMetodoPago_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            string metodo = GetMetodoPagoActual();
            bool esEfectivo = metodo == "Efectivo";
            PanelEfectivo.Visibility = esEfectivo ? Visibility.Visible : Visibility.Collapsed;
            PanelReferencia.Visibility = esEfectivo ? Visibility.Collapsed : Visibility.Visible;
            if (esEfectivo)
            {
                CalcularVuelto();
            }
        }

        private void TxtMontoRecibido_TextChanged(object sender, TextChangedEventArgs e) => CalcularVuelto();

        private void CalcularVuelto()
        {
            if (TxtCambio == null || TxtMontoRecibido == null) return;

            if (decimal.TryParse(TxtMontoRecibido.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal recibido)
                && recibido > _totalActual)
            {
                TxtCambio.Text = $"${(recibido - _totalActual):F2}";
            }
            else
            {
                TxtCambio.Text = "$0.00";
            }
        }

        private void CmbClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbClientes.SelectedItem is ClienteItem cliente)
            {
                TxtClienteInfo.Text = $"{cliente.TipoIdentificacion}: {cliente.Identificacion}  |  Dir: {(string.IsNullOrWhiteSpace(cliente.Direccion) ? "S/N" : cliente.Direccion)}";
                TxtClienteContacto.Text = $"Tel/Cel: {cliente.Telefono} {cliente.Celular}  |  Correo: {(string.IsNullOrWhiteSpace(cliente.Correo) ? "No registrado" : cliente.Correo)}";
            }
        }

        private string GetTarifaActual() => (CmbTarifa.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Minorista";

        private string GetTipoDocumentoActual() => (CmbTipoDocumento.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Factura";

        private string GetMetodoPagoActual() => (CmbMetodoPago.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Efectivo";

        private void RecalcularTotales()
        {
            if (TxtSubtotal == null || TxtIva == null || TxtTotal == null || TxtResumenItems == null) return;

            decimal subtotal = Math.Round(_carrito.Sum(i => i.Subtotal), 2);
            bool esFactura = GetTipoDocumentoActual() == "Factura";
            decimal iva = esFactura ? Math.Round(subtotal * 0.15m, 2) : 0.00m;
            _totalActual = Math.Round(subtotal + iva, 2);

            TxtSubtotal.Text = $"${subtotal:F2}";
            TxtIva.Text = $"${iva:F2}";
            TxtTotal.Text = $"${_totalActual:F2}";
            TxtResumenItems.Text = $"Ítems en carrito: {_carrito.Count}  |  Unidades: {_carrito.Sum(i => i.Cantidad)}";
            CalcularVuelto();
        }

        private async void BtnCobrar_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Count == 0)
            {
                MessageBox.Show("Agregue al menos un producto al carrito antes de cobrar.", "DECORLIM POS", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtBuscarProducto.Focus();
                return;
            }

            var cliente = CmbClientes.SelectedItem as ClienteItem ?? new ClienteItem
            {
                Identificacion = "9999999999999",
                RazonSocial = "CONSUMIDOR FINAL"
            };

            string metodoPago = GetMetodoPagoActual();
            string referencia = TxtReferenciaPago.Text.Trim();
            if (metodoPago == "Transferencia" && string.IsNullOrWhiteSpace(referencia))
            {
                MessageBox.Show("Ingrese el número de referencia de la transferencia.", "Pago por transferencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtReferenciaPago.Focus();
                return;
            }

            var request = new RegistrarVentaRequest
            {
                UsuarioLogin = string.IsNullOrWhiteSpace(_vm.UsuarioActual.Login) ? "admin" : _vm.UsuarioActual.Login,
                IdentificacionCliente = cliente.Identificacion,
                Tarifa = GetTarifaActual(),
                TipoDocumento = GetTipoDocumentoActual(),
                MetodoPago = metodoPago,
                ReferenciaPago = metodoPago == "Transferencia" ? referencia : null,
                Items = _carrito.Select(i => new ItemVentaRequest
                {
                    IdProducto = i.IdProducto,
                    Cantidad = i.Cantidad,
                    PrecioUnitario = i.PrecioUnitario,
                    Descuento = i.Descuento
                }).ToList()
            };

            BtnCobrar.IsEnabled = false;
            var resultado = await _vm.Api.RegistrarVentaAsync(request, cliente, _carrito.ToList());
            BtnCobrar.IsEnabled = true;
            _vm.RefrescarEstadoOffline();

            if (!resultado.Ok || resultado.Comprobante == null)
            {
                MessageBox.Show(resultado.Mensaje, "Error al registrar venta", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ChkImprimirAutomatico.IsChecked == true)
            {
                PrintService.ImprimirComprobante(resultado.Comprobante, mostrarDialogo: true);
            }

            string aviso = resultado.GuardadoOffline
                ? $"Venta guardada en COLA OFFLINE ({resultado.Comprobante.Secuencial}). Se sincronizará automáticamente al reconectar."
                : $"{(resultado.Comprobante.TipoDocumento == "Factura" ? "Factura SRI" : "Nota de Venta")} {resultado.Comprobante.Secuencial} emitida por ${resultado.Comprobante.Total:F2}.";

            MessageBox.Show(aviso, "Venta completada", MessageBoxButton.OK, MessageBoxImage.Information);
            _vm.EstadoBarra = aviso;

            CancelarVentaConConfirmacion(pedirConfirmacion: false);
            await CargarCatalogosAsync();
            EnfocarBuscador();
        }

        private void BtnCancelarVenta_Click(object sender, RoutedEventArgs e)
            => CancelarVentaConConfirmacion(pedirConfirmacion: true);

        private void CancelarVentaConConfirmacion(bool pedirConfirmacion)
        {
            if (pedirConfirmacion && _carrito.Count > 0)
            {
                var r = MessageBox.Show(
                    "¿Desea cancelar la venta actual y vaciar el carrito?",
                    "Cancelar Venta",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

            foreach (var item in _carrito)
                item.PropertyChanged -= CarritoItem_PropertyChanged;
            _carrito.Clear();
            TxtReferenciaPago.Clear();
            TxtMontoRecibido.Clear();
            CmbMetodoPago.SelectedIndex = 0;

            var consumidorFinal = _todosClientes.FirstOrDefault(c => c.Identificacion == "9999999999999");
            if (consumidorFinal != null)
            {
                CmbClientes.SelectedItem = consumidorFinal;
            }

            RecalcularTotales();
            EnfocarBuscador();
        }
    }
}