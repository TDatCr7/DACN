namespace CinemaS.Models.Email
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "";
        public int Port { get; set; } = 587;
        public string SenderEmail { get; set; } = "";
        public string SenderPassword { get; set; } = "";
    }
}
