namespace CinemaS.Models.ViewModels
{
    public class MovieListVM
    {
        public List<MovieCardVM> Movies { get; set; } = new();
        public List<string> Genres { get; set; } = new();

        // Thông báo nhẹ cho view (tuỳ chọn)
        public string? Message { get; set; }
        public string? Error { get; set; }
    }
}
