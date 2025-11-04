using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Movie_Theaters", Schema = "dbo")]
    public class MovieTheaters
    {
    [Key]
    [Column("Movie_Theater_ID")]
    [StringLength(10)]
    public string MovieTheaterId { get; set; }
    [MaxLength(500)]
    public string? Name { get; set; }
    [MaxLength(300)]
    public string? Address { get; set; }
    [MaxLength(30)]
    public string? Hotline { get; set; }
    public byte? Status { get; set; }
    [Column("I_frame_Code")]
    [MaxLength(300)]
    public string? IFrameCode { get; set; }
    [Required]
    [Column("Province_ID")]
    [StringLength(10)]
    public string ProvinceId { get; set; }

    }
}
