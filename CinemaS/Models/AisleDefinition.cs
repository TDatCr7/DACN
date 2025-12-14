using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    /// <summary>
    /// Persists aisle definitions for rows and columns.
    /// When a row or column is marked as an aisle, all future seats created in that row/column
    /// will automatically be created as Is_Deleted = true.
    /// </summary>
    [Table("Aisle_Definitions", Schema = "dbo")]
    public class AisleDefinition
    {
        [Key]
        [Column("Aisle_Definition_ID")]
        [MaxLength(10)]
        public string AisleDefinitionId { get; set; } = default!;

        [Required]
        [Column("Cinema_Theater_ID")]
        [MaxLength(10)]
        public string CinemaTheaterId { get; set; } = default!;

        /// <summary>
        /// Type of aisle: "ROW" or "COLUMN"
        /// </summary>
        [Required]
        [Column("Aisle_Type")]
        [MaxLength(10)]
        public string AisleType { get; set; } = default!;

        /// <summary>
        /// For ROW type: the RowIndex (e.g., "A", "B", "C")
        /// For COLUMN type: null
        /// </summary>
        [Column("Row_Index")]
        [MaxLength(10)]
        public string? RowIndex { get; set; }

        /// <summary>
        /// For COLUMN type: the ColumnIndex (e.g., 1, 2, 3)
        /// For ROW type: null
        /// </summary>
        [Column("Column_Index")]
        public int? ColumnIndex { get; set; }

        [Column("Created_At")]
        public DateTime? CreatedAt { get; set; }

        [Column("Updated_At")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public virtual CinemaTheaters? CinemaTheater { get; set; }
    }
}
