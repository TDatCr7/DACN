namespace CinemaS.Models.ViewModels
{
    public class ShowTimeVM
    {
        public string ShowTimeId { get; set; } = default!;
        public string MoviesId { get; set; } = default!;
        public string CinemaTheaterId { get; set; } = default!;
        public string? MovieTitle { get; set; }
        public string? CinemaName { get; set; } // ✅ Thêm tên phòng chiếu
        public string? CinemaTheaterName { get; set; }
        public DateTime? ShowDate { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Price { get; set; } // ✅ Thêm giá vé
        public int? OriginPrice { get; set; }
        public int? TotalSeats { get; set; } // ✅ Thêm tổng ghế
        public int? TotalCinema { get; set; }
        public int? AvailableSeats { get; set; }
    }
}
