using Decorlim.API.Data;
using Decorlim.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Decorlim.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductosController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/productos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
        {
            return await _context.Productos.Where(p => p.Activo).ToListAsync();
        }

        // GET: api/productos/7861004520194 (Búsqueda rápida por Código de Barras / Escáner USB)
        [HttpGet("codigo/{codigo}")]
        public async Task<ActionResult<Producto>> GetProductoPorCodigo(string codigo)
        {
            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.CodigoBarras == codigo && p.Activo);

            if (producto == null)
            {
                return NotFound(new { mensaje = "Producto no encontrado con ese código de barras." });
            }

            return producto;
        }
    }
}