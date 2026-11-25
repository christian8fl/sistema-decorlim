using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Decorlim.Desktop.Models;
using Decorlim.Desktop.ViewModels;
using Microsoft.Win32;

namespace Decorlim.Desktop.Views
{
    public partial class InventoryView : UserControl
    {
        private readonly MainViewModel _vm;
        private List<ProductoItem> _productos = new();
        private ProductoItem? _productoSeleccionado;
        private bool _modoNuevo = true;

        public InventoryView(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;

            ConfigurarPermisos();
            PreviewKeyDown += InventoryView_PreviewKeyDown;
            Loaded += async (_, _) => await CargarInventarioAsync();
        }

        private void InventoryView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                TxtBuscar.Focus();
                TxtBuscar.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (_vm.EsAdministrador && BtnGuardar.IsEnabled)
                {
                    BtnGuardar_Click(BtnGuardar, new RoutedEventArgs());
                }
                e.Handled = true;
            }
        }

        private void ConfigurarPermisos()
        {
            bool esAdmin = _vm.EsAdministrador;
            PanelEdicion.IsEnabled = esAdmin;
            BtnImportarExcel.IsEnabled = esAdmin;
            TxtAvisoSoloLectura.Visibility = esAdmin ? Visibility.Collapsed : Visibility.Visible;
        }

        public async Task CargarInventarioAsync()
        {
            bool incluirInactivos = ChkIncluirInactivos?.IsChecked == true;
            _productos = await _vm.Api.ObtenerProductosAsync(incluirInactivos);
            _vm.RefrescarEstadoOffline();
            AplicarFiltro();
        }

        private void AplicarFiltro()
        {
            if (GridInventario == null) return;
            string q = TxtBuscar?.Text.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(q))
            {
                GridInventario.ItemsSource = _productos;
            }
            else
            {
                GridInventario.ItemsSource = _productos
                    .Where(p => p.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)
                             || p.CodigoBarras.Contains(q, StringComparison.OrdinalIgnoreCase)
                             || p.Presentacion.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();

        private async void Filtro_Changed(object sender, RoutedEventArgs e) => await CargarInventarioAsync();

        private async void BtnRecargar_Click(object sender, RoutedEventArgs e) => await CargarInventarioAsync();

        private async void BtnImportarExcel_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.EsAdministrador)
            {
                MessageBox.Show("Solo el Administrador puede importar inventario.", "Permiso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title = "Seleccionar archivo CSV / Excel de inventario",
                Filter = "Archivos CSV (*.csv)|*.csv|Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            BtnImportarExcel.IsEnabled = false;
            var res = await _vm.Api.ImportarInventarioDesdeArchivoAsync(dlg.FileName);
            BtnImportarExcel.IsEnabled = true;
            _vm.RefrescarEstadoOffline();

            MessageBox.Show(
                res.Mensaje,
                "Importar Inventario",
                MessageBoxButton.OK,
                res.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);

            if (res.Ok)
            {
                _vm.EstadoBarra = res.Mensaje;
                await CargarInventarioAsync();
            }
        }

        private void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            var lista = GridInventario.ItemsSource as IEnumerable<ProductoItem> ?? _productos;
            var dlg = new SaveFileDialog
            {
                Title = "Exportar inventario para Excel",
                FileName = $"Inventario_DECORLIM_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Filter = "Archivo CSV compatible con Excel (*.csv)|*.csv"
            };

            if (dlg.ShowDialog() != true) return;

            _vm.Api.ExportarInventarioAExcelCsv(dlg.FileName, lista);
            _vm.EstadoBarra = $"Inventario exportado a {dlg.FileName}";
            MessageBox.Show("Inventario exportado correctamente para abrir en Excel.", "Exportar Inventario", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GridInventario_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridInventario.SelectedItem is ProductoItem prod)
            {
                _productoSeleccionado = prod;
                _modoNuevo = false;
                TxtTituloFormulario.Text = $"Editar Producto #{prod.IdProducto}";

                TxtCodigo.Text = prod.CodigoBarras;
                TxtNombre.Text = prod.Nombre;
                TxtPresentacion.Text = prod.Presentacion;
                TxtPrecioMinorista.Text = prod.PrecioMinorista.ToString("0.00", CultureInfo.InvariantCulture);
                TxtPrecioMayorista.Text = prod.PrecioMayorista.ToString("0.00", CultureInfo.InvariantCulture);
                TxtStock.Text = prod.StockActual.ToString();
                ChkActivo.IsChecked = prod.Activo;
            }
        }

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            _productoSeleccionado = null;
            _modoNuevo = true;
            GridInventario.SelectedItem = null;
            TxtTituloFormulario.Text = "Nuevo Producto";

            TxtCodigo.Clear();
            TxtNombre.Clear();
            TxtPresentacion.Text = "Galón";
            TxtPrecioMinorista.Text = "0.00";
            TxtPrecioMayorista.Text = "0.00";
            TxtStock.Text = "0";
            ChkActivo.IsChecked = true;
            TxtNombre.Focus();
        }

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.EsAdministrador)
            {
                MessageBox.Show("Solo el Administrador puede modificar el inventario.", "Permiso denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MessageBox.Show("Ingrese el nombre del producto.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(TxtPrecioMinorista.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pvp)
                || !decimal.TryParse(TxtPrecioMayorista.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pMay)
                || !int.TryParse(TxtStock.Text.Trim(), out int stock))
            {
                MessageBox.Show("Verifique que precios y stock sean valores numéricos válidos.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var item = new ProductoItem
            {
                IdProducto = _modoNuevo || _productoSeleccionado == null ? 0 : _productoSeleccionado.IdProducto,
                CodigoBarras = TxtCodigo.Text.Trim(),
                Nombre = TxtNombre.Text.Trim(),
                Presentacion = string.IsNullOrWhiteSpace(TxtPresentacion.Text) ? "Unidad" : TxtPresentacion.Text.Trim(),
                PrecioMinorista = pvp,
                PrecioMayorista = pMay,
                StockActual = stock,
                Activo = ChkActivo.IsChecked == true
            };

            BtnGuardar.IsEnabled = false;
            var res = await _vm.Api.GuardarProductoAsync(item, _modoNuevo);
            BtnGuardar.IsEnabled = true;
            _vm.RefrescarEstadoOffline();

            if (!res.Ok)
            {
                MessageBox.Show(res.Mensaje, "Error al guardar", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(res.Mensaje, "Inventario", MessageBoxButton.OK, MessageBoxImage.Information);
            _vm.EstadoBarra = res.Mensaje;
            await CargarInventarioAsync();
        }
    }
}