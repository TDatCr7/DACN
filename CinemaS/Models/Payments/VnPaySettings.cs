namespace CinemaS.Models.Payments
{
    public class VnPaySettings
    {
        public string TmnCode { get; set; } = "";
        public string HashSecret { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string ReturnUrl { get; set; } = "";

        // MobilePaymentController
        public string Locale { get; set; } = "vn";
        public string CurrCode { get; set; } = "VND";
        public string OrderType { get; set; } = "other";
    }
}
