namespace CinemaS.Models.ViewModels
{
    public class SeatVM
    {
        public string SeatId { get; set; } = default!;
        public string SeatTypeId { get; set; } = default!;
        public string? SeatTypeName { get; set; }
        public decimal? SeatTypePrice { get; set; }
        public string? RowIndex { get; set; }
        public int? ColumnIndex { get; set; }
        public string? Label { get; set; }
        public string Status { get; set; } = "Available"; // Available, Selected, Booked
        public bool IsCouple { get; set; }
        public bool IsVIP { get; set; }
        public bool IsActive { get; set; } = true; // Thêm thuộc tính IsActive
        public string? PairId { get; set; }
    }
}
