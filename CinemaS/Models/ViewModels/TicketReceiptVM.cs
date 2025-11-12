using System;
using System.Collections.Generic;

namespace CinemaS.Models.ViewModels
{
    public class SnackLineVM
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class TicketReceiptVM
    {
        // Result
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }

        // Payment
        public string InvoiceId { get; set; } = "";
        public string? BankCode { get; set; }      // vnp_BankCode
        public string? TransactionNo { get; set; } // vnp_TransactionNo
        public DateTime? VnpTime { get; set; }     // vnp_PayDate (UTC+7 theo VNPay)

        // Movie / Show info
        public string MovieTitle { get; set; } = "";
        public string? Poster { get; set; }
        public string CinemaTheaterName { get; set; } = "";
        public DateTime? ShowDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        // Seats
        public List<string> SeatLabels { get; set; } = new();
        public int SeatCount => SeatLabels.Count;

        // Money
        public decimal SeatAmount { get; set; }
        public List<SnackLineVM> Snacks { get; set; } = new();
        public decimal SnackAmount { get; set; }
        public decimal GrandTotal => SeatAmount + SnackAmount;

        // Customer
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
    }
}
