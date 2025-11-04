using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Snack_Types", Schema = "dbo")]
    public class SnackTypes
    {
    [Key]
    [Column("Snack_Type_ID")]
    [StringLength(10)]
    public string SnackTypeId { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(300)]
    public string? Description { get; set; }

    }
}
