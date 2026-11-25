using System.Threading.Tasks;
using System.Windows;
using Decorlim.Desktop.Services;
using Decorlim.Desktop.ViewModels;

namespace Decorlim.Desktop.Views
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService = new();

        public MainWindow()
        {
            InitializeComponent();
            TxtUsuario.Focus();
        }

        private void BtnRellenarAdmin_Click(object sender, RoutedEventArgs e)
        {
            TxtUsuario.Text = "admin";
            TxtPassword.Password = "1234";
            LblError.Text = string.Empty;
        }

        private void BtnRellenarVendedor_Click(object sender, RoutedEventArgs e)
        {
            TxtUsuario.Text = "cflores_vendedor";
            TxtPassword.Password = "1234";
            LblError.Text = string.Empty;
        }

        private async void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = TxtUsuario.Text.Trim();
            string password = TxtPassword.Password;

            if (string.IsNullOrWhiteSpace(usuario))
            {
                LblError.Text = "Ingrese su nombre de usuario.";
                TxtUsuario.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                LblError.Text = "Ingrese su contraseña.";
                TxtPassword.Focus();
                return;
            }

            BtnIngresar.IsEnabled = false;
            LblError.Text = "Verificando acceso...";

            var resultado = await _apiService.LoginAsync(usuario, password);

            BtnIngresar.IsEnabled = true;

            if (!resultado.Ok || resultado.Sesion == null)
            {
                LblError.Text = resultado.Mensaje;
                return;
            }

            LblError.Text = string.Empty;

            var vm = new MainViewModel(_apiService)
            {
                UsuarioActual = resultado.Sesion,
                EstadoBarra = resultado.Mensaje
            };
            vm.RefrescarEstadoOffline();

            var shell = new ShellWindow(vm);
            shell.Show();
            Close();

            // Sincronizar en segundo plano sin demorar la apertura de ShellWindow
            if (_apiService.ApiEnLinea && _apiService.ContarVentasPendientes() > 0)
            {
                _ = Task.Run(async () =>
                {
                    int synced = await _apiService.SincronizarVentasPendientesAsync();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        vm.RefrescarEstadoOffline();
                        if (synced > 0)
                        {
                            vm.EstadoBarra = $"Se sincronizaron {synced} venta(s) pendientes al iniciar sesión.";
                        }
                    });
                });
            }
        }
    }
}