using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Decorlim.Desktop.Models
{
    public class UsuarioSesion
    {
        public int IdUsuario { get; set; }
        public string Login { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = "Vendedor";
        public bool EsAdministrador => string.Equals(Rol, "Administrador", StringComparison.OrdinalIgnoreCase);
    }

    public class LoginRequest
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ProductoItem
    {
        public int IdProducto { get; set; }
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Presentacion { get; set; } = "Unidad";
        public decimal PrecioMinorista { get; set; }
        public decimal PrecioMayorista { get; set; }
        public int StockActual { get; set; }
        public bool Activo { get; set; } = true;

        public string DisplayText => $"{CodigoBarras} - {Nombre} ({Presentacion})";
    }

    public class ClienteItem
    {
        public int IdCliente { get; set; }
        public string TipoIdentificacion { get; set; } = "CEDULA";
        public string Identificacion { get; set; } = string.Empty;
        public string RazonSocial { get; set; } = string.Empty;
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
        public string? Celular { get; set; }
        public string TipoCliente { get; set; } = "General";
        public bool Activo { get; set; } = true;

        public string DisplayText => $"{Identificacion} - {RazonSocial}";
    }

    public class UsuarioItem
    {
        public int IdUsuario { get; set; }
        public string Login { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = "Vendedor";
        public bool Activo { get; set; } = true;
    }

    public class CrearUsuarioRequest
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = "Vendedor";
    }

    public class ActualizarUsuarioRequest
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = "Vendedor";
        public bool Activo { get; set; } = true;
        public string? NuevaPassword { get; set; }
    }

    public class CarritoItem : INotifyPropertyChanged
    {
        private int _cantidad = 1;
        private decimal _precioUnitario;
        private decimal _descuento;

        public int IdProducto { get; set; }
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Presentacion { get; set; } = string.Empty;
        public int StockDisponible { get; set; }
        public decimal PrecioMinorista { get; set; }
        public decimal PrecioMayorista { get; set; }

        public int Cantidad
        {
            get => _cantidad;
            set
            {
                if (_cantidad != value && value > 0)
                {
                    _cantidad = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Subtotal));
                }
            }
        }

        public decimal PrecioUnitario
        {
            get => _precioUnitario;
            set
            {
                if (_precioUnitario != value)
                {
                    _precioUnitario = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Subtotal));
                }
            }
        }

        public decimal Descuento
        {
            get => _descuento;
            set
            {
                if (_descuento != value && value >= 0)
                {
                    _descuento = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Subtotal));
                }
            }
        }

        public decimal Subtotal => Math.Round((PrecioUnitario * Cantidad) - Descuento, 2);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public class ItemVentaRequest
    {
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
    }

    public class RegistrarVentaRequest
    {
        public string UsuarioLogin { get; set; } = "cflores_vendedor";
        public string? IdentificacionCliente { get; set; } = "9999999999999";
        public string Tarifa { get; set; } = "Minorista";
        public string TipoDocumento { get; set; } = "Factura";
        public string MetodoPago { get; set; } = "Efectivo";
        public string? ReferenciaPago { get; set; }
        public List<ItemVentaRequest> Items { get; set; } = new();
    }

    public class ItemVentaDetalle
    {
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Presentacion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal SubtotalLinea { get; set; }
    }

    public class RegistrarVentaResponse
    {
        public int IdVenta { get; set; }
        public string TipoDocumento { get; set; } = "Factura";
        public string Secuencial { get; set; } = string.Empty;
        public string ClaveAccesoSri { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public string ClienteIdentificacion { get; set; } = string.Empty;
        public string ClienteDireccion { get; set; } = string.Empty;
        public string ClienteTelefono { get; set; } = string.Empty;
        public string ClienteCelular { get; set; } = string.Empty;
        public string ClienteCorreo { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = "Efectivo";
        public string? ReferenciaPago { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public DateTime FechaHora { get; set; }
        public List<ItemVentaDetalle> Items { get; set; } = new();
    }

    public class VentaHistorialItem
    {
        public int IdVenta { get; set; }
        public DateTime FechaHora { get; set; }
        public string Vendedor { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string Identificacion { get; set; } = string.Empty;
        public string DireccionCliente { get; set; } = string.Empty;
        public string TelefonoCliente { get; set; } = string.Empty;
        public string CelularCliente { get; set; } = string.Empty;
        public string CorreoCliente { get; set; } = string.Empty;
        public string Tarifa { get; set; } = "Minorista";
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string MetodoPago { get; set; } = "Efectivo";
        public string? ReferenciaPago { get; set; }
        public string Secuencial { get; set; } = string.Empty;
        public string EstadoSri { get; set; } = "Pendiente";
    }

    public class DashboardResumen
    {
        public decimal TotalVentasHoy { get; set; }
        public int CantidadVentasHoy { get; set; }
        public decimal TicketPromedio { get; set; }
        public decimal TotalEfectivoHoy { get; set; }
        public decimal TotalTransferenciaHoy { get; set; }
        public List<StockBajoItem> ProductosStockBajo { get; set; } = new();
        public List<ComprobanteResumenItem> ComprobantesRecientes { get; set; } = new();
    }

    public class StockBajoItem
    {
        public int IdProducto { get; set; }
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Presentacion { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public decimal PrecioMinorista { get; set; }
    }

    public class ComprobanteResumenItem
    {
        public int IdComprobante { get; set; }
        public int IdVenta { get; set; }
        public string Secuencial { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Identificacion { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string EstadoEnvio { get; set; } = "Pendiente";
    }

    public class EtiquetaLoteItem : INotifyPropertyChanged
    {
        private int _copias = 1;
        public int IdProducto { get; set; }
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Presentacion { get; set; } = string.Empty;
        public decimal PrecioMinorista { get; set; }
        public decimal PrecioMayorista { get; set; }

        public int Copias
        {
            get => _copias;
            set
            {
                if (_copias != value && value > 0)
                {
                    _copias = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Copias)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}