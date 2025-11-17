using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Payment_Transactions", Schema = "dbo")]
    public class PaymentTransactions
    {
    [Key]
    [Column("Payment_Transaction_ID")]
    [StringLength(20)]
    public string PaymentTransactionId { get; set; }
    [Required]
    [Column("Invoice_ID")]
    [MaxLength(50)]
    public string InvoiceId { get; set; }
    [Required]
    [Column("Payment_Method_ID")]
    [StringLength(10)]
    public string PaymentMethodId { get; set; }
    [Column(TypeName="money")]
    public decimal? Amount { get; set; }
    [StringLength(3)]
    public string? Currency { get; set; }
    [Column("Status")]
    public byte? Status { get; set; }
    [Column("Provider_Txn_ID")]
    [MaxLength(100)]
    public string? ProviderTxnId { get; set; }
    [Column("Provider_Order_No")]
    [MaxLength(100)]
    public string? ProviderOrderNo { get; set; }
    [MaxLength(300)]
    public string? Description { get; set; }
    [Column("Failure_Reason")]
    [MaxLength(300)]
    public string? FailureReason { get; set; }
    [Column("Created_At")]
    public DateTime? CreatedAt { get; set; }
    [Column("Updated_At")]
    public DateTime? UpdatedAt { get; set; }
    [Column("Paid_At")]
    public DateTime? PaidAt { get; set; }
    [Column("Refunded_At")]
    public DateTime? RefundedAt { get; set; }

    }
}
