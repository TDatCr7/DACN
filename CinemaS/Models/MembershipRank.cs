using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Membership_Rank", Schema = "dbo")]
    public class MembershipRank
    {
    [Key]
    [Column("Membership_Rank_ID")]
    [StringLength(10)]
    public string MembershipRankId { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }
    public int? RequirePoint { get; set; }
    public int? PointReturnTicket { get; set; }
    public int? PointReturnCombo { get; set; }
    public int? PriorityLevel { get; set; }
    [Column("Created_At")]
    public DateTime? CreatedAt { get; set; }
    [Column("Updated_At")]
    public DateTime? UpdatedAt { get; set; }

    }
}
