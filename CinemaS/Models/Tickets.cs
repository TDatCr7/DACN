using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Tickets", Schema = "dbo")]
    public class Tickets
    {
    [Key]
    [Column("Ticket_ID")]
    [StringLength(10)]
    public string TicketId { get; set; }
    [Required]
    [Column("Invoice_ID")]
    [MaxLength(50)]
    public string InvoiceId { get; set; }
    [Required]
    [Column("Ticket_Type_ID")]
    [StringLength(10)]
    public string TicketTypeId { get; set; }
    [Required]
    [Column("Show_Time_ID")]
    [StringLength(10)]
    public string ShowTimeId { get; set; }
    [Required]
    [Column("Seat_ID")]
    [StringLength(10)]
    public string SeatId { get; set; }
    public byte? Status { get; set; }
    [Column(TypeName="money")]
    public decimal? Price { get; set; }
    [Column("Created_Booking")]
    public DateTime? CreatedBooking { get; set; }
    public DateTime? Expire { get; set; }

    }
}
