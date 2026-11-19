using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decorlim.API.Models
{
    [Table("PAGOS")]
    public class Pago
    {
        [Key]
        [Column("id_pago")]
        public int IdPago { get; set; }

        [Column("id_venta")]
        public int IdVenta { get; set; }

        [Required]
        [Column("metodo")]
        public string Metodo { get; set; } = "Efectivo";

        [Column("monto", TypeName = "decimal(12,2)")]
        public decimal Monto { get; set; }

        [Column("referencia")]
        public string? Referencia { get; set; }
    }
}