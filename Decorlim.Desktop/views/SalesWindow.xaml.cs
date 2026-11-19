using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;

namespace Decorlim.Desktop
{
    public partial class SalesWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        private const string ApiUrl = "http://localhost:5041/api/productos";
        private readonly string _cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DecorlimPOS",
            "productos_cache.json");
        private readonly string _ventasPendientesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DecorlimPOS",
            "ventas_pendientes.json");

        private readonly ObservableCollection<ItemCarrito> _carrito = new ObservableCollection<ItemCarrito>();
        private List<ProductoDto> _catalogoLocal = new List<ProductoDto>();

        private readonly string _usuarioActivo = "cflores_vendedor";
        private readonly string _rolActivo = "Vendedor";

        public SalesWindow()
        {
            InitializeComponent();
            dgCarrito.ItemsSource = _carrito;

            PreviewKeyDown += SalesWindow_PreviewKeyDown;
            chkMayorista.Click += ChkMayorista_Click;

            Loaded += async (_, _) =>
            {
                await SincronizarCatalogoAsync();
                await ReenviarVentasPendientesAsync();
                ActualizarTotal();
                txtCodigoBarras.Focus();
            };
        }

        public SalesWindow(string usuarioActivo, string rolActivo) : this()
        {
            if (!string.IsNullOrWhiteSpace(usuarioActivo))
            {
                _usuarioActivo = usuarioActivo;
            }

            if (!string.IsNullOrWhiteSpace(rolActivo))
            {
                _rolActivo = rolActivo;
            }

            Title = $"DECORLIM - Punto de Venta POS ({_usuarioActivo} | {_rolActivo})";
        }

        private void SalesWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F4)
            {
                e.Handled = true;
                BtnCobrar_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Delete && dgCarrito.SelectedItem is ItemCarrito seleccionado && !txtCodigoBarras.IsFocused)
            {
                e.Handled = true;
                _carrito.Remove(seleccionado);
                ActualizarTotal();
                txtCodigoBarras.Focus();
            }
        }

        private void ChkMayorista_Click(object sender, RoutedEventArgs e)
        {
            bool usarMayorista = chkMayorista?.IsChecked == true;

            foreach (var item in _carrito)
            {
                var producto = _catalogoLocal.FirstOrDefault(p => p.IdProducto == item.IdProducto);
                if (producto != null)
                {
                    item.PrecioUnitario = (usarMayorista && producto.PrecioMayorista > 0)
                        ? producto.PrecioMayorista
                        : producto.PrecioMinorista;
                }
            }

            dgCarrito.Items.Refresh();
            ActualizarTotal();
            txtCodigoBarras.Focus();
        }

        private async Task SincronizarCatalogoAsync()
        {
            try
            {
                string response = await _httpClient.GetStringAsync(ApiUrl);
                var productos = JsonConvert.DeserializeObject<List<ProductoDto>>(response);

                if (productos != null && productos.Count > 0)
                {
                    _catalogoLocal = productos.Where(p => p.Activo).ToList();
                    GuardarCacheLocal(_catalogoLocal);
                    lblEstadoApi.Text = $"API en línea ({_catalogoLocal.Count} productos) — http://localhost:5041/api/productos";
                    return;
                }
            }
            catch
            {
                // Si la API no responde, intentamos cargar la caché offline local
            }

            _catalogoLocal = CargarCacheLocal();
            if (_catalogoLocal.Count > 0)
            {
                lblEstadoApi.Text = $"Modo Offline ({_catalogoLocal.Count} productos en caché local)";
            }
            else
            {
                lblEstadoApi.Text = "Sin conexión a API ni caché local — verifique Decorlim.API";
            }
        }

        private void GuardarCacheLocal(List<ProductoDto> productos)
        {
            try
            {
                string? carpeta = Path.GetDirectoryName(_cachePath);
                if (!string.IsNullOrEmpty(carpeta) && !Directory.Exists(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }

                string json = JsonConvert.SerializeObject(productos, Formatting.Indented);
                File.WriteAllText(_cachePath, json);
            }
            catch
            {
                // No interrumpe el flujo de caja si falla la escritura en disco
            }
        }

        private List<ProductoDto> CargarCacheLocal()
        {
            try
            {
                if (File.Exists(_cachePath))
                {
                    string json = File.ReadAllText(_cachePath);
                    return JsonConvert.DeserializeObject<List<ProductoDto>>(json) ?? new List<ProductoDto>();
                }
            }
            catch
            {
                // Ignora errores de lectura de caché corrupta
            }

            return new List<ProductoDto>();
        }

        private List<RegistrarVentaRequestDto> CargarVentasPendientes()
        {
            try
            {
                if (File.Exists(_ventasPendientesPath))
                {
                    string json = File.ReadAllText(_ventasPendientesPath);
                    return JsonConvert.DeserializeObject<List<RegistrarVentaRequestDto>>(json) ?? new List<RegistrarVentaRequestDto>();
                }
            }
            catch
            {
                // Ignora errores de lectura si el archivo está vacío o corrupto
            }

            return new List<RegistrarVentaRequestDto>();
        }

        private void GuardarVentasPendientes(List<RegistrarVentaRequestDto> pendientes)
        {
            try
            {
                string? carpeta = Path.GetDirectoryName(_ventasPendientesPath);
                if (!string.IsNullOrEmpty(carpeta) && !Directory.Exists(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }

                string json = JsonConvert.SerializeObject(pendientes, Formatting.Indented);
                File.WriteAllText(_ventasPendientesPath, json);
            }
            catch
            {
                // Evita romper el flujo en caso de fallo de disco
            }
        }

        private void EncolarVentaPendiente(RegistrarVentaRequestDto venta)
        {
            var pendientes = CargarVentasPendientes();
            pendientes.Add(venta);
            GuardarVentasPendientes(pendientes);
        }

        private async Task ReenviarVentasPendientesAsync()
        {
            var pendientes = CargarVentasPendientes();
            if (pendientes.Count == 0)
            {
                return;
            }

            string ventasUrl = ApiUrl.Replace("/api/productos", "/api/ventas");
            var restantes = new List<RegistrarVentaRequestDto>();
            int enviadas = 0;

            foreach (var venta in pendientes)
            {
                try
                {
                    string jsonBody = JsonConvert.SerializeObject(venta);
                    using var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await _httpClient.PostAsync(ventasUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        enviadas++;
                    }
                    else
                    {
                        restantes.Add(venta);
                    }
                }
                catch
                {
                    restantes.Add(venta);
                }
            }

            GuardarVentasPendientes(restantes);

            if (enviadas > 0)
            {
                await SincronizarCatalogoAsync();
                lblEstadoApi.Text += $" | {enviadas} venta(s) pendiente(s) sincronizada(s)";
            }
        }

        private async void TxtCodigoBarras_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                string codigo = txtCodigoBarras.Text.Trim();
                txtCodigoBarras.Clear();

                if (!string.IsNullOrEmpty(codigo))
                {
                    await BuscarYAgregarProductoAsync(codigo);
                }

                txtCodigoBarras.Focus();
            }
        }

        private async Task BuscarYAgregarProductoAsync(string codigo)
        {
            if (_catalogoLocal.Count == 0)
            {
                await SincronizarCatalogoAsync();
            }

            if (_catalogoLocal.Count == 0)
            {
                MessageBox.Show(
                    "No hay catálogo disponible. Verifique que Decorlim.API esté ejecutándose en http://localhost:5041.",
                    "Error de Conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var encontrado = _catalogoLocal.FirstOrDefault(p =>
                string.Equals(p.CodigoBarras?.Trim(), codigo, StringComparison.OrdinalIgnoreCase));

            if (encontrado == null)
            {
                MessageBox.Show(
                    $"No se encontró ningún producto activo con el código: {codigo}",
                    "Producto no registrado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (encontrado.StockActual <= 0)
            {
                MessageBox.Show(
                    $"El producto \"{encontrado.Nombre}\" no tiene stock disponible.",
                    "Sin Stock",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            bool esMayorista = chkMayorista?.IsChecked == true && encontrado.PrecioMayorista > 0;
            decimal precioAplicado = esMayorista ? encontrado.PrecioMayorista : encontrado.PrecioMinorista;

            var itemExistente = _carrito.FirstOrDefault(i => i.IdProducto == encontrado.IdProducto);
            if (itemExistente != null)
            {
                if (itemExistente.Cantidad + 1 > encontrado.StockActual)
                {
                    MessageBox.Show(
                        $"Stock máximo alcanzado ({encontrado.StockActual} unidades) para \"{encontrado.Nombre}\".",
                        "Stock Insuficiente",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                itemExistente.Cantidad++;
                itemExistente.PrecioUnitario = precioAplicado;
                itemExistente.StockDisponible = encontrado.StockActual;
                dgCarrito.Items.Refresh();
            }
            else
            {
                _carrito.Add(new ItemCarrito
                {
                    IdProducto = encontrado.IdProducto,
                    CodigoBarras = encontrado.CodigoBarras ?? string.Empty,
                    Nombre = encontrado.Nombre ?? string.Empty,
                    Presentacion = encontrado.Presentacion ?? string.Empty,
                    StockDisponible = encontrado.StockActual,
                    PrecioUnitario = precioAplicado,
                    Cantidad = 1
                });
            }

            ActualizarTotal();
        }

        private void ActualizarTotal()
        {
            decimal subtotal = Math.Round(_carrito.Sum(item => item.Subtotal), 2);
            decimal iva = Math.Round(subtotal * 0.15m, 2);
            decimal totalConIva = Math.Round(subtotal + iva, 2);

            int items = _carrito.Count;
            int unidades = _carrito.Sum(item => item.Cantidad);
            int pendientes = CargarVentasPendientes().Count;

            txtTotalPagar.Text = totalConIva.ToString("C2");
            if (txtResumenItems != null)
            {
                string textoPendientes = pendientes > 0 ? $"  |  {pendientes} pendiente(s)" : string.Empty;
                txtResumenItems.Text = $"  |  {items} {(items == 1 ? "ítem" : "ítems")} ({unidades} und.)  |  Sub: {subtotal:C2} + IVA: {iva:C2}{textoPendientes}";
            }
        }

        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            _carrito.Clear();
            ActualizarTotal();
            txtCodigoBarras.Focus();
        }

        private async void BtnCobrar_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Count == 0)
            {
                MessageBox.Show(
                    "El carrito está vacío. Escanea o ingresa al menos un producto.",
                    "DECORLIM POS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                txtCodigoBarras.Focus();
                return;
            }

            btnCobrar.IsEnabled = false;
            btnLimpiar.IsEnabled = false;

            string tarifa = chkMayorista?.IsChecked == true ? "Mayorista" : "Minorista";

            var request = new RegistrarVentaRequestDto
            {
                UsuarioLogin = _usuarioActivo,
                IdentificacionCliente = "9999999999999",
                Tarifa = tarifa,
                MetodoPago = "Efectivo",
                Items = _carrito.Select(i => new ItemVentaRequestDto
                {
                    IdProducto = i.IdProducto,
                    Cantidad = i.Cantidad,
                    PrecioUnitario = i.PrecioUnitario,
                    Descuento = 0m
                }).ToList()
            };

            try
            {
                string jsonBody = JsonConvert.SerializeObject(request);
                using var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                string ventasUrl = ApiUrl.Replace("/api/productos", "/api/ventas");
                HttpResponseMessage response = await _httpClient.PostAsync(ventasUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    string detalleError = await response.Content.ReadAsStringAsync();
                    MessageBox.Show(
                        $"No se pudo registrar la venta en el servidor.\nDetalle: {detalleError}",
                        "Error al Cobrar",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                string respuestaJson = await response.Content.ReadAsStringAsync();
                var ventaConfirmada = JsonConvert.DeserializeObject<RegistrarVentaResponseDto>(respuestaJson);

                _carrito.Clear();
                await SincronizarCatalogoAsync();
                await ReenviarVentasPendientesAsync();
                ActualizarTotal();

                MessageBox.Show(
                    $"Venta registrada con éxito\n\n" +
                    $"Secuencial: {ventaConfirmada?.Secuencial ?? "N/A"}\n" +
                    $"Cajero: {_usuarioActivo} ({_rolActivo})\n" +
                    $"Subtotal: {(ventaConfirmada?.Subtotal ?? 0m):C2}\n" +
                    $"IVA (15%): {(ventaConfirmada?.Iva ?? 0m):C2}\n" +
                    $"Total Cobrado: {(ventaConfirmada?.Total ?? 0m):C2}",
                    "Cobro Completado - DECORLIM",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception)
            {
                EncolarVentaPendiente(request);

                // Descuenta stock localmente en caché para evitar vender sin respaldo
                foreach (var item in _carrito)
                {
                    var prodLocal = _catalogoLocal.FirstOrDefault(p => p.IdProducto == item.IdProducto);
                    if (prodLocal != null)
                    {
                        prodLocal.StockActual = Math.Max(0, prodLocal.StockActual - item.Cantidad);
                    }
                }
                GuardarCacheLocal(_catalogoLocal);

                _carrito.Clear();
                ActualizarTotal();

                MessageBox.Show(
                    "No hubo conexión con Decorlim.API en este momento.\n\n" +
                    "La venta se guardó de forma segura en la cola offline local y se sincronizará automáticamente cuando vuelva la conexión.",
                    "Venta Guardada Offline",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                btnCobrar.IsEnabled = true;
                btnLimpiar.IsEnabled = true;
                txtCodigoBarras.Focus();
            }
        }

        public class ProductoDto
        {
            public int IdProducto { get; set; }
            public string CodigoBarras { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string Marca { get; set; } = string.Empty;
            public string Color { get; set; } = string.Empty;
            public string Acabado { get; set; } = string.Empty;
            public string Presentacion { get; set; } = string.Empty;
            public decimal PrecioMinorista { get; set; }
            public decimal PrecioMayorista { get; set; }
            public int StockActual { get; set; }
            public int StockMinimo { get; set; }
            public bool Activo { get; set; } = true;
        }

        public class ItemCarrito
        {
            public int IdProducto { get; set; }
            public string CodigoBarras { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string Presentacion { get; set; } = string.Empty;
            public int StockDisponible { get; set; }
            public decimal PrecioUnitario { get; set; }
            public int Cantidad { get; set; }
            public decimal Subtotal => PrecioUnitario * Cantidad;
        }
    }

    public class RegistrarVentaRequestDto
    {
        public string UsuarioLogin { get; set; } = string.Empty;
        public string? IdentificacionCliente { get; set; }
        public string Tarifa { get; set; } = "Minorista";
        public string MetodoPago { get; set; } = "Efectivo";
        public string? ReferenciaPago { get; set; }
        public List<ItemVentaRequestDto> Items { get; set; } = new();
    }

    public class ItemVentaRequestDto
    {
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
    }

    public class RegistrarVentaResponseDto
    {
        public int IdVenta { get; set; }
        public string Secuencial { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public DateTime FechaHora { get; set; }
    }
}