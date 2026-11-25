using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Decorlim.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Decorlim.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("resumen")]
        public async Task<ActionResult<DashboardResumenDto>> GetResumen()
        {
            DateTime inicioHoy = DateTime.Today;
            DateTime finHoy = inicioHoy.AddDays(1);

            var ventasHoyQuery = _context.Ventas.AsNoTracking()
                .Where(v => v.FechaHora >= inicioHoy && v.FechaHora < finHoy && v.Estado == "Completada");

            int cantidadVentasHoy = await ventasHoyQuery.CountAsync();
            decimal totalVentasHoy = cantidadVentasHoy > 0
                ? await ventasHoyQuery.SumAsync(v => v.Total)
                : 0m;

            var idsVentasHoy = await ventasHoyQuery.Select(v => v.IdVenta).ToListAsync();

            decimal totalEfectivoHoy = 0m;
            decimal totalTransferenciaHoy = 0m;

            if (idsVentasHoy.Count > 0)
            {
                var pagosHoy = await _context.Pagos.AsNoTracking()
                    .Where(p => idsVentasHoy.Contains(p.IdVenta))
                    .GroupBy(p => p.Metodo)
                    .Select(g => new { Metodo = g.Key, Total = g.Sum(x => x.Monto) })
                    .ToListAsync();

                totalEfectivoHoy = pagosHoy.FirstOrDefault(x => x.Metodo == "Efectivo")?.Total ?? 0m;
                totalTransferenciaHoy = pagosHoy.FirstOrDefault(x => x.Metodo == "Transferencia")?.Total ?? 0m;
            }

            var stockBajo = await _context.Productos.AsNoTracking()
                .Where(p => p.Activo && p.StockActual <= 5)
                .OrderBy(p => p.StockActual)
                .Select(p => new StockBajoDto
                {
                    IdProducto = p.IdProducto,
                    CodigoBarras = p.CodigoBarras,
                    Nombre = p.Nombre,
                    Presentacion = p.Presentacion,
                    StockActual = p.StockActual,
                    PrecioMinorista = p.PrecioMinorista
                })
                .ToListAsync();

            var comprobantesRecientes = await (
                from c in _context.Comprobantes.AsNoTracking()
                join v in _context.Ventas.AsNoTracking() on c.IdVenta equals v.IdVenta
                join cl in _context.Clientes.AsNoTracking() on v.IdCliente equals cl.IdCliente
                orderby v.FechaHora descending
                select new ComprobanteResumenDto
                {
                    IdComprobante = c.IdComprobante,
                    IdVenta = v.IdVenta,
                    Secuencial = c.Secuencial,
                    FechaHora = v.FechaHora,
                    Cliente = cl.RazonSocial,
                    Identificacion = cl.Identificacion,
                    Total = v.Total,
                    EstadoEnvio = c.EstadoEnvio
                }
            ).Take(30).ToListAsync();

            return Ok(new DashboardResumenDto
            {
                TotalVentasHoy = totalVentasHoy,
                CantidadVentasHoy = cantidadVentasHoy,
                TicketPromedio = cantidadVentasHoy > 0 ? Math.Round(totalVentasHoy / cantidadVentasHoy, 2) : 0m,
                TotalEfectivoHoy = totalEfectivoHoy,
                TotalTransferenciaHoy = totalTransferenciaHoy,
                ProductosStockBajo = stockBajo,
                ComprobantesRecientes = comprobantesRecientes
            });
        }
    }

    public class DashboardResumenDto
    {
        public decimal TotalVentasHoy { get; set; }
        public int CantidadVentasHoy { get; set; }
        public decimal TicketPromedio { get; set; }
        public decimal TotalEfectivoHoy { get; set; }
        public decimal TotalTransferenciaHoy { get; set; }
        public List<StockBajoDto> ProductosStockBajo { get; set; } = new();
        public List<ComprobanteResumenDto> ComprobantesRecientes { get; set; } = new();
    }

    public class StockBajoDto
    {
        public int IdProducto { get; set; }
        public string CodigoBarras { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Presentacion { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public decimal PrecioMinorista { get; set; }
    }

    public class ComprobanteResumenDto
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
}