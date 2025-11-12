namespace CinemaS.Models.Payments
{
    public class VnPaySettings
    {
        public string TmnCode { get; set; } = "";
        public string HashSecret { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string ReturnUrl { get; set; } = "";
    }
}
