using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Payment_Methods", Schema = "dbo")]
    public class PaymentMethods
    {
    [Key]
    [Column("Payment_Method_ID")]
    [StringLength(10)]
    public string PaymentMethodId { get; set; }
    [Required]
    [MaxLength(50)]
    public string Code { get; set; }
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }
    [Column("Type")]
    public byte? Type { get; set; }
    [MaxLength(100)]
    public string? Provider { get; set; }
    public bool IsActive { get; set; }
    [Column("Created_At")]
    public DateTime? CreatedAt { get; set; }
    [Column("Updated_At")]
    public DateTime? UpdatedAt { get; set; }

    }
}
