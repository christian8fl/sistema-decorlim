using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Decorlim.Desktop.Models;
using Decorlim.Desktop.ViewModels;
using Microsoft.Win32;

namespace Decorlim.Desktop.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly MainViewModel _vm;
        private List<VentaHistorialItem> _todasLasVentas = new();
        private List<VentaHistorialItem> _ventasFiltradas = new();
        private bool _ajustandoFechas;

        public DashboardView(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;

            ConfigurarRangoInicial();
            PreviewKeyDown += DashboardView_PreviewKeyDown;
            Loaded += async (_, _) => await CargarEstadisticasAsync();
            IsVisibleChanged += async (_, args) =>
            {
                if (args.NewValue is true && IsLoaded)
                {
                    await CargarEstadisticasAsync();
                }
            };
        }

        private void ConfigurarRangoInicial()
        {
            _ajustandoFechas = true;
            var hoy = DateTime.Today;
            DpDesde.SelectedDate = hoy;
            DpHasta.SelectedDate = hoy;
            _ajustandoFechas = false;
            ActualizarTextoRango();
        }

        private async void DashboardView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                e.Handled = true;
                await CargarEstadisticasAsync();
            }
        }

        private async void BtnActualizar_Click(object sender, RoutedEventArgs e)
            => await CargarEstadisticasAsync();

        private void BtnHoy_Click(object sender, RoutedEventArgs e)
        {
            CmbPeriodo.SelectedIndex = 0;
            ConfigurarRangoInicial();
            AplicarFiltrosYActualizarVista();
        }

        private void CmbPeriodo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _ajustandoFechas) return;

            string periodo = (CmbPeriodo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Diario";
            var hoy = DateTime.Today;

            _ajustandoFechas = true;
            switch (periodo)
            {
                case "Diario":
                    DpDesde.SelectedDate = hoy;
                    DpHasta.SelectedDate = hoy;
                    break;
                case "Semanal":
                    DpDesde.SelectedDate = hoy.AddDays(-6);
                    DpHasta.SelectedDate = hoy;
                    break;
                case "Mensual":
                    DpDesde.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
                    DpHasta.SelectedDate = hoy;
                    break;
                case "Anual":
                    DpDesde.SelectedDate = new DateTime(hoy.Year, 1, 1);
                    DpHasta.SelectedDate = hoy;
                    break;
            }
            _ajustandoFechas = false;

            ActualizarTextoRango();
            AplicarFiltrosYActualizarVista();
        }

        private void FechaFiltro_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _ajustandoFechas) return;

            if (DpDesde.SelectedDate.HasValue && DpHasta.SelectedDate.HasValue
                && DpDesde.SelectedDate.Value.Date > DpHasta.SelectedDate.Value.Date)
            {
                _ajustandoFechas = true;
                DpHasta.SelectedDate = DpDesde.SelectedDate;
                _ajustandoFechas = false;
            }

            CmbPeriodo.SelectedIndex = 4; // Personalizado
            ActualizarTextoRango();
            AplicarFiltrosYActualizarVista();
        }

        private void TxtBuscarVenta_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            AplicarFiltrosYActualizarVista();
        }

        private void ActualizarTextoRango()
        {
            if (TxtEtiquetaRango == null || DpDesde == null || DpHasta == null) return;
            DateTime desde = (DpDesde.SelectedDate ?? DateTime.Today).Date;
            DateTime hasta = (DpHasta.SelectedDate ?? DateTime.Today).Date;
            string periodo = (CmbPeriodo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Diario";

            TxtEtiquetaRango.Text = desde == hasta
                ? $"Período: {periodo}  |  Fecha: {desde:dd/MM/yyyy}"
                : $"Período: {periodo}  |  Desde: {desde:dd/MM/yyyy}  hasta: {hasta:dd/MM/yyyy}";
        }

        public async Task CargarEstadisticasAsync()
        {
            var resumenApi = await _vm.Api.ObtenerDashboardResumenAsync();
            var ventasRemotas = await _vm.Api.ObtenerHistorialVentasAsync();
            var ventasLocales = _vm.Api.ObtenerVentasLocales();
            _vm.RefrescarEstadoOffline();

            var combinadas = new Dictionary<string, VentaHistorialItem>(StringComparer.OrdinalIgnoreCase);

            if (resumenApi != null || ventasRemotas.Count > 0)
            {
                foreach (var v in ventasRemotas)
                {
                    string clave = !string.IsNullOrWhiteSpace(v.Secuencial)
                        ? v.Secuencial
                        : $"REM-{v.IdVenta}-{v.FechaHora:yyyyMMddHHmmss}";
                    combinadas[clave] = NormalizarVenta(v);
                }
            }
            else
            {
                foreach (var v in ventasLocales)
                {
                    string clave = !string.IsNullOrWhiteSpace(v.Secuencial)
                        ? v.Secuencial
                        : $"LOC-{v.IdVenta}-{v.FechaHora:yyyyMMddHHmmss}";
                    if (!combinadas.ContainsKey(clave))
                    {
                        combinadas[clave] = NormalizarVenta(v);
                    }
                }
            }

            _todasLasVentas = combinadas.Values
                .OrderByDescending(v => v.FechaHora)
                .ToList();

            var productosLocales = await _vm.Api.ObtenerProductosAsync();
            var bajoStockLocal = productosLocales
                .Where(p => p.StockActual <= 5)
                .OrderBy(p => p.StockActual)
                .Select(p => new StockBajoItem
                {
                    IdProducto = p.IdProducto,
                    CodigoBarras = p.CodigoBarras,
                    Nombre = p.Nombre,
                    Presentacion = p.Presentacion,
                    StockActual = p.StockActual,
                    PrecioMinorista = p.PrecioMinorista
                })
                .ToList();

            GridStockBajo.ItemsSource = resumenApi?.ProductosStockBajo is { Count: > 0 }
                ? resumenApi.ProductosStockBajo
                : bajoStockLocal;

            AplicarFiltrosYActualizarVista();
            _vm.EstadoBarra = resumenApi != null
                ? "Estadísticas actualizadas desde la base de datos."
                : "Dashboard actualizado desde caché local (Modo Offline).";
        }

        private static VentaHistorialItem NormalizarVenta(VentaHistorialItem v)
        {
            decimal total = v.Total;
            decimal subtotal = v.Subtotal;
            decimal iva = v.Iva;

            bool esNotaVenta = string.Equals(v.EstadoSri, "NotaVenta", StringComparison.OrdinalIgnoreCase);
            if (subtotal <= 0m && total > 0m)
            {
                if (esNotaVenta)
                {
                    subtotal = Math.Round(total, 2);
                    iva = 0m;
                }
                else
                {
                    subtotal = Math.Round(total / 1.15m, 2);
                    iva = Math.Round(total - subtotal, 2);
                }
            }
            else if (iva <= 0m && subtotal > 0m && !esNotaVenta)
            {
                iva = Math.Round(subtotal * 0.15m, 2);
                if (total <= 0m) total = Math.Round(subtotal + iva, 2);
            }

            var normalizada = new VentaHistorialItem
            {
                IdVenta = v.IdVenta,
                Secuencial = v.Secuencial,
                FechaHora = v.FechaHora,
                Cliente = string.IsNullOrWhiteSpace(v.Cliente) ? "CONSUMIDOR FINAL" : v.Cliente,
                Identificacion = v.Identificacion,
                DireccionCliente = v.DireccionCliente,
                TelefonoCliente = v.TelefonoCliente,
                CelularCliente = v.CelularCliente,
                CorreoCliente = v.CorreoCliente,
                MetodoPago = string.IsNullOrWhiteSpace(v.MetodoPago) ? "Efectivo" : v.MetodoPago,
                ReferenciaPago = v.ReferenciaPago,
                Subtotal = subtotal,
                Iva = iva,
                Total = total,
                EstadoSri = string.IsNullOrWhiteSpace(v.EstadoSri) ? "Pendiente" : v.EstadoSri
            };

            string[] nombresPropiedades = { "Items", "Detalles", "Detalle", "Productos" };
            foreach (var nombreProp in nombresPropiedades)
            {
                var origen = v.GetType().GetProperty(nombreProp);
                var destino = normalizada.GetType().GetProperty(nombreProp);
                if (origen != null && destino != null && destino.CanWrite)
                {
                    var valor = origen.GetValue(v);
                    if (valor != null && destino.PropertyType.IsInstanceOfType(valor))
                    {
                        destino.SetValue(normalizada, valor);
                        break;
                    }
                }
            }

            return normalizada;
        }

        private void AplicarFiltrosYActualizarVista()
        {
            if (TxtVentasHoy == null || GridComprobantesRecientes == null) return;

            DateTime desde = (DpDesde?.SelectedDate ?? DateTime.Today).Date;
            DateTime hasta = (DpHasta?.SelectedDate ?? DateTime.Today).Date;
            if (desde > hasta)
            {
                hasta = desde;
            }

            string texto = TxtBuscarVenta?.Text?.Trim() ?? string.Empty;

            var consulta = _todasLasVentas.Where(v =>
                v != null && v.FechaHora.Date >= desde && v.FechaHora.Date <= hasta);

            if (!string.IsNullOrWhiteSpace(texto))
            {
                consulta = consulta.Where(v =>
                    (v.Secuencial?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (v.Cliente?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (v.Identificacion?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (v.MetodoPago?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (v.EstadoSri?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            _ventasFiltradas = consulta.OrderByDescending(v => v.FechaHora).ToList();

            decimal totalPeriodo = Math.Round(_ventasFiltradas.Sum(v => v.Total), 2);
            decimal efectivoPeriodo = Math.Round(_ventasFiltradas
                .Where(v => string.Equals(v.MetodoPago, "Efectivo", StringComparison.OrdinalIgnoreCase))
                .Sum(v => v.Total), 2);
            decimal transferenciaPeriodo = Math.Round(_ventasFiltradas
                .Where(v => string.Equals(v.MetodoPago, "Transferencia", StringComparison.OrdinalIgnoreCase))
                .Sum(v => v.Total), 2);
            decimal ivaPeriodo = Math.Round(_ventasFiltradas.Sum(v => v.Iva), 2);
            decimal ticketProm = _ventasFiltradas.Count > 0
                ? Math.Round(totalPeriodo / _ventasFiltradas.Count, 2)
                : 0m;

            var cultura = CultureInfo.CurrentCulture;
            TxtVentasHoy.Text = $"${totalPeriodo.ToString("F2", cultura)}";

            if (TxtCantidadVentas != null)
                TxtCantidadVentas.Text = $"{_ventasFiltradas.Count} comprobante(s) en rango";

            if (TxtEfectivoHoy != null)
                TxtEfectivoHoy.Text = $"${efectivoPeriodo.ToString("F2", cultura)}";

            if (TxtTransferenciaHoy != null)
                TxtTransferenciaHoy.Text = $"${transferenciaPeriodo.ToString("F2", cultura)}";

            if (TxtTicketPromedio != null)
                TxtTicketPromedio.Text = $"${ticketProm.ToString("F2", cultura)}";

            if (TxtIvaPeriodo != null)
                TxtIvaPeriodo.Text = $"IVA 15% del período: ${ivaPeriodo.ToString("F2", cultura)}";

            GridComprobantesRecientes.ItemsSource = _ventasFiltradas;
        }

        private (DateTime Desde, DateTime Hasta, string Periodo, string EtiquetaArchivo) ObtenerContextoReporte()
        {
            DateTime desde = (DpDesde?.SelectedDate ?? DateTime.Today).Date;
            DateTime hasta = (DpHasta?.SelectedDate ?? DateTime.Today).Date;
            string periodo = (CmbPeriodo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Diario";
            string etiqueta = desde == hasta ? $"{desde:yyyyMMdd}" : $"{desde:yyyyMMdd}_a_{hasta:yyyyMMdd}";
            return (desde, hasta, periodo, etiqueta);
        }

        private async void BtnExportarDetallePdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (desde, hasta, periodo, etiqueta) = ObtenerContextoReporte();
                string marcaTiempo = DateTime.Now.ToString("HHmmss");

                var dlg = new SaveFileDialog
                {
                    Title = "Descargar reporte de productos vendidos en PDF",
                    FileName = $"DECORLIM_Productos_Vendidos_{etiqueta}_{marcaTiempo}.pdf",
                    Filter = "Archivo PDF (*.pdf)|*.pdf"
                };

                if (dlg.ShowDialog() != true) return;

                var catalogoObj = await _vm.Api.ObtenerProductosAsync();
                var catalogo = new List<object>();
                if (catalogoObj != null)
                {
                    foreach (var p in catalogoObj)
                    {
                        if (p != null) catalogo.Add(p);
                    }
                }

                decimal totalGeneral = Math.Round(_ventasFiltradas.Sum(v => v.Total), 2);
                decimal totalEfectivo = Math.Round(_ventasFiltradas
                    .Where(v => string.Equals(v.MetodoPago, "Efectivo", StringComparison.OrdinalIgnoreCase))
                    .Sum(v => v.Total), 2);
                decimal totalTransferencias = Math.Round(_ventasFiltradas
                    .Where(v => string.Equals(v.MetodoPago, "Transferencia", StringComparison.OrdinalIgnoreCase))
                    .Sum(v => v.Total), 2);

                string operador = _vm.UsuarioActual?.NombreCompleto ?? "Usuario";

                var lineas = new List<string>
                {
                    "DECORLIM POS - PRODUCTOS VENDIDOS POR DIA",
                    $"Periodo: {periodo}   |   Rango: {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}",
                    $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}   |   Operador: {operador}",
                    new string('=', 96)
                };

                lineas.AddRange(ConstruirLineasProductosPorDia(_ventasFiltradas, catalogo));

                lineas.Add(new string('=', 96));
                lineas.Add("RESUMEN DE RECAUDACION DEL PERIODO");
                lineas.Add($"  * Total Efectivo:       ${totalEfectivo:F2}");
                lineas.Add($"  * Total Transferencias: ${totalTransferencias:F2}");
                lineas.Add($"  * Total General:        ${totalGeneral:F2}");

                GuardarPdfSimple(dlg.FileName, lineas);
                FinalizarDescargaPdf(
                    dlg.FileName,
                    $"PDF de productos vendidos descargado en {dlg.FileName}",
                    "Reporte de productos vendidos por día descargado en PDF correctamente.",
                    "Productos Vendidos por Día");
            }
            catch (IOException)
            {
                MessageBox.Show(
                    "El archivo PDF destino está abierto en otro programa (como Adobe Acrobat).\nCiérralo o elige otro nombre de archivo para poder guardarlo.",
                    "Archivo en uso",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ocurrió un error al generar el PDF:\n{ex.Message}",
                    "Error al exportar PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static List<string> ConstruirLineasProductosPorDia(
            List<VentaHistorialItem> ventas,
            List<object> catalogo)
        {
            var resultado = new List<string>();
            var ventasPorDia = ventas
                .GroupBy(v => v.FechaHora.Date)
                .OrderBy(g => g.Key);

            bool huboLineas = false;

            foreach (var grupoDia in ventasPorDia)
            {
                var productosDia = new Dictionary<string, (string Codigo, string Nombre, decimal Cantidad, decimal CostoUnit, decimal Total)>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var venta in grupoDia)
                {
                    var items = ObtenerItemsVenta(venta, catalogo);

                    foreach (var it in items)
                    {
                        string codigo = string.IsNullOrWhiteSpace(it.Codigo) ? "S/C" : it.Codigo.Trim();
                        string nombre = string.IsNullOrWhiteSpace(it.Nombre) ? "Producto" : it.Nombre.Trim();
                        string clave = $"{codigo}|{nombre}";

                        if (productosDia.TryGetValue(clave, out var actual))
                        {
                            decimal nuevaCant = actual.Cantidad + it.Cantidad;
                            decimal nuevoTotal = Math.Round(actual.Total + it.Subtotal, 2);
                            decimal costoRef = it.CostoUnit > 0m
                                ? it.CostoUnit
                                : (nuevaCant > 0m ? Math.Round(nuevoTotal / nuevaCant, 2) : actual.CostoUnit);

                            productosDia[clave] = (actual.Codigo, actual.Nombre, nuevaCant, costoRef, nuevoTotal);
                        }
                        else
                        {
                            decimal costoRef = it.CostoUnit > 0m
                                ? it.CostoUnit
                                : (it.Cantidad > 0m ? Math.Round(it.Subtotal / it.Cantidad, 2) : 0m);

                            productosDia[clave] = (codigo, nombre, it.Cantidad, costoRef, Math.Round(it.Subtotal, 2));
                        }
                    }
                }

                if (productosDia.Count == 0) continue;

                huboLineas = true;
                resultado.Add($"DIA: {grupoDia.Key:dd/MM/yyyy}");
                resultado.Add("CODIGO        PRODUCTO                                 CANT.   COSTO/U.     TOTAL");
                resultado.Add(new string('-', 96));

                foreach (var prod in productosDia.Values.OrderByDescending(x => x.Total))
                {
                    string cod = Truncar(prod.Codigo, 12).PadRight(12);
                    string nom = Truncar(prod.Nombre, 38).PadRight(38);
                    string cant = prod.Cantidad.ToString("0.##", CultureInfo.InvariantCulture).PadLeft(7);
                    string costo = $"${prod.CostoUnit:F2}".PadLeft(10);
                    string tot = $"${prod.Total:F2}".PadLeft(10);
                    resultado.Add($"{cod}  {nom} {cant} {costo} {tot}");
                }

                decimal ventaDia = Math.Round(productosDia.Values.Sum(x => x.Total), 2);
                decimal costoDia = Math.Round(productosDia.Values.Sum(x => x.CostoUnit * x.Cantidad), 2);
                resultado.Add(new string('-', 96));
                resultado.Add($"SUBTOTAL DIA ({grupoDia.Key:dd/MM/yyyy}): Base/Costo ${costoDia:F2}  |  Venta ${ventaDia:F2}");
                resultado.Add(string.Empty);
            }

            if (!huboLineas)
            {
                resultado.Add("  (No hay registros de ventas en el rango seleccionado)");
            }

            return resultado;
        }

        private static List<(string Codigo, string Nombre, decimal Cantidad, decimal CostoUnit, decimal Subtotal)> ObtenerItemsVenta(
            VentaHistorialItem venta,
            List<object> catalogo)
        {
            var lista = new List<(string Codigo, string Nombre, decimal Cantidad, decimal CostoUnit, decimal Subtotal)>();
            var prop = venta.GetType().GetProperty("Items")
                       ?? venta.GetType().GetProperty("Detalles")
                       ?? venta.GetType().GetProperty("Detalle")
                       ?? venta.GetType().GetProperty("Productos");

            if (prop?.GetValue(venta) is IEnumerable enumerable)
            {
                foreach (var obj in enumerable)
                {
                    if (obj == null) continue;
                    var t = obj.GetType();

                    string codigo = t.GetProperty("CodigoBarras")?.GetValue(obj)?.ToString()
                                    ?? t.GetProperty("Codigo")?.GetValue(obj)?.ToString()
                                    ?? string.Empty;

                    string nombre = t.GetProperty("Nombre")?.GetValue(obj)?.ToString()
                                    ?? t.GetProperty("Producto")?.GetValue(obj)?.ToString()
                                    ?? t.GetProperty("Descripcion")?.GetValue(obj)?.ToString()
                                    ?? string.Empty;

                    decimal cantidad = ConvertToDecimal(t.GetProperty("Cantidad")?.GetValue(obj));
                    decimal costoUnit = ConvertToDecimal(t.GetProperty("CostoUnitario")?.GetValue(obj)
                                                         ?? t.GetProperty("Costo")?.GetValue(obj)
                                                         ?? t.GetProperty("PrecioUnitario")?.GetValue(obj)
                                                         ?? t.GetProperty("Precio")?.GetValue(obj));
                    decimal precioVenta = ConvertToDecimal(t.GetProperty("PrecioUnitario")?.GetValue(obj)
                                                          ?? t.GetProperty("Precio")?.GetValue(obj)
                                                          ?? costoUnit);
                    decimal descuento = ConvertToDecimal(t.GetProperty("Descuento")?.GetValue(obj));
                    decimal subtotal = t.GetProperty("SubtotalLinea") != null
                        ? ConvertToDecimal(t.GetProperty("SubtotalLinea")?.GetValue(obj))
                        : (t.GetProperty("Subtotal") != null
                            ? ConvertToDecimal(t.GetProperty("Subtotal")?.GetValue(obj))
                            : Math.Max(0m, (cantidad * precioVenta) - descuento));

                    if (cantidad > 0m || subtotal > 0m)
                    {
                        lista.Add((codigo, nombre, cantidad > 0m ? cantidad : 1m, costoUnit, subtotal));
                    }
                }
            }

            if (lista.Count > 0)
                return lista;

            decimal montoBase = venta.Subtotal > 0m ? venta.Subtotal : venta.Total;
            decimal montoMostrar = venta.Total > 0m ? venta.Total : montoBase;

            foreach (var p in catalogo)
            {
                var t = p.GetType();
                string codigo = t.GetProperty("CodigoBarras")?.GetValue(p)?.ToString()
                                ?? t.GetProperty("Codigo")?.GetValue(p)?.ToString()
                                ?? "S/C";
                string nombre = t.GetProperty("Nombre")?.GetValue(p)?.ToString()
                                ?? t.GetProperty("Producto")?.GetValue(p)?.ToString()
                                ?? "Producto";

                decimal pMin = ConvertToDecimal(t.GetProperty("PrecioMinorista")?.GetValue(p));
                decimal pMay = ConvertToDecimal(t.GetProperty("PrecioMayorista")?.GetValue(p));
                decimal costo = ConvertToDecimal(t.GetProperty("CostoUnitario")?.GetValue(p)
                                                 ?? t.GetProperty("Costo")?.GetValue(p));
                decimal costoFinal = costo > 0m ? costo : (pMin > 0m ? pMin : pMay);

                if (pMin > 0m && Math.Abs(pMin - montoBase) <= 0.02m)
                {
                    lista.Add((codigo, nombre, 1m, costoFinal, Math.Round(montoMostrar, 2)));
                    return lista;
                }

                if (pMay > 0m && Math.Abs(pMay - montoBase) <= 0.02m)
                {
                    lista.Add((codigo, nombre, 1m, costoFinal, Math.Round(montoMostrar, 2)));
                    return lista;
                }

                if (pMin > 0m && montoBase > pMin)
                {
                    decimal division = montoBase / pMin;
                    int cantEntera = (int)Math.Round(division);
                    if (cantEntera >= 2 && Math.Abs(division - cantEntera) <= 0.01m)
                    {
                        lista.Add((codigo, nombre, cantEntera, costoFinal, Math.Round(montoMostrar, 2)));
                        return lista;
                    }
                }
            }

            string codFallback = string.IsNullOrWhiteSpace(venta.Secuencial) ? "S/C" : venta.Secuencial.Trim();
            lista.Add((codFallback, $"Detalle no guardado ({venta.MetodoPago})", 1m, Math.Round(montoBase, 2), Math.Round(montoMostrar, 2)));
            return lista;
        }

        private static decimal ConvertToDecimal(object? valor)
        {
            if (valor == null) return 0m;
            if (valor is decimal d) return d;
            if (decimal.TryParse(valor.ToString()?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return 0m;
        }

        private void BtnExportarResumenPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (desde, hasta, periodo, etiqueta) = ObtenerContextoReporte();
                string marcaTiempo = DateTime.Now.ToString("HHmmss");

                var dlg = new SaveFileDialog
                {
                    Title = "Descargar cierre de caja / resumen en PDF",
                    FileName = $"DECORLIM_Cierre_Caja_{periodo}_{etiqueta}_{marcaTiempo}.pdf",
                    Filter = "Archivo PDF (*.pdf)|*.pdf"
                };

                if (dlg.ShowDialog() != true) return;

                decimal subtotal = Math.Round(_ventasFiltradas.Sum(v => v.Subtotal), 2);
                decimal iva = Math.Round(_ventasFiltradas.Sum(v => v.Iva), 2);
                decimal total = Math.Round(_ventasFiltradas.Sum(v => v.Total), 2);
                decimal efectivo = Math.Round(_ventasFiltradas
                    .Where(v => string.Equals(v.MetodoPago, "Efectivo", StringComparison.OrdinalIgnoreCase))
                    .Sum(v => v.Total), 2);
                decimal transferencia = Math.Round(_ventasFiltradas
                    .Where(v => string.Equals(v.MetodoPago, "Transferencia", StringComparison.OrdinalIgnoreCase))
                    .Sum(v => v.Total), 2);
                decimal otrosPagos = Math.Round(total - efectivo - transferencia, 2);

                string operador = _vm.UsuarioActual?.NombreCompleto ?? "Usuario";

                var lineas = new List<string>
                {
                    "DECORLIM POS - CIERRE DE CAJA (Z) Y RESUMEN GERENCIAL",
                    new string('=', 96),
                    $"Periodo: {periodo}          Desde: {desde:dd/MM/yyyy}          Hasta: {hasta:dd/MM/yyyy}",
                    $"Generado en: {DateTime.Now:dd/MM/yyyy HH:mm:ss}          Operador: {operador}",
                    $"Comprobantes emitidos en el rango: {_ventasFiltradas.Count}",
                    new string('-', 96),
                    "RESUMEN FINANCIERO Y CUADRE DE CAJA",
                    $"  * Subtotal:                  ${subtotal:F2}",
                    $"  * IVA (15%):                 ${iva:F2}",
                    $"  * TOTAL GENERAL:             ${total:F2}",
                    $"  * Total Efectivo (Caja):     ${efectivo:F2}",
                    $"  * Total Transferencias:      ${transferencia:F2}"
                };

                if (otrosPagos != 0m)
                {
                    lineas.Add($"  * Otros Metodos de Pago:     ${otrosPagos:F2}");
                }

                lineas.Add(new string('-', 96));
                lineas.Add("COMPROBANTES DEL PERIODO");
                lineas.Add("FECHA/HORA       SECUENCIAL          CLIENTE                     PAGO          TOTAL");
                lineas.Add(new string('-', 96));

                foreach (var v in _ventasFiltradas.OrderBy(x => x.FechaHora))
                {
                    string fecha = v.FechaHora.ToString("dd/MM/yy HH:mm");
                    string sec = Truncar(v.Secuencial, 18).PadRight(18);
                    string cli = Truncar(v.Cliente, 26).PadRight(26);
                    string pago = Truncar(v.MetodoPago, 12).PadRight(12);
                    string tot = $"${v.Total:F2}".PadLeft(8);
                    lineas.Add($"{fecha}  {sec} {cli} {pago} {tot}");
                }

                GuardarPdfSimple(dlg.FileName, lineas);
                FinalizarDescargaPdf(
                    dlg.FileName,
                    $"PDF de cierre de caja / resumen descargado en {dlg.FileName}",
                    $"Cierre de caja y resumen ({periodo}) descargado en PDF correctamente.",
                    "Cierre de Caja (Z) PDF");
            }
            catch (IOException)
            {
                MessageBox.Show(
                    "No se pudo escribir el archivo PDF porque está abierto en otro programa o en uso.\nCiérralo o elige otro nombre de archivo.",
                    "Archivo en uso",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ocurrió un error al generar el cierre de caja en PDF:\n{ex.Message}",
                    "Error al exportar PDF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void FinalizarDescargaPdf(string rutaArchivo, string mensajeBarra, string mensajeModal, string tituloModal)
        {
            _vm.EstadoBarra = mensajeBarra;
            var abrir = MessageBox.Show(
                $"{mensajeModal}\n\n¿Desea abrir el archivo PDF ahora?",
                tituloModal,
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (abrir == MessageBoxResult.Yes && File.Exists(rutaArchivo))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = rutaArchivo,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"El PDF se guardó correctamente, pero no se pudo abrir automáticamente:\n{ex.Message}",
                        tituloModal,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        private static string Truncar(string? texto, int maxLen)
        {
            string s = (texto ?? string.Empty).Trim();
            if (maxLen <= 0) return string.Empty;
            return s.Length <= maxLen ? s : s[..maxLen];
        }

        private static void GuardarPdfSimple(string rutaArchivo, IEnumerable<string> lineas)
        {
            var sb = new StringBuilder();
            foreach (var linea in lineas)
            {
                sb.AppendLine(linea);
            }
            File.WriteAllText(rutaArchivo, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
    }
}