using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Ticket_Types", Schema = "dbo")]
    public class TicketTypes
    {
    [Key]
    [Column("Ticket_Type_ID")]
    [StringLength(10)]
    public string TicketTypeId { get; set; }
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(250)]
    public string? Description { get; set; }
    [Column(TypeName="money")]
    public decimal? Price { get; set; }

    }
}
