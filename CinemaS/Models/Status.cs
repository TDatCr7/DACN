using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Status", Schema = "dbo")]
    public class Status
    {
    [Key]
    [Column("Status_ID")]
    [StringLength(10)]
    public string StatusId { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }

    }
}
