using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Cinema_Theaters", Schema = "dbo")]
    public class CinemaTheaters
    {
    [Key]
    [Column("Cinema_Theater_ID")]
    [StringLength(10)]
    public string CinemaTheaterId { get; set; }
    [Required]
    [Column("Cinema_Type_ID")]
    [StringLength(10)]
    public string CinemaTypeId { get; set; }
    [Required]
    [Column("Movie_Theater_ID")]
    [StringLength(10)]
    public string MovieTheaterId { get; set; }
    [MaxLength(300)]
    public string? Name { get; set; }
    [Column("Num_Of_Rows")]
    public int? NumOfRows { get; set; }
    [Column("Num_Of_Columns")]
    public int? NumOfColumns { get; set; }
    [Column("Regular_Seat_Row")]
    public int? RegularSeatRow { get; set; }
    [Column("Double_Seat_Row")]
    public int? DoubleSeatRow { get; set; }
    [Column("VIP_Seat_Row")]
    public int? VIPSeatRow { get; set; }
    public byte? Status { get; set; }

    }
}
