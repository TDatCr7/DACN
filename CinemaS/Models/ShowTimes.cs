using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Show_Times", Schema = "dbo")]
    public class ShowTimes
    {
        [Key]
        [Column("Show_Time_ID")]
        [StringLength(10)]
        public string ShowTimeId { get; set; }
        [Required]
        [Column("Movies_ID")]
        [StringLength(10)]
        public string MoviesId { get; set; }
        [Required]
        [Column("Cinema_Theater_ID")]
        [StringLength(10)]
        public string CinemaTheaterId { get; set; }
        public int? OriginPrice { get; set; }
        
        /// <summary>
        /// Price adjustment percentage for this showtime.
        /// Range: -100 to 100. 
        /// Example: 10 means +10% price increase, -20 means 20% discount.
        /// FinalPrice = BasePrice * (1 + PriceAdjustmentPercent / 100)
        /// </summary>
        [Column("Price_Adjustment_Percent")]
        public decimal? PriceAdjustmentPercent { get; set; }
        
        [Column("Show_Date")]
        public DateTime? ShowDate { get; set; }
        [Column("Start_Time")]
        public DateTime? StartTime { get; set; }
        [Column("End_Time")]
        public DateTime? EndTime { get; set; }
        [Column("Total_Cinema")]
        public int? TotalCinema { get; set; }
        [Column("Created_At")]
        public DateTime? CreatedAt { get; set; }
        [Column("Updated_At")]
        public DateTime? UpdatedAt { get; set; }
    }
}
