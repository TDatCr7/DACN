using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Role_Permission", Schema = "dbo")]
    public class RolePermission
    {
    [Column("Permission_ID")]
    [StringLength(10)]
    public string PermissionId { get; set; }
    [Column("Role_ID")]
    [StringLength(10)]
    public string RoleId { get; set; }
    // Composite Key (Permission_ID, Role_ID) will be configured in DbContext

    }
}
