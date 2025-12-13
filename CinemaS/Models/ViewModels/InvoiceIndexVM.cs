using System;

namespace CinemaS.Models.ViewModels
{
    public class InvoiceIndexVM
    {
        public string InvoiceId { get; set; } = default!;
        public string? CustomerName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public decimal TotalPrice { get; set; }
        public byte Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
