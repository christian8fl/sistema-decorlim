using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Decorlim.API.Data;
using Decorlim.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Decorlim.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VentasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VentasController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<RegistrarVentaResponseDto>> RegistrarVenta([FromBody] RegistrarVentaRequestDto request)
        {
            if (request.Items == null || request.Items.Count == 0)
            {
                return BadRequest("La venta debe contener al menos un producto.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Login == request.UsuarioLogin && u.Activo);

                if (usuario == null)
                {
                    return BadRequest($"Usuario '{request.UsuarioLogin}' no encontrado o inactivo.");
                }

                var cliente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.Identificacion == (request.IdentificacionCliente ?? "9999999999999") && c.Activo)
                    ?? await _context.Clientes.FirstOrDefaultAsync(c => c.Identificacion == "9999999999999");

                if (cliente == null)
                {
                    return BadRequest("No existe un cliente Consumidor Final configurado en CLIENTES.");
                }

                var turno = await _context.TurnosCaja
                    .FirstOrDefaultAsync(t => t.IdUsuario == usuario.IdUsuario && t.Estado == "Abierto");

                if (turno == null)
                {
                    turno = new TurnoCaja
                    {
                        IdUsuario = usuario.IdUsuario,
                        Apertura = DateTime.Now,
                        FondoInicial = 0.00m,
                        EfecEsperado = 0.00m,
                        Estado = "Abierto"
                    };
                    _context.TurnosCaja.Add(turno);
                    await _context.SaveChangesAsync();
                }

                string tarifa = string.Equals(request.Tarifa, "Mayorista", StringComparison.OrdinalIgnoreCase)
                    ? "Mayorista"
                    : "Minorista";

                const decimal tasaIva = 15.00m;
                decimal subtotalVenta = 0m;
                decimal ivaVenta = 0m;
                var detallesEntidad = new List<DetalleVenta>();

                foreach (var item in request.Items)
                {
                    if (item.Cantidad <= 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest("La cantidad por producto debe ser mayor a cero.");
                    }

                    var producto = await _context.Productos
                        .FirstOrDefaultAsync(p => p.IdProducto == item.IdProducto && p.Activo);

                    if (producto == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest($"Producto con ID {item.IdProducto} no encontrado o inactivo.");
                    }

                    if (producto.StockActual < item.Cantidad)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest($"Stock insuficiente para '{producto.Nombre}'. Disponible: {producto.StockActual}.");
                    }

                    producto.StockActual -= item.Cantidad;

                    decimal precioUnit = item.PrecioUnitario > 0
                        ? item.PrecioUnitario
                        : (tarifa == "Mayorista" && producto.PrecioMayorista > 0
                            ? producto.PrecioMayorista
                            : producto.PrecioMinorista);

                    decimal descuentoLinea = Math.Max(0m, item.Descuento);
                    decimal subtotalLinea = Math.Round((precioUnit * item.Cantidad) - descuentoLinea, 2);
                    decimal ivaLinea = Math.Round(subtotalLinea * (tasaIva / 100m), 2);

                    subtotalVenta += subtotalLinea;
                    ivaVenta += ivaLinea;

                    detallesEntidad.Add(new DetalleVenta
                    {
                        IdProducto = producto.IdProducto,
                        Cantidad = item.Cantidad,
                        Descuento = descuentoLinea,
                        PrecioUnitario = precioUnit,
                        TasaIva = tasaIva,
                        SubtotalLinea = subtotalLinea
                    });
                }

                decimal totalVenta = Math.Round(subtotalVenta + ivaVenta, 2);

                var venta = new Venta
                {
                    IdUsuario = usuario.IdUsuario,
                    IdCliente = cliente.IdCliente,
                    IdTurno = turno.IdTurno,
                    FechaHora = DateTime.Now,
                    Tarifa = tarifa,
                    Subtotal = subtotalVenta,
                    Iva = ivaVenta,
                    Total = totalVenta,
                    Estado = "Completada",
                    Detalles = detallesEntidad
                };

                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                string metodoPago = string.Equals(request.MetodoPago, "Transferencia", StringComparison.OrdinalIgnoreCase)
                    ? "Transferencia"
                    : "Efectivo";

                var pago = new Pago
                {
                    IdVenta = venta.IdVenta,
                    Metodo = metodoPago,
                    Monto = totalVenta,
                    Referencia = request.ReferenciaPago
                };
                _context.Pagos.Add(pago);

                if (metodoPago == "Efectivo")
                {
                    turno.EfecEsperado += totalVenta;
                }

                int siguienteNumero = await _context.Comprobantes.CountAsync() + 1;
                string secuencial = $"001-002-{siguienteNumero:D9}";

                var comprobante = new Comprobante
                {
                    IdVenta = venta.IdVenta,
                    Secuencial = secuencial,
                    EstadoEnvio = "Pendiente",
                    Intentos = 0
                };
                _context.Comprobantes.Add(comprobante);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new RegistrarVentaResponseDto
                {
                    IdVenta = venta.IdVenta,
                    Secuencial = secuencial,
                    Subtotal = subtotalVenta,
                    Iva = ivaVenta,
                    Total = totalVenta,
                    FechaHora = venta.FechaHora
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                string detalleReal = ex.InnerException?.InnerException?.Message
                                  ?? ex.InnerException?.Message
                                  ?? ex.Message;
                return StatusCode(500, $"Error al registrar la venta: {detalleReal}");
            }
        }
    }

    public class RegistrarVentaRequestDto
    {
        public string UsuarioLogin { get; set; } = "cflores_vendedor";
        public string? IdentificacionCliente { get; set; } = "9999999999999";
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