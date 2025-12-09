namespace CinemaS.Models.ViewModels
{
    /// <summary>
    /// ViewModel cho trang đặt vé hộ khách hàng (Admin Booking)
    /// </summary>
    public class AdminBookingVM
    {
        public string ShowTimeId { get; set; } = default!;
        public string MoviesId { get; set; } = default!;
        public string? MovieTitle { get; set; }
        public string? MoviePoster { get; set; }
        public string? CinemaTheaterName { get; set; }
        public DateTime? ShowDate { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public List<SeatVM> Seats { get; set; } = new();
        public int NumOfRows { get; set; }
        public int NumOfColumns { get; set; }
        
        // Thông tin đặt vé hộ
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public bool IsGuestBooking { get; set; } // True nếu đặt cho khách vãng lai
    }
}
