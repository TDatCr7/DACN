namespace CinemaS.Models.ViewModels
{
    public class MovieCardVM
    {
        // --- Cơ bản ---
        public string MoviesId { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string? Summary { get; set; }

        // --- Ảnh ---
        public string? PosterImage { get; set; }    // cho thẻ phim
        public string? BannerImage { get; set; }    // cho Hero Slider

        // --- Thông tin mở rộng ---
        public string? Rating { get; set; }
        public int? Duration { get; set; }          // phút
        public string? Language { get; set; }
        public string? Age { get; set; }

        // --- Thể loại & Trạng thái ---
        public List<string>? Genres { get; set; }
        public string? GenreName { get; set; }

        // --- Ngày phát hành & Trạng thái chiếu ---
        public DateTime? ReleaseDate { get; set; }
        public string? StatusId { get; set; }
        public string? StatusName { get; set; }

        public string? Country { get; set; }
        public string? AudioOption { get; set; }
    }
}
