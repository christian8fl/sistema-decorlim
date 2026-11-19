using System.Windows;
using System.Windows.Input;

namespace Decorlim.Desktop
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => txtUsuario.Focus();
            txtUsuario.KeyDown += CampoLogin_KeyDown;
            txtPassword.KeyDown += CampoLogin_KeyDown;
        }

        private void CampoLogin_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                BtnIngresar_Click(btnIngresar, new RoutedEventArgs());
            }
        }

        private void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = txtUsuario.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(usuario))
            {
                MessageBox.Show(
                    "Ingrese su nombre de usuario.",
                    "Campo Requerido",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtUsuario.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show(
                    "Ingrese su contraseña.",
                    "Campo Requerido",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtPassword.Focus();
                return;
            }

            string? rol = usuario.ToLowerInvariant() switch
            {
                "mtorres_admin" => "Administrador",
                "cflores_vendedor" => "Vendedor",
                _ => null
            };

            if (rol == null)
            {
                MessageBox.Show(
                    "Usuario o contraseña incorrectos.",
                    "Error de Autenticación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtPassword.Clear();
                txtPassword.Focus();
                return;
            }

            SalesWindow salesWindow = new SalesWindow(usuario, rol);
            salesWindow.Show();
            Close();
        }
    }
}