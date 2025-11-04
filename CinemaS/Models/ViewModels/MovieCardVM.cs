namespace CinemaS.Models.ViewModels
{
    public class MovieCardVM
    {
        public string MoviesId { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string? PosterImage { get; set; }      // dùng cho thẻ phim
        public string? BannerImage { get; set; }      // dùng cho HERO slider
        public string? Summary { get; set; }
        public string? GenreName { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string? StatusId { get; set; }
        public string? StatusName { get; set; }
    }
}
