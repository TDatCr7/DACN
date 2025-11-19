using System.Collections.Generic;

namespace CinemaS.Models.ViewModels
{
    public class BannerMovieItemVM
    {
        public string MoviesId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsHidden { get; set; }  // true = ẩn khỏi slider
    }

    public class BannerManageVM
    {
        public List<BannerMovieItemVM> MovieBanners { get; set; } = new();
        public List<HomeDisplayConfigVM.ExtraBannerItem> ExtraBanners { get; set; } = new();

        // Phân trang cho banner phim
        public int PageIndex { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }

    public class TrailerManageItemVM
    {
        public string MoviesId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? TrailerLink { get; set; }
        public bool ShowOnHome { get; set; }
    }

    public class TrailerManageVM
    {
        public List<TrailerManageItemVM> Items { get; set; } = new();

        // Phân trang cho trailer
        public int PageIndex { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }
}
