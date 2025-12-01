namespace CinemaS.Models.ViewModels
{
    public class MovieCardVM
    {
        public string MoviesId { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string? Summary { get; set; }

        public string? PosterImage { get; set; }
        public string? BannerImage { get; set; }

        public string? Rating { get; set; }
        public int? Duration { get; set; }
        public string? Language { get; set; }
        public string? Age { get; set; }

        // Nhiều thể loại
        public List<string> Genres { get; set; } = new();
        // Chuỗi hiển thị (ví dụ: "Hành động, Hài")
        public string? GenreName { get; set; }

        public DateTime? ReleaseDate { get; set; }
        public string? StatusId { get; set; }
        public string? StatusName { get; set; }

        public string? Country { get; set; }
        public string? AudioOption { get; set; }
    }
}
