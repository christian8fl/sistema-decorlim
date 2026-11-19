using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decorlim.API.Models
{
    [Table("USUARIOS")]
    public class Usuario
    {
        [Key]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Required]
        [Column("nombre_completo")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required]
        [Column("login")]
        public string Login { get; set; } = string.Empty;

        [Required]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [Column("rol")]
        public string Rol { get; set; } = string.Empty;

        [Column("activo")]
        public bool Activo { get; set; } = true;
    }
}