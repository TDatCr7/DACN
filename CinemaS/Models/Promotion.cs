using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Promotion", Schema = "dbo")]
    public class Promotion
    {
    [Key]
    [Column("Promotion_ID")]
    [StringLength(10)]
    public string PromotionId { get; set; }
    [Required]
    [Column("User_ID")]
    [StringLength(10)]
    public string UserId { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(1000)]
    public string? Content { get; set; }
    [MaxLength(100)]
    public string? Code { get; set; }
    [Column("Start_Day")]
    public DateTime? StartDay { get; set; }
    [Column("End_Day")]
    public DateTime? EndDay { get; set; }
    public double? Discount { get; set; }
    public bool? Status { get; set; }

    }
}
