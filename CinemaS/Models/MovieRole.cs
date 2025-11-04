using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Movie_Role", Schema = "dbo")]
    public class MovieRole
    {
    [Key]
    [Column("Movie_Role_ID")]
    [StringLength(10)]
    public string MovieRoleId { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }

    }
}
