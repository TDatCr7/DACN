using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Seats", Schema = "dbo")]
    public class Seats
    {
        [Key]
        [Column("Seat_ID")]
        [StringLength(10)]
        public string SeatId { get; set; }
        [Required]
        [Column("Seat_Type_ID")]
        [StringLength(10)]
        public string SeatTypeId { get; set; }
        [Required]
        [Column("Cinema_Theater_ID")]
        [StringLength(10)]
        public string CinemaTheaterId { get; set; }
        [MaxLength(1)]
        public string? RowIndex { get; set; }
        public int? ColumnIndex { get; set; }
        [MaxLength(4)]
        public string? Label { get; set; }

        [Column("Is_Active")]
        public bool IsActive { get; set; } = true;

    }
}
