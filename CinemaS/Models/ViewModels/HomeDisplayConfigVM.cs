using System.Collections.Generic;

namespace CinemaS.Models.ViewModels
{
    /// <summary>
    /// Cấu hình hiển thị trang Home, lưu ra file JSON, không đụng tới database.
    /// </summary>
    public class HomeDisplayConfigVM
    {
        /// <summary>
        /// Danh sách MoviesId bị ẩn khỏi slider banner (banner phim đang chiếu).
        /// </summary>
        public List<string> HiddenBannerMovieIds { get; set; } = new();

        /// <summary>
        /// Danh sách banner ngoài (không gắn với phim).
        /// </summary>
        public List<ExtraBannerItem> ExtraBanners { get; set; } = new();

        /// <summary>
        /// Danh sách MoviesId được phép hiện trailer ở Home.
        /// Nếu rỗng: hiểu là cho phép tất cả phim có TrailerLink.
        /// </summary>
        public List<string> HomeTrailerMovieIds { get; set; } = new();

        public class ExtraBannerItem
        {
            public int Id { get; set; }
            public string ImagePath { get; set; } = string.Empty;
        }
    }
}
