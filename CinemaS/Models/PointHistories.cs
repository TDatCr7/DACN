using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Point_Histories", Schema = "dbo")]
    public class PointHistories
    {
    [Key]
    [Column("Point_History_ID")]
    [StringLength(10)]
    public string PointHistoryId { get; set; }
    [Required]
    [Column("User_ID")]
    [StringLength(10)]
    public string UserId { get; set; }
    [Required]
    [Column("Invoice_ID")]
    [StringLength(10)]
    public string InvoiceId { get; set; }
    [Column("Change_Amount", TypeName="money")]
    public decimal? ChangeAmount { get; set; }
    [MaxLength(300)]
    public string? Reason { get; set; }
    [Column("Created_At")]
    public DateTime? CreatedAt { get; set; }
    [Column("Updated_At")]
    public DateTime? UpdatedAt { get; set; }

    }
}
