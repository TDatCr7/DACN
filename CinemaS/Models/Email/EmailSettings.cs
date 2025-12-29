namespace CinemaS.Models.Email
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "";
        public int Port { get; set; } = 587;
        public string SenderEmail { get; set; } = "";
        public string SenderPassword { get; set; } = "";

        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "Cinema Booking";

        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;

        public string SmtpUser { get; set; } = "";
        public string SmtpPass { get; set; } = "";

    }
}
