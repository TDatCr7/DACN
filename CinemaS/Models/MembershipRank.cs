using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Membership_Rank", Schema = "dbo")]
    public class MembershipRank
    {
        [Key]
        [Column("Membership_Rank_ID")]
        [StringLength(10)]
        public string MembershipRankId { get; set; } = default!;

        [MaxLength(100)]
        public string? Name { get; set; }

        /// <summary>
        /// Điểm tối thiểu để đạt hạng này
        /// </summary>
        public int? RequirePoint { get; set; }

        /// <summary>
        /// Điểm tối đa của hạng này (null = không giới hạn)
        /// </summary>
        [Column("Max_Point")]
        public int? MaxPoint { get; set; }

        public int? PointReturnTicket { get; set; }
        public int? PointReturnCombo { get; set; }
        public int? PriorityLevel { get; set; }

        /// <summary>
        /// % giảm giá vé (VD: 5 = giảm 5%)
        /// </summary>
        [Column("Ticket_Discount_Percent")]
        public decimal? TicketDiscountPercent { get; set; }

        /// <summary>
        /// % giảm giá đồ ăn/nước uống (VD: 2 = giảm 2%)
        /// </summary>
        [Column("Snack_Discount_Percent")]
        public decimal? SnackDiscountPercent { get; set; }

        /// <summary>
        /// Hệ số nhân điểm (VD: 1.2 = nhân x1.2)
        /// </summary>
        [Column("Point_Multiplier")]
        public decimal? PointMultiplier { get; set; }

        /// <summary>
        /// Chỉ áp dụng giảm giá cho ghế thường (true = chỉ ghế thường, false = mọi loại ghế)
        /// </summary>
        [Column("Only_Normal_Seat")]
        public bool? OnlyNormalSeat { get; set; }

        /// <summary>
        /// Mô tả quyền lợi của hạng thành viên
        /// </summary>
        [Column("Description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("Created_At")]
        public DateTime? CreatedAt { get; set; }

        [Column("Updated_At")]
        public DateTime? UpdatedAt { get; set; }
    }
}
