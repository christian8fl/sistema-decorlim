using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decorlim.API.Models
{
    [Table("TURNOS_CAJA")]
    public class TurnoCaja
    {
        [Key]
        [Column("id_turno")]
        public int IdTurno { get; set; }

        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("apertura")]
        public DateTime Apertura { get; set; } = DateTime.Now;

        [Column("cierre")]
        public DateTime? Cierre { get; set; }

        [Column("fondo_inicial", TypeName = "decimal(12,2)")]
        public decimal FondoInicial { get; set; }

        [Column("efec_esperado", TypeName = "decimal(12,2)")]
        public decimal EfecEsperado { get; set; }

        [Column("efec_contado", TypeName = "decimal(12,2)")]
        public decimal? EfecContado { get; set; }

        [Column("diferencia", TypeName = "decimal(12,2)")]
        public decimal? Diferencia { get; set; }

        [Required]
        [Column("estado")]
        public string Estado { get; set; } = "Abierto";
    }
}