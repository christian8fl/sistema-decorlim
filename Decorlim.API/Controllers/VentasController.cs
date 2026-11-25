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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VentaHistorialDto>>> GetVentas()
        {
            var historial = await (
                from v in _context.Ventas
                join cl in _context.Clientes on v.IdCliente equals cl.IdCliente
                join u in _context.Usuarios on v.IdUsuario equals u.IdUsuario
                join c in _context.Comprobantes on v.IdVenta equals c.IdVenta into compGroup
                from c in compGroup.DefaultIfEmpty()
                join p in _context.Pagos on v.IdVenta equals p.IdVenta into pagoGroup
                from p in pagoGroup.DefaultIfEmpty()
                orderby v.FechaHora descending
                select new VentaHistorialDto
                {
                    IdVenta = v.IdVenta,
                    FechaHora = v.FechaHora,
                    Vendedor = u.NombreCompleto,
                    Cliente = cl.RazonSocial,
                    Identificacion = cl.Identificacion,
                    DireccionCliente = cl.Direccion ?? string.Empty,
                    TelefonoCliente = cl.Telefono ?? string.Empty,
                    CelularCliente = cl.Celular ?? string.Empty,
                    CorreoCliente = cl.Correo ?? string.Empty,
                    Tarifa = v.Tarifa,
                    Subtotal = v.Subtotal,
                    Iva = v.Iva,
                    Total = v.Total,
                    MetodoPago = p != null ? p.Metodo : "Efectivo",
                    ReferenciaPago = p != null ? p.Referencia : null,
                    Secuencial = c != null ? c.Secuencial : "001-002-000000001",
                    EstadoSri = c != null ? c.EstadoEnvio : "Pendiente"
                }
            ).Take(100).ToListAsync();

            var idsVentas = historial.Select(h => h.IdVenta).ToList();
            if (idsVentas.Count > 0)
            {
                var detallesPorVenta = await (
                    from v in _context.Ventas
                    where idsVentas.Contains(v.IdVenta)
                    from d in v.Detalles
                    join pr in _context.Productos on d.IdProducto equals pr.IdProducto
                    select new
                    {
                        v.IdVenta,
                        Item = new ItemVentaDetalleDto
                        {
                            CodigoBarras = pr.CodigoBarras,
                            Nombre = pr.Nombre,
                            Presentacion = pr.Presentacion,
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioUnitario,
                            SubtotalLinea = d.SubtotalLinea
                        }
                    }
                ).ToListAsync();

                var agrupados = detallesPorVenta
                    .GroupBy(x => x.IdVenta)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Item).ToList());

                foreach (var ventaDto in historial)
                {
                    if (agrupados.TryGetValue(ventaDto.IdVenta, out var listaItems))
                    {
                        ventaDto.Items = listaItems;
                    }
                }
            }

            return Ok(historial);
        }

        [HttpPost]
        public async Task<ActionResult<RegistrarVentaResponseDto>> RegistrarVenta([FromBody] RegistrarVentaRequestDto? request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return BadRequest("La venta debe contener al menos un producto.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                string loginBuscado = (request.UsuarioLogin ?? string.Empty).Trim().ToLowerInvariant();
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Login.ToLower() == loginBuscado && u.Activo);

                if (usuario == null)
                {
                    await transaction.RollbackAsync();
                    return BadRequest($"Usuario '{request.UsuarioLogin}' no encontrado o inactivo.");
                }

                string identBuscada = string.IsNullOrWhiteSpace(request.IdentificacionCliente)
                    ? "9999999999999"
                    : request.IdentificacionCliente.Trim();

                var cliente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.Identificacion == identBuscada && c.Activo)
                    ?? await _context.Clientes.FirstOrDefaultAsync(c => c.Identificacion == "9999999999999");

                if (cliente == null)
                {
                    await transaction.RollbackAsync();
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

                string tipoDocumento = string.Equals(request.TipoDocumento, "NotaVenta", StringComparison.OrdinalIgnoreCase)
                    ? "NotaVenta"
                    : "Factura";

                const decimal tasaIva = 15.00m;
                decimal subtotalVenta = 0m;
                decimal ivaVenta = 0m;
                var detallesEntidad = new List<DetalleVenta>();
                var itemsRespuesta = new List<ItemVentaDetalleDto>();

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
                    decimal ivaLinea = tipoDocumento == "Factura"
                        ? Math.Round(subtotalLinea * (tasaIva / 100m), 2)
                        : 0.00m;

                    subtotalVenta += subtotalLinea;
                    ivaVenta += ivaLinea;

                    detallesEntidad.Add(new DetalleVenta
                    {
                        IdProducto = producto.IdProducto,
                        Cantidad = item.Cantidad,
                        Descuento = descuentoLinea,
                        PrecioUnitario = precioUnit,
                        TasaIva = tipoDocumento == "Factura" ? tasaIva : 0.00m,
                        SubtotalLinea = subtotalLinea
                    });

                    itemsRespuesta.Add(new ItemVentaDetalleDto
                    {
                        CodigoBarras = producto.CodigoBarras,
                        Nombre = producto.Nombre,
                        Presentacion = producto.Presentacion,
                        Cantidad = item.Cantidad,
                        PrecioUnitario = precioUnit,
                        SubtotalLinea = subtotalLinea
                    });
                }

                decimal totalVenta = Math.Round(subtotalVenta + ivaVenta, 2);
                DateTime fechaEmision = DateTime.Now;

                var venta = new Venta
                {
                    IdUsuario = usuario.IdUsuario,
                    IdCliente = cliente.IdCliente,
                    IdTurno = turno.IdTurno,
                    FechaHora = fechaEmision,
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
                    Referencia = metodoPago == "Transferencia" ? request.ReferenciaPago?.Trim() : null
                };
                _context.Pagos.Add(pago);

                if (metodoPago == "Efectivo")
                {
                    turno.EfecEsperado += totalVenta;
                }

                string prefijoSerie = tipoDocumento == "NotaVenta" ? "001-003" : "001-002";
                string secuencial = await GenerarSiguienteSecuencialUnicoAsync(prefijoSerie);
                string secuencial9 = secuencial.Substring(secuencial.Length - 9);

                string tipoDocCodigo = tipoDocumento == "NotaVenta" ? "04" : "01";
                string claveAccesoUnica = await GenerarClaveAccesoUnicaAsync(fechaEmision, "1099999999001", secuencial9, tipoDocCodigo);
                string claveAccesoRespuesta = tipoDocumento == "Factura" ? claveAccesoUnica : string.Empty;

                var comprobante = new Comprobante
                {
                    IdVenta = venta.IdVenta,
                    Secuencial = secuencial,
                    ClaveAcceso = claveAccesoUnica,
                    EstadoEnvio = tipoDocumento == "Factura" ? "Pendiente" : "NotaVenta",
                    Intentos = 0
                };
                _context.Comprobantes.Add(comprobante);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new RegistrarVentaResponseDto
                {
                    IdVenta = venta.IdVenta,
                    TipoDocumento = tipoDocumento,
                    Secuencial = secuencial,
                    ClaveAccesoSri = claveAccesoRespuesta,
                    ClienteNombre = cliente.RazonSocial,
                    ClienteIdentificacion = cliente.Identificacion,
                    ClienteDireccion = cliente.Direccion ?? string.Empty,
                    ClienteTelefono = cliente.Telefono ?? string.Empty,
                    ClienteCelular = cliente.Celular ?? string.Empty,
                    ClienteCorreo = cliente.Correo ?? string.Empty,
                    MetodoPago = metodoPago,
                    ReferenciaPago = pago.Referencia,
                    Subtotal = subtotalVenta,
                    Iva = ivaVenta,
                    Total = totalVenta,
                    FechaHora = fechaEmision,
                    Items = itemsRespuesta
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

        private async Task<string> GenerarSiguienteSecuencialUnicoAsync(string prefijoSerie)
        {
            string prefijoConGuion = $"{prefijoSerie}-";
            var existentes = await _context.Comprobantes
                .Where(c => c.Secuencial.StartsWith(prefijoConGuion))
                .Select(c => c.Secuencial)
                .ToListAsync();

            int maxNumero = 0;
            foreach (var sec in existentes)
            {
                var partes = sec.Split('-');
                if (partes.Length == 3 && int.TryParse(partes[2], out int num) && num > maxNumero)
                {
                    maxNumero = num;
                }
            }

            int candidato = maxNumero + 1;
            string secuencialCandidato = $"{prefijoConGuion}{candidato:D9}";

            while (await _context.Comprobantes.AnyAsync(c => c.Secuencial == secuencialCandidato))
            {
                candidato++;
                secuencialCandidato = $"{prefijoConGuion}{candidato:D9}";
            }

            return secuencialCandidato;
        }

        private async Task<string> GenerarClaveAccesoUnicaAsync(DateTime fecha, string ruc, string secuencial9, string tipoComprobante)
        {
            int offset = 0;
            string clave = GenerarClaveAccesoSri(fecha, ruc, secuencial9, tipoComprobante, offset);

            while (await _context.Comprobantes.AnyAsync(c => c.ClaveAcceso == clave))
            {
                offset++;
                clave = GenerarClaveAccesoSri(fecha.AddMilliseconds(offset * 17), ruc, secuencial9, tipoComprobante, offset);
            }

            return clave;
        }

        private static string GenerarClaveAccesoSri(DateTime fecha, string ruc, string secuencial9, string tipoComprobante = "01", int offset = 0)
        {
            string fechaStr = fecha.ToString("ddMMyyyy");
            string ruc13 = ruc.PadLeft(13, '0').Substring(0, 13);
            string ambiente = "1";
            string serie = "001002";
            int semilla = ((fecha.Hour * 3600 + fecha.Minute * 60 + fecha.Second) * 100 + (fecha.Millisecond / 10) + offset) % 100000000;
            if (semilla < 0) semilla = -semilla;
            string codigoNumerico = semilla.ToString("D8");
            string tipoEmision = "1";

            string base48 = $"{fechaStr}{tipoComprobante}{ruc13}{ambiente}{serie}{secuencial9}{codigoNumerico}{tipoEmision}";
            int factor = 2;
            int suma = 0;

            for (int i = base48.Length - 1; i >= 0; i--)
            {
                suma += (base48[i] - '0') * factor;
                factor = factor == 7 ? 2 : factor + 1;
            }

            int residuo = suma % 11;
            int dv = 11 - residuo;
            if (dv == 11) dv = 0;
            else if (dv == 10) dv = 1;

            return $"{base48}{dv}";
        }
    }

    public class RegistrarVentaRequestDto
    {
        public string UsuarioLogin { get; set; } = "cflores_vendedor";
        public string? IdentificacionCliente { get; set; } = "9999999999999";
        public string Tarifa { get; set; } = "Minorista";
        public string TipoDocumento { get; set; } = "Factura";
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

    public class ItemVentaDetalleDto
    {
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Presentacion { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal SubtotalLinea { get; set; }
    }

    public class RegistrarVentaResponseDto
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
        public List<ItemVentaDetalleDto> Items { get; set; } = new();
    }

    public class VentaHistorialDto
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
        public List<ItemVentaDetalleDto> Items { get; set; } = new();
    }
}