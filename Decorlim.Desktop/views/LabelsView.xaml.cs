using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Decorlim.Desktop.Models;
using Decorlim.Desktop.Services;
using Decorlim.Desktop.ViewModels;

namespace Decorlim.Desktop.Views
{
    public partial class LabelsView : UserControl
    {
        private readonly MainViewModel _vm;
        private List<ProductoItem> _productos = new();
        private readonly ObservableCollection<EtiquetaLoteItem> _lote = new();

        public LabelsView(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            GridLote.ItemsSource = _lote;
            Loaded += async (_, _) => await CargarProductosAsync();
        }

        public async Task CargarProductosAsync()
        {
            _productos = await _vm.Api.ObtenerProductosAsync();
            _vm.RefrescarEstadoOffline();
            GridProductosEtiqueta.ItemsSource = _productos;
            if (_productos.Count > 0)
            {
                GridProductosEtiqueta.SelectedIndex = 0;
            }
        }

        private void TxtBuscarProd_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = TxtBuscarProd.Text.Trim();
            GridProductosEtiqueta.ItemsSource = string.IsNullOrWhiteSpace(q)
                ? _productos
                : _productos.Where(p => p.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase)
                                     || p.CodigoBarras.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void GridProductosEtiqueta_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ActualizarVistaPrevia();
        }

        private void ChkMostrarMayorista_Changed(object sender, RoutedEventArgs e)
        {
            ActualizarVistaPrevia();
        }

        private void ActualizarVistaPrevia()
        {
            if (TxtPreviewNombre == null || GridProductosEtiqueta?.SelectedItem is not ProductoItem prod) return;

            TxtPreviewNombre.Text = prod.Nombre;
            TxtPreviewPresentacion.Text = prod.Presentacion;
            ImgBarcodePreview.Source = BarcodeService.GenerarImagenCodigoBarras(prod.CodigoBarras, 230, 64);

            bool verMay = ChkMostrarMayorista?.IsChecked == true && prod.PrecioMayorista > 0;
            TxtPreviewPrecio.Text = verMay
                ? $"PVP: ${prod.PrecioMinorista:F2}  |  May: ${prod.PrecioMayorista:F2}"
                : $"PVP: ${prod.PrecioMinorista:F2}";
        }

        private void BtnAgregarAlLote_Click(object sender, RoutedEventArgs e)
        {
            if (GridProductosEtiqueta.SelectedItem is not ProductoItem prod)
            {
                MessageBox.Show("Seleccione un producto para agregar al lote.", "Etiquetas", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!int.TryParse(TxtCopias.Text.Trim(), out int copias) || copias <= 0)
            {
                copias = 1;
            }

            var existente = _lote.FirstOrDefault(x => x.IdProducto == prod.IdProducto);
            if (existente != null)
            {
                existente.Copias += copias;
            }
            else
            {
                _lote.Add(new EtiquetaLoteItem
                {
                    IdProducto = prod.IdProducto,
                    CodigoBarras = prod.CodigoBarras,
                    Nombre = prod.Nombre,
                    Presentacion = prod.Presentacion,
                    PrecioMinorista = prod.PrecioMinorista,
                    PrecioMayorista = prod.PrecioMayorista,
                    Copias = copias
                });
            }
        }

        private void BtnQuitarLote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is EtiquetaLoteItem item)
            {
                _lote.Remove(item);
            }
        }

        private void BtnLimpiarLote_Click(object sender, RoutedEventArgs e) => _lote.Clear();

        private void BtnImprimirLote_Click(object sender, RoutedEventArgs e)
        {
            if (_lote.Count == 0)
            {
                if (GridProductosEtiqueta.SelectedItem is ProductoItem actual)
                {
                    _lote.Add(new EtiquetaLoteItem
                    {
                        IdProducto = actual.IdProducto,
                        CodigoBarras = actual.CodigoBarras,
                        Nombre = actual.Nombre,
                        Presentacion = actual.Presentacion,
                        PrecioMinorista = actual.PrecioMinorista,
                        PrecioMayorista = actual.PrecioMayorista,
                        Copias = Math.Max(1, int.TryParse(TxtCopias.Text, out int c) ? c : 1)
                    });
                }
                else
                {
                    MessageBox.Show("Agregue etiquetas al lote antes de imprimir.", "Etiquetas", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            PrintService.ImprimirEtiquetasLote(_lote, ChkMostrarMayorista.IsChecked == true, mostrarDialogo: true);
        }
    }
}