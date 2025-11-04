namespace CinemaS.Models.ViewModels
{
    public class CinemaTheaterVM
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string CinemaTypeId { get; set; } = default!;
        public string MovieTheaterId { get; set; } = default!;
   public string? Name { get; set; }
        public string? CinemaTypeName { get; set; }
        public string? MovieTheaterName { get; set; }
        public int? NumOfRows { get; set; }
        public int? NumOfColumns { get; set; }
        public int? TotalSeats { get; set; }
        public byte? Status { get; set; }
    }
}
