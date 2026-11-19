using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decorlim.API.Models
{
    [Table("DETALLE_VENTAS")]
    public class DetalleVenta
    {
        [Key]
        [Column("id_detalle")]
        public int IdDetalle { get; set; }

        [Column("id_venta")]
        public int IdVenta { get; set; }

        [Column("id_producto")]
        public int IdProducto { get; set; }

        [Column("cantidad")]
        public int Cantidad { get; set; }

        [Column("descuento", TypeName = "decimal(12,2)")]
        public decimal Descuento { get; set; }

        [Column("precio_unitario", TypeName = "decimal(12,2)")]
        public decimal PrecioUnitario { get; set; }

        [Column("tasa_iva", TypeName = "decimal(5,2)")]
        public decimal TasaIva { get; set; } = 15.00m;

        [Column("subtotal_linea", TypeName = "decimal(12,2)")]
        public decimal SubtotalLinea { get; set; }
    }
}