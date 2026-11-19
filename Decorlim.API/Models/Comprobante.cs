using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decorlim.API.Models
{
    [Table("COMPROBANTES")]
    public class Comprobante
    {
        [Key]
        [Column("id_comprobante")]
        public int IdComprobante { get; set; }

        [Column("id_venta")]
        public int IdVenta { get; set; }

        [Column("clave_acceso")]
        public string? ClaveAcceso { get; set; }

        [Required]
        [Column("secuencial")]
        public string Secuencial { get; set; } = string.Empty;

        [Required]
        [Column("estado_envio")]
        public string EstadoEnvio { get; set; } = "Pendiente";

        [Column("xml_local")]
        public string? XmlLocal { get; set; }

        [Column("intentos")]
        public int Intentos { get; set; }

        [Column("fecha_aut")]
        public DateTime? FechaAut { get; set; }
    }
}