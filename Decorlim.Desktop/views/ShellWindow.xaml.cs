using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Decorlim.Desktop.ViewModels;

namespace Decorlim.Desktop.Views
{
    public partial class ShellWindow : Window
    {
        private readonly MainViewModel _vm;
        private DashboardView? _dashboardView;
        private SalesView? _salesView;
        private InventoryView? _inventoryView;
        private LabelsView? _labelsView;
        private ClientsView? _clientsView;
        private ReceiptsView? _receiptsView;
        private UsersView? _usersView;

        public ShellWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;

            PreviewKeyDown += ShellWindow_PreviewKeyDown;
            AplicarPermisosPorRol();
            CargarVistaInicial();
        }

        private void AplicarPermisosPorRol()
        {
            // Solo Administrador gestiona usuarios y roles
            TabUsuarios.Visibility = _vm.EsAdministrador ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CargarVistaInicial()
        {
            // Solo instanciamos el Dashboard al arrancar para evitar congelamientos
            _dashboardView = new DashboardView(_vm);
            HostDashboard.Children.Add(_dashboardView);
        }

        private void AsegurarVistaCargada(TabItem? tab)
        {
            if (tab == null) return;

            if (tab == TabDashboard && _dashboardView == null)
            {
                _dashboardView = new DashboardView(_vm);
                HostDashboard.Children.Add(_dashboardView);
            }
            else if (tab == TabVentas && _salesView == null)
            {
                _salesView = new SalesView(_vm);
                HostVentas.Children.Add(_salesView);
            }
            else if (tab == TabInventario && _inventoryView == null)
            {
                _inventoryView = new InventoryView(_vm);
                HostInventario.Children.Add(_inventoryView);
            }
            else if (tab == TabEtiquetas && _labelsView == null)
            {
                _labelsView = new LabelsView(_vm);
                HostEtiquetas.Children.Add(_labelsView);
            }
            else if (tab == TabClientes && _clientsView == null)
            {
                _clientsView = new ClientsView(_vm);
                HostClientes.Children.Add(_clientsView);
            }
            else if (tab == TabComprobantes && _receiptsView == null)
            {
                _receiptsView = new ReceiptsView(_vm);
                HostComprobantes.Children.Add(_receiptsView);
            }
            else if (tab == TabUsuarios && _vm.EsAdministrador && _usersView == null)
            {
                _usersView = new UsersView(_vm);
                HostUsuarios.Children.Add(_usersView);
            }
        }

        private async Task RefrescarVistaActualAsync()
        {
            var seleccionada = MainTabs.SelectedItem as TabItem ?? TabDashboard;
            AsegurarVistaCargada(seleccionada);

            if (seleccionada == TabDashboard && _dashboardView != null)
                await _dashboardView.CargarEstadisticasAsync();
            else if (seleccionada == TabVentas && _salesView != null)
                await _salesView.CargarCatalogosAsync();
            else if (seleccionada == TabInventario && _inventoryView != null)
                await _inventoryView.CargarInventarioAsync();
            else if (seleccionada == TabEtiquetas && _labelsView != null)
                await _labelsView.CargarProductosAsync();
            else if (seleccionada == TabClientes && _clientsView != null)
                await _clientsView.CargarClientesAsync();
            else if (seleccionada == TabComprobantes && _receiptsView != null)
                await _receiptsView.CargarHistorialAsync();
            else if (seleccionada == TabUsuarios && _usersView != null)
                await _usersView.CargarUsuariosAsync();

            _vm.RefrescarEstadoOffline();
        }

        private async void ShellWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.F5)
                {
                    _vm.EstadoBarra = "Actualizando datos desde el servidor...";
                    await RefrescarVistaActualAsync();
                    _vm.EstadoBarra = _vm.Api.ApiEnLinea
                        ? "Datos actualizados con la base de datos."
                        : "Actualizado desde caché local (Modo Offline).";
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.F2)
                {
                    MainTabs.SelectedItem = TabVentas;
                    AsegurarVistaCargada(TabVentas);
                    _salesView?.EnfocarBuscador();
                    e.Handled = true;
                    return;
                }

                if (MainTabs.SelectedItem == TabVentas && _salesView != null)
                {
                    if (_salesView.ManejarAtajoGlobal(e.Key))
                    {
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _vm.EstadoBarra = $"Error en atajo de teclado: {ex.Message}";
            }
        }

        private async void BtnSincronizar_Click(object sender, RoutedEventArgs e)
        {
            var btnSincronizar = sender as Button;
            if (btnSincronizar != null) btnSincronizar.IsEnabled = false;
            _vm.EstadoBarra = "Sincronizando con la base de datos...";

            try
            {
                int synced = await _vm.Api.SincronizarVentasPendientesAsync();

                // Traer datos actualizados desde la API para refrescar caché local y vistas
                _ = await _vm.Api.ObtenerProductosAsync(true);
                _ = await _vm.Api.ObtenerClientesAsync();
                _ = await _vm.Api.ObtenerHistorialVentasAsync();
                _ = await _vm.Api.ObtenerDashboardResumenAsync();
                if (_vm.EsAdministrador)
                {
                    try { _ = await _vm.Api.ObtenerUsuariosAsync(); } catch { }
                }

                await RefrescarVistaActualAsync();

                if (!_vm.Api.ApiEnLinea)
                {
                    _vm.EstadoBarra = synced > 0
                        ? $"Se sincronizaron {synced} venta(s), pero la API volvió a Modo Offline."
                        : "Sin conexión con la API (Modo Offline). Revisa que Decorlim.API esté activa en el puerto 5041.";
                    return;
                }

                int pendientesRestantes = _vm.Api.ContarVentasPendientes();
                _vm.EstadoBarra = synced > 0
                    ? $"Sincronizadas {synced} venta(s) con BD. Pendientes restantes: {pendientesRestantes}."
                    : $"Sincronizado con BD. Pendientes por sincronizar: {pendientesRestantes}.";
            }
            catch (Exception ex)
            {
                _vm.EstadoBarra = $"Error al sincronizar: {ex.Message}";
            }
            finally
            {
                if (btnSincronizar != null) btnSincronizar.IsEnabled = true;
            }
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            var login = new MainWindow();
            login.Show();
            Close();
        }

        private async void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (e.Source is not TabControl) return;

            try
            {
                _vm.RefrescarEstadoOffline();

                var seleccionada = MainTabs.SelectedItem as TabItem;
                AsegurarVistaCargada(seleccionada);

                if (seleccionada == TabDashboard && _dashboardView != null)
                    await _dashboardView.CargarEstadisticasAsync();
                else if (seleccionada == TabVentas && _salesView != null)
                    await _salesView.CargarCatalogosAsync();
                else if (seleccionada == TabInventario && _inventoryView != null)
                    await _inventoryView.CargarInventarioAsync();
                else if (seleccionada == TabEtiquetas && _labelsView != null)
                    await _labelsView.CargarProductosAsync();
                else if (seleccionada == TabClientes && _clientsView != null)
                    await _clientsView.CargarClientesAsync();
                else if (seleccionada == TabComprobantes && _receiptsView != null)
                    await _receiptsView.CargarHistorialAsync();
                else if (seleccionada == TabUsuarios && _usersView != null)
                    await _usersView.CargarUsuariosAsync();
            }
            catch (Exception ex)
            {
                _vm.EstadoBarra = $"Error al cambiar de pestaña: {ex.Message}";
            }
        }
    }
}