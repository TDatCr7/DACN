using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Detail_Booking_Snacks", Schema = "dbo")]
    public class DetailBookingSnacks
    {
    [Column("Snack_ID")]
    [StringLength(10)]
    public string SnackId { get; set; }
    [Column("Invoice_ID")]
    [MaxLength(50)]
    public string InvoiceId { get; set; }
    [Column("Detail_Booking_Snack_ID")]
    [StringLength(10)]
    public string DetailBookingSnackId { get; set; }
    [Column("Total_Snack")]
    public int? TotalSnack { get; set; }
    [Column("Total_Price", TypeName="money")]
    public decimal? TotalPrice { get; set; }
    
    }
}
