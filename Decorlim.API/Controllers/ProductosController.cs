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
    public class ProductosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductosController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos([FromQuery] bool incluirInactivos = false)
        {
            var query = _context.Productos.AsQueryable();
            if (!incluirInactivos)
            {
                query = query.Where(p => p.Activo);
            }

            var lista = await query
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return Ok(lista);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Producto>> GetProductoById(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound($"Producto con ID {id} no encontrado.");
            }

            return Ok(producto);
        }

        [HttpGet("siguiente-codigo")]
        public async Task<ActionResult<object>> GetSiguienteCodigo()
        {
            int siguiente = await _context.Productos.CountAsync() + 1;
            string codigo = $"DEC-{siguiente:D5}";
            while (await _context.Productos.AnyAsync(p => p.CodigoBarras == codigo))
            {
                siguiente++;
                codigo = $"DEC-{siguiente:D5}";
            }

            return Ok(new { CodigoSugerido = codigo });
        }

        [HttpPost]
        public async Task<ActionResult<Producto>> CrearProducto([FromBody] GuardarProductoRequestDto? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Nombre))
            {
                return BadRequest("El nombre del producto es obligatorio.");
            }

            string codigo = (request.CodigoBarras ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                int siguiente = await _context.Productos.CountAsync() + 1;
                codigo = $"DEC-{siguiente:D5}";
                while (await _context.Productos.AnyAsync(p => p.CodigoBarras == codigo))
                {
                    siguiente++;
                    codigo = $"DEC-{siguiente:D5}";
                }
            }

            bool existe = await _context.Productos.AnyAsync(p => p.CodigoBarras == codigo);
            if (existe)
            {
                return BadRequest($"Ya existe un producto registrado con el código '{codigo}'.");
            }

            var nuevo = new Producto
            {
                CodigoBarras = codigo,
                Nombre = request.Nombre.Trim(),
                Presentacion = string.IsNullOrWhiteSpace(request.Presentacion) ? "Unidad" : request.Presentacion.Trim(),
                Marca = string.IsNullOrWhiteSpace(request.Marca) ? "DECORLIM" : request.Marca.Trim(),
                Acabado = string.IsNullOrWhiteSpace(request.Acabado) ? "Estándar" : request.Acabado.Trim(),
                PrecioMinorista = request.PrecioMinorista,
                PrecioMayorista = request.PrecioMayorista,
                StockActual = request.StockActual,
                Activo = true
            };

            _context.Productos.Add(nuevo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProductoById), new { id = nuevo.IdProducto }, nuevo);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Producto>> ActualizarProducto(int id, [FromBody] GuardarProductoRequestDto? actualizado)
        {
            if (actualizado == null || string.IsNullOrWhiteSpace(actualizado.Nombre))
            {
                return BadRequest("El nombre del producto es obligatorio.");
            }

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound($"Producto con ID {id} no encontrado.");
            }

            string nuevoCodigo = string.IsNullOrWhiteSpace(actualizado.CodigoBarras)
                ? producto.CodigoBarras
                : actualizado.CodigoBarras.Trim();

            bool duplicado = await _context.Productos
                .AnyAsync(p => p.CodigoBarras == nuevoCodigo && p.IdProducto != id);

            if (duplicado)
            {
                return BadRequest($"El código '{nuevoCodigo}' ya pertenece a otro producto.");
            }

            producto.CodigoBarras = nuevoCodigo;
            producto.Nombre = actualizado.Nombre.Trim();
            producto.Presentacion = string.IsNullOrWhiteSpace(actualizado.Presentacion)
                ? producto.Presentacion
                : actualizado.Presentacion.Trim();

            if (!string.IsNullOrWhiteSpace(actualizado.Marca))
                producto.Marca = actualizado.Marca.Trim();
            else if (string.IsNullOrWhiteSpace(producto.Marca))
                producto.Marca = "DECORLIM";

            if (!string.IsNullOrWhiteSpace(actualizado.Acabado))
                producto.Acabado = actualizado.Acabado.Trim();
            else if (string.IsNullOrWhiteSpace(producto.Acabado))
                producto.Acabado = "Estándar";

            producto.PrecioMinorista = actualizado.PrecioMinorista;
            producto.PrecioMayorista = actualizado.PrecioMayorista;
            producto.StockActual = actualizado.StockActual;
            producto.Activo = actualizado.Activo;

            await _context.SaveChangesAsync();
            return Ok(producto);
        }
    }

    public class GuardarProductoRequestDto
    {
        public int IdProducto { get; set; }
        public string? CodigoBarras { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Presentacion { get; set; } = "Unidad";
        public string? Marca { get; set; } = "DECORLIM";
        public string? Acabado { get; set; } = "Estándar";
        public decimal PrecioMinorista { get; set; }
        public decimal PrecioMayorista { get; set; }
        public int StockActual { get; set; }
        public bool Activo { get; set; } = true;
    }
}