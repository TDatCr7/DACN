namespace CinemaS.Models.ViewModels
{
    public class MovieListVM
    {
        public List<MovieCardVM> Movies { get; set; } = new();
        public List<string> Genres { get; set; } = new();

        public string? Message { get; set; }
        public string? Error { get; set; }

        // ===== Phân trang =====
        public int PageIndex { get; set; } = 1;   // trang hiện tại
        public int TotalPages { get; set; } = 1;  // tổng số trang

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }
}
