using System.Collections.Generic;

namespace CinemaS.Models.ViewModels
{
    public class HomeVM
    {
        public List<MovieCardVM> Carousel { get; set; } = new();
        public List<MovieCardVM> NowShowing { get; set; } = new();
        public List<MovieCardVM> ComingSoon { get; set; } = new();

        // Trailer: MoviesId, Title, TrailerLink, PosterImage
        public List<(string MoviesId, string Title, string? TrailerLink, string? PosterImage)> Trailers { get; set; } = new();

        // Banner ngoài phim
        public List<HomeDisplayConfigVM.ExtraBannerItem> ExtraBanners { get; set; } = new();
    }
}
