using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Decorlim.Desktop.Models;
using Decorlim.Desktop.Services;

namespace Decorlim.Desktop.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private UsuarioSesion _usuarioActual = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EstadoConexionTexto))]
        private bool _apiEnLinea;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ColaOfflineTexto))]
        private int _ventasPendientesCount;

        [ObservableProperty]
        private string _estadoBarra = "Listo";

        [ObservableProperty]
        private bool _estaSincronizando;

        public ApiService Api { get; }

        public MainViewModel(ApiService api)
        {
            Api = api ?? throw new ArgumentNullException(nameof(api));
            RefrescarEstadoOffline();
        }

        public UsuarioSesion UsuarioActual
        {
            get => _usuarioActual;
            set
            {
                if (SetProperty(ref _usuarioActual, value ?? new UsuarioSesion()))
                {
                    OnPropertyChanged(nameof(EsAdministrador));
                    OnPropertyChanged(nameof(EtiquetaRol));
                }
            }
        }

        public bool EsAdministrador
        {
            get
            {
                if (_usuarioActual?.EsAdministrador == true)
                    return true;

                string rolNormalizado = (_usuarioActual?.Rol ?? string.Empty).Trim();
                return rolNormalizado.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
                    || rolNormalizado.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase)
                    || rolNormalizado.Equals("ADMINISTRATOR", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string EtiquetaRol => EsAdministrador ? "ADMINISTRADOR" : "VENDEDOR";

        public string EstadoConexionTexto => ApiEnLinea
            ? "En línea (API activa)"
            : "Modo Offline (Caché local)";

        public string ColaOfflineTexto => VentasPendientesCount > 0
            ? $"Pendientes por sincronizar: {VentasPendientesCount}"
            : "Cola offline vacía";

        public void RefrescarEstadoOffline()
        {
            ApiEnLinea = Api.ApiEnLinea;
            VentasPendientesCount = Api.ContarVentasPendientes();
        }

        [RelayCommand(CanExecute = nameof(PuedeSincronizar))]
        private async Task SincronizarColaOfflineAsync()
        {
            if (EstaSincronizando) return;

            try
            {
                EstaSincronizando = true;
                EstadoBarra = "Sincronizando ventas pendientes...";

                int sincronizadas = await Api.SincronizarVentasPendientesAsync();
                RefrescarEstadoOffline();

                EstadoBarra = sincronizadas > 0
                    ? $"Sincronizadas {sincronizadas} venta(s) correctamente."
                    : (ApiEnLinea ? "No había ventas pendientes o no se pudieron enviar." : "Sin conexión con la API (Modo Offline).");
            }
            catch (Exception ex)
            {
                EstadoBarra = $"Error al sincronizar: {ex.Message}";
            }
            finally
            {
                EstaSincronizando = false;
                SincronizarColaOfflineCommand.NotifyCanExecuteChanged();
            }
        }

        private bool PuedeSincronizar() => !EstaSincronizando && VentasPendientesCount > 0;
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}