using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Seat_Types", Schema = "dbo")]
    public class SeatTypes
    {
    [Key]
    [Column("Seat_Type_ID")]
    [StringLength(10)]
    public string SeatTypeId { get; set; }
    [MaxLength(200)]
    public string? Name { get; set; }
    [Column(TypeName="money")]
    public decimal? Price { get; set; }

    }
}
