using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decorlim.API.Models
{
    [Table("VENTAS")]
    public class Venta
    {
        [Key]
        [Column("id_venta")]
        public int IdVenta { get; set; }

        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("id_cliente")]
        public int IdCliente { get; set; }

        [Column("id_turno")]
        public int IdTurno { get; set; }

        [Column("fecha_hora")]
        public DateTime FechaHora { get; set; } = DateTime.Now;

        [Required]
        [Column("tarifa")]
        public string Tarifa { get; set; } = "Minorista";

        [Column("subtotal", TypeName = "decimal(12,2)")]
        public decimal Subtotal { get; set; }

        [Column("iva", TypeName = "decimal(12,2)")]
        public decimal Iva { get; set; }

        [Column("total", TypeName = "decimal(12,2)")]
        public decimal Total { get; set; }

        [Required]
        [Column("estado")]
        public string Estado { get; set; } = "Completada";

        public List<DetalleVenta> Detalles { get; set; } = new();
        public List<Pago> Pagos { get; set; } = new();
    }
}