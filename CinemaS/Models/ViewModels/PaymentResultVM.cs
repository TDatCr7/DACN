using System;
using System.Collections.Generic;

namespace CinemaS.Models.ViewModels
{
    public class PaymentResultVM
    {
        public string? OrderId { get; set; }
        public bool IsValidSignature { get; set; }
        public string? VnpResponseCode { get; set; }
        public string? VnpTransactionStatus { get; set; }
        public string? BankCode { get; set; }
        public int Amount { get; set; }
        public string? PayDateRaw { get; set; }
        public string? TransactionNo { get; set; }
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }

        public TicketDetailVM? Detail { get; set; }
    }

    public class TicketDetailVM
    {
        public string InvoiceId { get; set; } = default!;
        public DateTime? CreatedAt { get; set; }
        public string? PaymentMethod { get; set; }
        public string? InvoiceEmail { get; set; }
        public string? InvoicePhone { get; set; }

        public string MovieTitle { get; set; } = default!;
        public string? MoviePoster { get; set; }
        public string CinemaTheater { get; set; } = default!;
        public DateTime? ShowDate { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public List<string> SeatLabels { get; set; } = new();
        public int TicketCount { get; set; }
        public decimal TicketTotal { get; set; }
        public List<TicketSnackLineVM> SnackItems { get; set; } = new();
        public decimal SnackTotal { get; set; }
        public decimal GrandTotal { get; set; }
    }

    public class TicketSnackLineVM
    {
        public string Name { get; set; } = default!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class InvoiceHistoryVM
    {
        public string InvoiceId { get; set; } = default!;
        public DateTime? CreatedAt { get; set; }
        public int Status { get; set; }
        public decimal TotalPrice { get; set; }
        public string? MovieTitle { get; set; }
        public string? Room { get; set; }
        public DateTime? ShowDate { get; set; }
        public DateTime? StartTime { get; set; }
    }
}
