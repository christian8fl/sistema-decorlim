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
    public class ClientesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClientesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes([FromQuery] bool incluirInactivos = false)
        {
            var query = _context.Clientes.AsQueryable();
            if (!incluirInactivos)
            {
                query = query.Where(c => c.Activo);
            }

            return Ok(await query.OrderBy(c => c.RazonSocial).ToListAsync());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Cliente>> GetClienteById(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
            {
                return NotFound("Cliente no encontrado.");
            }

            return Ok(cliente);
        }

        [HttpPost]
        public async Task<ActionResult<Cliente>> CrearCliente([FromBody] Cliente cliente)
        {
            string identificacion = (cliente.Identificacion ?? string.Empty).Trim();
            string razonSocial = (cliente.RazonSocial ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(identificacion) || string.IsNullOrWhiteSpace(razonSocial))
            {
                return BadRequest("La identificación y la razón social / nombre completo son obligatorios.");
            }

            bool existe = await _context.Clientes.AnyAsync(c => c.Identificacion == identificacion);
            if (existe)
            {
                return BadRequest($"Ya existe un cliente registrado con la identificación '{identificacion}'.");
            }

            string tipoIdent = !string.IsNullOrWhiteSpace(cliente.TipoIdentificacion)
                ? cliente.TipoIdentificacion.Trim().ToUpperInvariant()
                : (identificacion == "9999999999999" ? "CONSUMIDOR_FINAL"
                    : identificacion.Length == 13 ? "RUC"
                    : identificacion.Length == 10 ? "CEDULA" : "PASAPORTE");

            cliente.TipoIdentificacion = tipoIdent;
            cliente.Identificacion = identificacion;
            cliente.RazonSocial = razonSocial;
            cliente.Correo = (cliente.Correo ?? string.Empty).Trim();
            cliente.Direccion = (cliente.Direccion ?? string.Empty).Trim();
            cliente.Telefono = (cliente.Telefono ?? string.Empty).Trim();
            cliente.Celular = (cliente.Celular ?? string.Empty).Trim();
            cliente.TipoCliente = string.IsNullOrWhiteSpace(cliente.TipoCliente)
                ? "General"
                : cliente.TipoCliente.Trim();
            cliente.Activo = true;

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClienteById), new { id = cliente.IdCliente }, cliente);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Cliente>> ActualizarCliente(int id, [FromBody] Cliente actualizado)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
            {
                return NotFound("Cliente no encontrado.");
            }

            string nuevaIdent = (actualizado.Identificacion ?? cliente.Identificacion).Trim();
            bool duplicado = await _context.Clientes
                .AnyAsync(c => c.Identificacion == nuevaIdent && c.IdCliente != id);

            if (duplicado)
            {
                return BadRequest($"La identificación '{nuevaIdent}' ya pertenece a otro cliente.");
            }

            cliente.TipoIdentificacion = !string.IsNullOrWhiteSpace(actualizado.TipoIdentificacion)
                ? actualizado.TipoIdentificacion.Trim().ToUpperInvariant()
                : cliente.TipoIdentificacion;
            cliente.Identificacion = nuevaIdent;
            cliente.RazonSocial = !string.IsNullOrWhiteSpace(actualizado.RazonSocial)
                ? actualizado.RazonSocial.Trim()
                : cliente.RazonSocial;
            cliente.Correo = (actualizado.Correo ?? string.Empty).Trim();
            cliente.Direccion = (actualizado.Direccion ?? string.Empty).Trim();
            cliente.Telefono = (actualizado.Telefono ?? string.Empty).Trim();
            cliente.Celular = (actualizado.Celular ?? string.Empty).Trim();
            cliente.TipoCliente = !string.IsNullOrWhiteSpace(actualizado.TipoCliente)
                ? actualizado.TipoCliente.Trim()
                : cliente.TipoCliente;
            cliente.Activo = actualizado.Activo;

            await _context.SaveChangesAsync();
            return Ok(cliente);
        }
    }
}