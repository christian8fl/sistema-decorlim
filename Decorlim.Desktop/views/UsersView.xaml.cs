using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Decorlim.Desktop.Models;
using Decorlim.Desktop.ViewModels;

namespace Decorlim.Desktop.Views
{
    public partial class UsersView : UserControl
    {
        private readonly MainViewModel _vm;
        private List<UsuarioItem> _usuarios = new();
        private UsuarioItem? _seleccionado;
        private bool _esNuevo = true;

        public UsersView(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            Loaded += async (_, _) => await CargarUsuariosAsync();
        }

        public async Task CargarUsuariosAsync()
        {
            try
            {
                _usuarios = await _vm.Api.ObtenerUsuariosAsync();
                _vm.RefrescarEstadoOffline();
                GridUsuarios.ItemsSource = null;
                GridUsuarios.ItemsSource = _usuarios;
            }
            catch
            {
                _vm.RefrescarEstadoOffline();
                _vm.EstadoBarra = "Usuarios: sin conexión con el servidor (Modo Offline).";
            }
        }

        private void GridUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridUsuarios.SelectedItem is UsuarioItem u)
            {
                _seleccionado = u;
                _esNuevo = false;
                TxtTituloUsuario.Text = $"Editar Usuario ({u.Login})";
                TxtLogin.Text = u.Login;
                TxtLogin.IsEnabled = false;
                TxtNombreCompleto.Text = u.NombreCompleto;
                TxtPassword.Password = string.Empty;
                TxtEtiquetaPassword.Text = "Nueva Contraseña (opcional al editar)";
                ChkUsuarioActivo.IsChecked = u.Activo;

                foreach (object obj in CmbRol.Items)
                {
                    if (obj is ComboBoxItem item &&
                        string.Equals(item.Content?.ToString(), u.Rol, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbRol.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void LimpiarFormularioNuevo()
        {
            _seleccionado = null;
            _esNuevo = true;
            GridUsuarios.SelectedItem = null;
            TxtTituloUsuario.Text = "Nuevo Usuario";
            TxtLogin.Clear();
            TxtLogin.IsEnabled = true;
            TxtNombreCompleto.Clear();
            TxtPassword.Password = string.Empty;
            TxtEtiquetaPassword.Text = "Contraseña *";
            if (CmbRol.Items.Count > 1)
                CmbRol.SelectedIndex = 1;
            else if (CmbRol.Items.Count > 0)
                CmbRol.SelectedIndex = 0;
            ChkUsuarioActivo.IsChecked = true;
        }

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            LimpiarFormularioNuevo();
            TxtLogin.Focus();
        }

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string rol = (CmbRol.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim() ?? "Vendedor";
            string nombre = TxtNombreCompleto.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("Ingrese el nombre completo.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnGuardarUsuario.IsEnabled = false;

            try
            {
                if (_esNuevo)
                {
                    string login = TxtLogin.Text.Trim();
                    string pass = TxtPassword.Password;

                    if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
                    {
                        MessageBox.Show("Ingrese usuario y contraseña para el nuevo usuario.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var res = await _vm.Api.CrearUsuarioAsync(new CrearUsuarioRequest
                    {
                        Login = login,
                        NombreCompleto = nombre,
                        Rol = rol,
                        Password = pass
                    });

                    MessageBox.Show(
                        res.Mensaje,
                        "Usuarios",
                        MessageBoxButton.OK,
                        res.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);

                    if (res.Ok)
                    {
                        await CargarUsuariosAsync();
                        LimpiarFormularioNuevo();
                    }
                }
                else if (_seleccionado != null)
                {
                    string? nuevaPass = string.IsNullOrWhiteSpace(TxtPassword.Password) ? null : TxtPassword.Password;
                    var res = await _vm.Api.ActualizarUsuarioAsync(_seleccionado.IdUsuario, new ActualizarUsuarioRequest
                    {
                        NombreCompleto = nombre,
                        Rol = rol,
                        Activo = ChkUsuarioActivo.IsChecked == true,
                        NuevaPassword = nuevaPass
                    });

                    MessageBox.Show(
                        res.Mensaje,
                        "Usuarios",
                        MessageBoxButton.OK,
                        res.Ok ? MessageBoxImage.Information : MessageBoxImage.Warning);

                    if (res.Ok)
                    {
                        await CargarUsuariosAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error al guardar el usuario: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnGuardarUsuario.IsEnabled = true;
            }
        }
    }
}