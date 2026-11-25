using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Decorlim.Desktop.Models;
using Decorlim.Desktop.ViewModels;

namespace Decorlim.Desktop.Views
{
    public partial class ClientsView : UserControl
    {
        private readonly MainViewModel _vm;
        private List<ClienteItem> _clientes = new();
        private ClienteItem? _seleccionado;
        private bool _esNuevo = true;

        public ClientsView(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            Loaded += async (_, _) => await CargarClientesAsync();
        }

        public async Task CargarClientesAsync()
        {
            _clientes = await _vm.Api.ObtenerClientesAsync();
            _vm.RefrescarEstadoOffline();
            AplicarFiltro();
        }

        private void AplicarFiltro()
        {
            if (GridClientes == null) return;
            string q = TxtBuscarCliente?.Text.Trim() ?? string.Empty;

            GridClientes.ItemsSource = string.IsNullOrWhiteSpace(q)
                ? _clientes
                : _clientes.Where(c => c.RazonSocial.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || c.Identificacion.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || (c.Correo?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }

        private void TxtBuscarCliente_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();

        private async void BtnRecargar_Click(object sender, RoutedEventArgs e) => await CargarClientesAsync();

        private void GridClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridClientes.SelectedItem is ClienteItem cl)
            {
                _seleccionado = cl;
                _esNuevo = false;
                TxtTituloCliente.Text = $"Editar Cliente #{cl.IdCliente}";

                foreach (ComboBoxItem item in CmbTipoIdent.Items)
                {
                    if (string.Equals(item.Content?.ToString(), cl.TipoIdentificacion, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbTipoIdent.SelectedItem = item;
                        break;
                    }
                }

                TxtIdentificacion.Text = cl.Identificacion;
                TxtRazonSocial.Text = cl.RazonSocial;
                TxtCorreo.Text = cl.Correo ?? string.Empty;
                TxtDireccion.Text = cl.Direccion ?? string.Empty;
                TxtTelefono.Text = cl.Telefono ?? string.Empty;
                TxtCelular.Text = cl.Celular ?? string.Empty;
            }
        }

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            _seleccionado = null;
            _esNuevo = true;
            GridClientes.SelectedItem = null;
            TxtTituloCliente.Text = "Nuevo Cliente";
            CmbTipoIdent.SelectedIndex = 0;
            TxtIdentificacion.Clear();
            TxtRazonSocial.Clear();
            TxtCorreo.Clear();
            TxtDireccion.Clear();
            TxtTelefono.Clear();
            TxtCelular.Clear();
            TxtIdentificacion.Focus();
        }

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string ident = TxtIdentificacion.Text.Trim();
            string razon = TxtRazonSocial.Text.Trim();

            if (string.IsNullOrWhiteSpace(ident) || string.IsNullOrWhiteSpace(razon))
            {
                MessageBox.Show("La identificación y la razón social / nombre son obligatorios.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipoIdent = (CmbTipoIdent.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CEDULA";

            var cliente = new ClienteItem
            {
                IdCliente = _esNuevo || _seleccionado == null ? 0 : _seleccionado.IdCliente,
                TipoIdentificacion = tipoIdent,
                Identificacion = ident,
                RazonSocial = razon,
                Correo = TxtCorreo.Text.Trim(),
                Direccion = TxtDireccion.Text.Trim(),
                Telefono = TxtTelefono.Text.Trim(),
                Celular = TxtCelular.Text.Trim(),
                TipoCliente = "General",
                Activo = true
            };

            BtnGuardarCliente.IsEnabled = false;
            var res = await _vm.Api.GuardarClienteAsync(cliente, _esNuevo);
            BtnGuardarCliente.IsEnabled = true;
            _vm.RefrescarEstadoOffline();

            if (!res.Ok)
            {
                MessageBox.Show(res.Mensaje, "Error al guardar cliente", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(res.Mensaje, "Clientes", MessageBoxButton.OK, MessageBoxImage.Information);
            _vm.EstadoBarra = res.Mensaje;
            await CargarClientesAsync();
        }
    }
}