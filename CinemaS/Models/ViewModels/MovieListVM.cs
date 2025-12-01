namespace CinemaS.Models.ViewModels
{
    public class MovieListVM
    {
        public List<MovieCardVM> Movies { get; set; } = new();
        public List<string> Genres { get; set; } = new();

        public string? Message { get; set; }
        public string? Error { get; set; }

        public int PageIndex { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        // filter hiện tại
        public string? SelectedGenre { get; set; }
        public string? SelectedStatus { get; set; }
        public string? SearchText { get; set; }
    }
}
