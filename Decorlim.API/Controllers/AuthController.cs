using System;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using Decorlim.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Decorlim.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Debe ingresar su usuario y contraseña.");
            }

            string loginNormalizado = request.Login.Trim();
            string passwordIngresada = request.Password;

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Login == loginNormalizado);

            if (usuario == null)
            {
                return Unauthorized("Usuario no encontrado.");
            }

            if (!usuario.Activo)
            {
                return Unauthorized("El usuario se encuentra inactivo.");
            }

            bool contraseñaValida = false;
            bool requiereMigracionAHash = false;

            if (!string.IsNullOrWhiteSpace(usuario.PasswordHash) && EsHashBCrypt(usuario.PasswordHash))
            {
                try
                {
                    contraseñaValida = BCrypt.Net.BCrypt.Verify(passwordIngresada, usuario.PasswordHash);
                }
                catch
                {
                    contraseñaValida = false;
                }
            }
            else
            {
                // Compatibilidad temporal con HASH_TEMPORAL_123 o texto plano previo en DB_DECORLIM
                contraseñaValida = usuario.PasswordHash == passwordIngresada;
                requiereMigracionAHash = contraseñaValida;
            }

            if (!contraseñaValida)
            {
                return Unauthorized("Contraseña incorrecta.");
            }

            if (requiereMigracionAHash)
            {
                usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordIngresada, workFactor: 11);
                await _context.SaveChangesAsync();
            }

            return Ok(new LoginResponseDto
            {
                IdUsuario = usuario.IdUsuario,
                Login = usuario.Login,
                NombreCompleto = usuario.NombreCompleto,
                Rol = usuario.Rol
            });
        }

        private static bool EsHashBCrypt(string valor)
        {
            return valor.StartsWith("$2a$", StringComparison.Ordinal)
                || valor.StartsWith("$2b$", StringComparison.Ordinal)
                || valor.StartsWith("$2y$", StringComparison.Ordinal);
        }
    }

    public class LoginRequestDto
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponseDto
    {
        public int IdUsuario { get; set; }
        public string Login { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
    }
}