using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Provinces", Schema = "dbo")]
    public class Provinces
    {
    [Key]
    [Column("Province_ID")]
    [StringLength(10)]
    public string ProvinceId { get; set; }
    [MaxLength(150)]
    public string? Name { get; set; }
    [Column("Created_At")]
    public DateTime? CreatedAt { get; set; }
    [Column("Updated_At")]
    public DateTime? UpdatedAt { get; set; }

    }
}
