using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Permission", Schema = "dbo")]
    public class Permission
    {
    [Key]
    [Column("Permission_ID")]
    [StringLength(10)]
    public string PermissionId { get; set; }
    [MaxLength(100)]
    public string? Title { get; set; }
    [Column("Description")]
    [MaxLength(500)]
    public string? Description { get; set; }
    [MaxLength(100)]
    public string? Method { get; set; }
    [MaxLength(500)]
    public string? Url { get; set; }
    [Column("Created_At")]
    public DateTime? CreatedAt { get; set; }
    [Column("Updated_At")]
    public DateTime? UpdatedAt { get; set; }

    }
}
