using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("User_Role", Schema = "dbo")]
    public class UserRole
    {
    [Column("Role_ID")]
    [StringLength(10)]
    public string RoleId { get; set; }
    [Column("User_ID")]
    [StringLength(10)]
    public string UserId { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; }
    // Composite Key (Role_ID, User_ID) will be configured in DbContext

    }
}
