using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Cinema_Types", Schema = "dbo")]
    public class CinemaTypes
    {
    [Key]
    [Column("Cinema_Type_ID")]
    [StringLength(10)]
    public string CinemaTypeId { get; set; }
    [MaxLength(300)]
    public string? Name { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    [MaxLength(100)]
    public string? Code { get; set; }

    }
}
