namespace CinemaS.Models.ViewModels
{
    public class SeatSelectionVM
    {
        public string ShowTimeId { get; set; } = default!;
        public string MoviesId { get; set; } = default!;
        public string? MovieTitle { get; set; }
        public string? MoviePoster { get; set; }
        public string? CinemaTheaterName { get; set; }
        public DateTime? ShowDate { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; } // ✅ Thêm thời gian kết thúc
        public List<SeatVM> Seats { get; set; } = new();
        public int NumOfRows { get; set; }
        public int NumOfColumns { get; set; }
    }
}
