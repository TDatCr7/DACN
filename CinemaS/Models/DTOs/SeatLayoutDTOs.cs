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

    // ===========================
    // Additional Request DTOs
    // ===========================
    
    public class UpdateSeatTypeRequest
    {
        public string SeatId { get; set; } = default!;
        public string SeatTypeId { get; set; } = default!;
        public bool IsActive { get; set; } = true;
    }

    public class BulkUpdateSeatTypeRequest
    {
        public List<string> SeatIds { get; set; } = new();
        public string SeatTypeId { get; set; } = default!;
    }

    public class SoftDeleteSeatRequest
    {
        public string SeatId { get; set; } = default!;
    }

    public class RestoreSeatRequest
    {
        public string SeatId { get; set; } = default!;
    }

    public class AddRowRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
    }

    public class DeleteRowRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string RowLabel { get; set; } = default!;
    }

    public class AddColumnRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
    }

    public class DeleteColumnRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int ColumnIndex { get; set; }
    }

    public class UpdateRowSeatTypeRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string RowLabel { get; set; } = default!;
        public string SeatTypeId { get; set; } = default!;
    }

    public class UpdateColumnSeatTypeRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int ColumnIndex { get; set; }
        public string SeatTypeId { get; set; } = default!;
    }

    public class ConvertSeatToAisleRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string RowLabel { get; set; } = default!;
        public int ColumnIndex { get; set; }
        public string Mode { get; set; } = "row"; // "row" or "column"
    }

    public class SearchSeatsRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string? RowLabel { get; set; }
        public int ColumnIndex { get; set; }
        public string? SeatTypeId { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class RestoreRowFromAisleRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string RowLabel { get; set; } = default!;
    }

    public class RestoreColumnFromAisleRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int ColumnIndex { get; set; }
    }

    public class ToggleRowAisleRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string RowIndex { get; set; } = default!;
    }

    public class ToggleColumnAisleRequest
    {
        public string CinemaTheaterId { get; set; } = default!;
        public int ColumnIndex { get; set; }
    }
}
