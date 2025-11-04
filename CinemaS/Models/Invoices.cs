using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Invoices", Schema = "dbo")]
    public class Invoices
    {
    [Key]
    [Column("Invoice_ID")]
    [StringLength(10)]
    public string InvoiceId { get; set; }
    [Column("Staff_ID")]
    [StringLength(10)]
    public string? StaffId { get; set; }
    [Column("Promotion_ID")]
    [StringLength(10)]
    public string? PromotionId { get; set; }
    [Required]
    [Column("Customer_ID")]
    [StringLength(10)]
    public string CustomerId { get; set; }
    [MaxLength(100)]
    public string? Email { get; set; }
    [MaxLength(30)]
    public string? PhoneNumber { get; set; }
    public byte? Status { get; set; }
    [Column("Total_Ticket")]
    public int? TotalTicket { get; set; }
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }
    [Column("Total_Price", TypeName="money")]
    public decimal? TotalPrice { get; set; }
    [Column("Created_At")]
    public DateTime? CreatedAt { get; set; }
    [Column("Updated_At")]
    public DateTime? UpdatedAt { get; set; }
    [Column("Payment_Method_ID")]
    [StringLength(10)]
    public string? PaymentMethodId { get; set; }

    }
}
