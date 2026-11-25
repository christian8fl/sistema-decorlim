using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decorlim.API.Models
{
    [Table("CLIENTES")]
    public class Cliente
    {
        [Key]
        [Column("id_cliente")]
        public int IdCliente { get; set; }

        [Required]
        [Column("tipo_identificacion")]
        public string TipoIdentificacion { get; set; } = "CEDULA";

        [Required]
        [Column("identificacion")]
        public string Identificacion { get; set; } = string.Empty;

        [Required]
        [Column("razon_social")]
        public string RazonSocial { get; set; } = string.Empty;

        [Column("correo")]
        public string? Correo { get; set; }

        [Column("direccion")]
        public string? Direccion { get; set; }

        [Column("telefono")]
        public string? Telefono { get; set; }

        [Column("celular")]
        public string? Celular { get; set; }

        [Required]
        [Column("tipo_cliente")]
        public string TipoCliente { get; set; } = "General";

        [Column("activo")]
        public bool Activo { get; set; } = true;
    }
}