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
    public class UsuariosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsuariosController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UsuarioDto>>> GetUsuarios()
        {
            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .OrderBy(u => u.NombreCompleto)
                .Select(u => new UsuarioDto
                {
                    IdUsuario = u.IdUsuario,
                    Login = u.Login,
                    NombreCompleto = u.NombreCompleto,
                    Rol = u.Rol,
                    Activo = u.Activo
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        [HttpPost]
        public async Task<ActionResult<UsuarioDto>> CrearUsuario([FromBody] CrearUsuarioRequestDto? request)
        {
            if (request == null)
            {
                return BadRequest("Datos de usuario inválidos.");
            }

            string login = (request.Login ?? string.Empty).Trim();
            string nombre = (request.NombreCompleto ?? string.Empty).Trim();
            string password = request.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(password))
            {
                return BadRequest("Debe completar usuario, nombre completo y contraseña.");
            }

            bool existe = await _context.Usuarios.AnyAsync(u => u.Login == login);
            if (existe)
            {
                return BadRequest($"El usuario '{login}' ya existe.");
            }

            string rolEntrada = (request.Rol ?? string.Empty).Trim();
            string rolNormalizado = (string.Equals(rolEntrada, "Administrador", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(rolEntrada, "Admin", StringComparison.OrdinalIgnoreCase))
                ? "Admin"
                : "Vendedor";

            var usuario = new Usuario
            {
                Login = login,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                NombreCompleto = nombre,
                Rol = rolNormalizado,
                Activo = true
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return Ok(new UsuarioDto
            {
                IdUsuario = usuario.IdUsuario,
                Login = usuario.Login,
                NombreCompleto = usuario.NombreCompleto,
                Rol = usuario.Rol,
                Activo = usuario.Activo
            });
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<UsuarioDto>> ActualizarUsuario(int id, [FromBody] ActualizarUsuarioRequestDto? request)
        {
            if (request == null)
            {
                return BadRequest("Datos de actualización inválidos.");
            }

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
            {
                return NotFound("Usuario no encontrado.");
            }

            string nombre = (request.NombreCompleto ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(nombre))
            {
                usuario.NombreCompleto = nombre;
            }

            string rolEntrada = (request.Rol ?? string.Empty).Trim();
            usuario.Rol = (string.Equals(rolEntrada, "Administrador", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(rolEntrada, "Admin", StringComparison.OrdinalIgnoreCase))
                ? "Admin"
                : "Vendedor";
            usuario.Activo = request.Activo;

            if (!string.IsNullOrWhiteSpace(request.NuevaPassword))
            {
                usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NuevaPassword);
            }

            await _context.SaveChangesAsync();

            return Ok(new UsuarioDto
            {
                IdUsuario = usuario.IdUsuario,
                Login = usuario.Login,
                NombreCompleto = usuario.NombreCompleto,
                Rol = usuario.Rol,
                Activo = usuario.Activo
            });
        }
    }

    public class UsuarioDto
    {
        public int IdUsuario { get; set; }
        public string Login { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }

    public class CrearUsuarioRequestDto
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = "Vendedor";
    }

    public class ActualizarUsuarioRequestDto
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = "Vendedor";
        public bool Activo { get; set; } = true;
        public string? NuevaPassword { get; set; }
    }
}