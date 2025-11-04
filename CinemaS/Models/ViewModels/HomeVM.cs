namespace CinemaS.Models.ViewModels
{
    public class HomeVM
    {
        // Carousel banner (ưu tiên phim đang chiếu có Banner_Image)
        public List<MovieCardVM> Carousel { get; set; } = new();

        // Tabs “Đang chiếu” / “Sắp chiếu”
        public List<MovieCardVM> NowShowing { get; set; } = new();
        public List<MovieCardVM> ComingSoon { get; set; } = new();

        // Trailer hot (link YouTube)
        public List<(string MoviesId, string Title, string? TrailerLink, string? PosterImage)> Trailers { get; set; } = new();
    }
}
