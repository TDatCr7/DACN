namespace CinemaS.Models.DTOs
{
    /// <summary>
    /// DTO for saving the entire seat layout
    /// </summary>
    public class SaveSeatLayoutRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int NumOfRows { get; set; }
        public int NumOfColumns { get; set; }
        public List<SeatDataDTO> Seats { get; set; } = new();
    }

    /// <summary>
    /// Represents one seat in the save request
    /// </summary>
    public class SeatDataDTO
    {
        public string? SeatId { get; set; }  // Null if new seat
        public string SeatTypeId { get; set; } = default!;
        public int RowNumber { get; set; }
        public int ColumnIndex { get; set; }
        public string? Label { get; set; }
        public bool IsActive { get; set; } = true;
        public string? PairId { get; set; }
    }

    /// <summary>
    /// Request to create a couple seat pair
    /// </summary>
    public class CreateCoupleSeatRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int RowNumber { get; set; }
        public int ColumnIndex { get; set; }  // Left seat column
    }

    /// <summary>
    /// Request to delete/inactivate a seat
    /// </summary>
    public class DeleteSeatRequest
    {
        public string SeatId { get; set; } = default!;
    }
}
