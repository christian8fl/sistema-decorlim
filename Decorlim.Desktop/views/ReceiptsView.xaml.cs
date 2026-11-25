using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Decorlim.Desktop.Models;
using Decorlim.Desktop.Services;
using Decorlim.Desktop.ViewModels;

namespace Decorlim.Desktop.Views
{
    public partial class ReceiptsView : UserControl
    {
        private readonly MainViewModel _vm;
        private List<VentaHistorialItem> _historial = new();

        public ReceiptsView(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            Loaded += async (_, _) => await CargarHistorialAsync();
        }

        public async Task CargarHistorialAsync()
        {
            _historial = await _vm.Api.ObtenerHistorialVentasAsync();
            _vm.RefrescarEstadoOffline();
            GridComprobantes.ItemsSource = _historial;
        }

        private async void BtnActualizar_Click(object sender, RoutedEventArgs e) => await CargarHistorialAsync();

        private void BtnReimprimir_Click(object sender, RoutedEventArgs e)
        {
            if (GridComprobantes.SelectedItem is not VentaHistorialItem item)
            {
                MessageBox.Show("Seleccione un comprobante de la lista para reimprimir.", "Comprobantes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool esNota = string.Equals(item.EstadoSri, "NotaVenta", System.StringComparison.OrdinalIgnoreCase);
            var doc = new RegistrarVentaResponse
            {
                IdVenta = item.IdVenta,
                TipoDocumento = esNota ? "NotaVenta" : "Factura",
                Secuencial = item.Secuencial,
                ClienteNombre = item.Cliente,
                ClienteIdentificacion = item.Identificacion,
                ClienteDireccion = item.DireccionCliente,
                ClienteTelefono = item.TelefonoCliente,
                ClienteCelular = item.CelularCliente,
                ClienteCorreo = item.CorreoCliente,
                MetodoPago = item.MetodoPago,
                ReferenciaPago = item.ReferenciaPago,
                Subtotal = item.Subtotal,
                Iva = item.Iva,
                Total = item.Total,
                FechaHora = item.FechaHora
            };

            PrintService.ImprimirComprobante(doc, mostrarDialogo: true);
        }
    }
}