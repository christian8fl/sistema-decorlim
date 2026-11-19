using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decorlim.API.Models
{
    [Table("PRODUCTOS")]
    public class Producto
    {
        [Key]
        [Column("id_producto")]
        public int IdProducto { get; set; }

        [Required]
        [Column("codigo_barras")]
        public string CodigoBarras { get; set; } = string.Empty;

        [Required]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [Column("marca")]
        public string Marca { get; set; } = string.Empty;

        [Column("color")]
        public string? Color { get; set; }

        [Required]
        [Column("acabado")]
        public string Acabado { get; set; } = string.Empty;

        [Required]
        [Column("presentacion")]
        public string Presentacion { get; set; } = string.Empty;

        [Column("costo", TypeName = "decimal(12,2)")]
        public decimal Costo { get; set; }

        [Column("precio_minorista", TypeName = "decimal(12,2)")]
        public decimal PrecioMinorista { get; set; }

        [Column("precio_mayorista", TypeName = "decimal(12,2)")]
        public decimal PrecioMayorista { get; set; }

        [Column("stock_actual")]
        public int StockActual { get; set; }

        [Column("stock_minimo")]
        public int StockMinimo { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;
    }
}