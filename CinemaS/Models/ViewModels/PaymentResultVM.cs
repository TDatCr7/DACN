namespace CinemaS.Models.ViewModels
{
    public class PaymentResultVM
    {
        public bool IsSuccess { get; set; }
        public string InvoiceId { get; set; } = "";
        public string TransactionNo { get; set; } = "";
        public string PayDateStr { get; set; } = "";
        public int TicketCount { get; set; }
        public List<string>? SeatLabels { get; set; }
        public string SnackSummary { get; set; } = "";
        public decimal TotalPrice { get; set; }

        public string MovieTitle { get; set; } = "";
        public string CinemaTheaterName { get; set; } = "";
        public string ShowDateStr { get; set; } = "";
        public string StartTimeStr { get; set; } = "";
    }
}
