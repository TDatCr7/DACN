using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Role", Schema = "dbo")]
    public class Role
    {
    [Key]
    [Column("Role_ID")]
    [StringLength(10)]
    public string RoleId { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }

    }
}
