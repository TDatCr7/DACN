using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Users", Schema = "dbo")]
    public class Users
    {
    [Key]
    [Column("User_ID")]
    [StringLength(10)]
    public string UserId { get; set; }
    [Required]
    [Column("Membership_Rank_ID")]
    [StringLength(10)]
    public string MembershipRankId { get; set; }
    [MaxLength(100)]
    public string? Email { get; set; }
    [MaxLength(100)]
    public string? Password { get; set; }
    [Column("Full_Name")]
    [MaxLength(100)]
    public string? FullName { get; set; }
    [MaxLength(30)]
    public string? PhoneNumber { get; set; }
    [MaxLength(300)]
    public string? Address { get; set; }
    [Column("Date_Of_Birth")]
    public DateTime? DateOfBirth { get; set; }
    public byte? Gender { get; set; }
    [Column("Save_Point")]
    public int? SavePoint { get; set; }
    [MaxLength(300)]
    public string? Facebook { get; set; }
    [MaxLength(300)]
    public string? Google { get; set; }
    public byte? Status { get; set; }
    [Column("Type")]
    [MaxLength(100)]
    public string? Type { get; set; }
    public bool? IsActive { get; set; }
    [Column("Created_At")]
    public DateTime? CreatedAt { get; set; }
    [Column("Updated_At")]
    public DateTime? UpdatedAt { get; set; }

    }
}
