using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Genres", Schema = "dbo")]
    public class Genres
    {
    [Key]
    [Column("Genres_ID")]
    [StringLength(10)]
    public string GenresId { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(250)]
    public string? Description { get; set; }

    }
}
