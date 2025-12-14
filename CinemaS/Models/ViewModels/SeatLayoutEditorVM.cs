namespace CinemaS.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the dynamic seat layout editor
    /// </summary>
    public class SeatLayoutEditorVM
    {
        public string CinemaTheaterId { get; set; } = default!;
        public string? TheaterName { get; set; }
        public string? CinemaTypeName { get; set; }
        public int NumOfRows { get; set; }
        public int NumOfColumns { get; set; }
        
        /// <summary>
        /// Current layout grid - seats organized by row
        /// </summary>
        public List<SeatRowVM> Rows { get; set; } = new();
        
        /// <summary>
        /// Available seat types for selection
        /// </summary>
        public List<SeatTypeOption> SeatTypeOptions { get; set; } = new();
    }

    /// <summary>
    /// Represents one row in the seat layout
    /// </summary>
    public class SeatRowVM
    {
        public string RowLabel { get; set; } = default!;
        public int RowNumber { get; set; }
        public List<SeatCellVM> Seats { get; set; } = new();
    }

    /// <summary>
    /// Represents one seat cell in the grid
    /// </summary>
    public class SeatCellVM
    {
        public string? SeatId { get; set; }
        public string SeatTypeId { get; set; } = default!;
        public string? SeatTypeName { get; set; }
        public string? Label { get; set; }
        public int RowNumber { get; set; }
        public int ColumnIndex { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public bool IsAisle { get; set; } = false; // ✅ Add IsAisle property
        public string? PairId { get; set; }
        public bool IsLeftOfPair { get; set; }
        public bool IsRightOfPair { get; set; }
    }

    /// <summary>
    /// Seat type option for the selector
    /// </summary>
    public class SeatTypeOption
    {
        public string SeatTypeId { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal? Price { get; set; }
    }
}
