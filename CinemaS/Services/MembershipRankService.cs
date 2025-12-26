using CinemaS.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Services
{
    /// <summary>
    /// Service xử lý logic phân hạng thành viên và tính giảm giá
    /// </summary>
    public interface IMembershipRankService : IRankService
    {
        // IMembershipRankService inherits from IRankService
    }

    /// <summary>
    /// Alias interface for backward compatibility
    /// </summary>
    public interface IRankService
    {
        /// <summary>
        /// Lấy hạng thành viên hiện tại của user dựa trên điểm tích lũy
        /// </summary>
        Task<MembershipRank?> GetCurrentRankAsync(int points);

        /// <summary>
        /// Lấy hạng thành viên tiếp theo và số điểm cần để đạt (tuple version)
        /// </summary>
        Task<(MembershipRank? nextRank, int pointsNeeded)> GetNextRankTupleAsync(int currentPoints);

        /// <summary>
        /// Lấy thông tin hạng tiếp theo (NextRankInfo version)
        /// </summary>
        Task<NextRankInfo?> GetNextRankInfoAsync(int currentPoints);

        /// <summary>
        /// Lấy thông tin hạng thành viên của user theo UserId
        /// </summary>
        Task<MembershipRankInfo?> GetUserRankInfoAsync(string userId);

        /// <summary>
        /// Lấy thông tin hạng thành viên của user theo Email
        /// </summary>
        Task<MembershipRankInfo?> GetUserRankInfoByEmailAsync(string email);

        /// <summary>
        /// Tính giảm giá theo hạng thành viên cho vé và đồ ăn
        /// </summary>
        Task<RankDiscountResult> CalculateRankDiscountAsync(
            string? userId,
            decimal ticketTotal,
            decimal snackTotal,
            List<SeatTypeInfo>? seatTypes = null);

        /// <summary>
        /// Tính giảm giá theo hạng thành viên (alias cho CalculateRankDiscountAsync, dùng cho Booking)
        /// </summary>
        Task<OrderDiscountResult> CalculateOrderDiscountAsync(
            string? userId,
            decimal ticketTotal,
            decimal snackTotal,
            bool hasNonNormalSeats = false);

        /// <summary>
        /// Tính điểm tích lũy với hệ số nhân của hạng thành viên
        /// </summary>
        Task<int> CalculatePointsAsync(string? userId, decimal baseAmount);

        /// <summary>
        /// Cập nhật hạng thành viên của user dựa trên điểm mới
        /// </summary>
        Task UpdateUserRankAsync(string userId);

        /// <summary>
        /// Lấy tất cả các hạng thành viên
        /// </summary>
        Task<List<MembershipRank>> GetAllRanksAsync();
    }

    /// <summary>
    /// Thông tin chi tiết về hạng thành viên của user
    /// </summary>
    public class MembershipRankInfo
    {
        public string UserId { get; set; } = "";
        public string? UserName { get; set; }
        public int CurrentPoints { get; set; }

        public string RankId { get; set; } = "";
        public string RankName { get; set; } = "";
        public int RankMinPoints { get; set; }
        public int? RankMaxPoints { get; set; }

        public decimal TicketDiscountPercent { get; set; }
        public decimal SnackDiscountPercent { get; set; }
        public decimal PointMultiplier { get; set; } = 1m;
        public bool OnlyNormalSeat { get; set; }
        public string? Description { get; set; }

        // Hạng tiếp theo (deprecated, use NextRankInfo separately)
        public string? NextRankId { get; set; }
        public string? NextRankName { get; set; }
        public int? NextRankMinPoints { get; set; }
        public int PointsToNextRank { get; set; }
        public decimal? NextTicketDiscountPercent { get; set; }
        public decimal? NextSnackDiscountPercent { get; set; }
        public string? NextRankDescription { get; set; }

        /// <summary>
        /// % tiến trình đến hạng tiếp theo (0-100)
        /// </summary>
        public int ProgressPercent { get; set; }
    }

    /// <summary>
    /// Thông tin hạng tiếp theo
    /// </summary>
    public class NextRankInfo
    {
        public string RankId { get; set; } = "";
        public string RankName { get; set; } = "";
        public int RequiredPoints { get; set; }
        public int PointsNeeded { get; set; }
        public decimal TicketDiscountPercent { get; set; }
        public decimal SnackDiscountPercent { get; set; }
        public decimal PointMultiplier { get; set; } = 1m;
        public string? Description { get; set; }
    }

    /// <summary>
    /// Kết quả tính giảm giá theo hạng
    /// </summary>
    public class RankDiscountResult
    {
        public string RankId { get; set; } = "";
        public string RankName { get; set; } = "";

        /// <summary>
        /// Tổng tiền vé gốc
        /// </summary>
        public decimal OriginalTicketTotal { get; set; }

        /// <summary>
        /// Số tiền giảm vé theo hạng
        /// </summary>
        public decimal TicketDiscountAmount { get; set; }

        /// <summary>
        /// % giảm vé
        /// </summary>
        public decimal TicketDiscountPercent { get; set; }

        /// <summary>
        /// Tổng tiền đồ ăn gốc
        /// </summary>
        public decimal OriginalSnackTotal { get; set; }

        /// <summary>
        /// Số tiền giảm đồ ăn theo hạng
        /// </summary>
        public decimal SnackDiscountAmount { get; set; }

        /// <summary>
        /// % giảm đồ ăn
        /// </summary>
        public decimal SnackDiscountPercent { get; set; }

        /// <summary>
        /// Tổng số tiền giảm (vé + đồ ăn)
        /// </summary>
        public decimal TotalDiscountAmount => TicketDiscountAmount + SnackDiscountAmount;

        /// <summary>
        /// Tổng tiền sau giảm theo hạng
        /// </summary>
        public decimal FinalTotal => (OriginalTicketTotal - TicketDiscountAmount) + (OriginalSnackTotal - SnackDiscountAmount);

        /// <summary>
        /// Hệ số nhân điểm
        /// </summary>
        public decimal PointMultiplier { get; set; } = 1m;
    }

    /// <summary>
    /// Thông tin loại ghế để tính giảm giá
    /// </summary>
    public class SeatTypeInfo
    {
        public string SeatId { get; set; } = "";
        public string SeatTypeName { get; set; } = "";
        public decimal Price { get; set; }
        public bool IsNormal => SeatTypeName.Equals("NORMAL", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kết quả tính giảm giá đơn hàng (dùng cho API Booking)
    /// </summary>
    public class OrderDiscountResult
    {
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
        public string RankId { get; set; } = "";
        public string RankName { get; set; } = "";
        public decimal TicketDiscount { get; set; }
        public decimal SnackDiscount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TicketDiscountPercent { get; set; }
        public decimal SnackDiscountPercent { get; set; }
        public decimal TotalFinal { get; set; }
        public decimal PointMultiplier { get; set; } = 1m;
    }

    public class MembershipRankService : IMembershipRankService
    {
        private readonly CinemaContext _context;

        public MembershipRankService(CinemaContext context)
        {
            _context = context;
        }

        public async Task<List<MembershipRank>> GetAllRanksAsync()
        {
            return await _context.MembershipRanks.AsNoTracking()
                .OrderBy(r => r.RequirePoint ?? 0)
                .ToListAsync();
        }

        public async Task<MembershipRank?> GetCurrentRankAsync(int points)
        {
            // Lấy hạng có RequirePoint <= points và MaxPoint >= points (hoặc MaxPoint null)
            var ranks = await _context.MembershipRanks.AsNoTracking()
                .OrderByDescending(r => r.RequirePoint ?? 0)
                .ToListAsync();

            foreach (var rank in ranks)
            {
                var minPoint = rank.RequirePoint ?? 0;
                var maxPoint = rank.MaxPoint;

                if (points >= minPoint && (maxPoint == null || points <= maxPoint))
                {
                    return rank;
                }
            }

            // Trả về hạng thấp nhất nếu không tìm thấy
            return ranks.LastOrDefault();
        }

        public async Task<(MembershipRank? nextRank, int pointsNeeded)> GetNextRankTupleAsync(int currentPoints)
        {
            var ranks = await _context.MembershipRanks.AsNoTracking()
                .OrderBy(r => r.RequirePoint ?? 0)
                .ToListAsync();

            foreach (var rank in ranks)
            {
                var minPoint = rank.RequirePoint ?? 0;
                if (minPoint > currentPoints)
                {
                    return (rank, minPoint - currentPoints);
                }
            }

            return (null, 0);
        }

        public async Task<NextRankInfo?> GetNextRankInfoAsync(int currentPoints)
        {
            var (nextRank, pointsNeeded) = await GetNextRankTupleAsync(currentPoints);
            if (nextRank == null) return null;

            return new NextRankInfo
            {
                RankId = nextRank.MembershipRankId,
                RankName = nextRank.Name ?? "",
                RequiredPoints = nextRank.RequirePoint ?? 0,
                PointsNeeded = pointsNeeded,
                TicketDiscountPercent = nextRank.TicketDiscountPercent ?? 0m,
                SnackDiscountPercent = nextRank.SnackDiscountPercent ?? 0m,
                PointMultiplier = nextRank.PointMultiplier ?? 1m,
                Description = nextRank.Description
            };
        }

        public async Task<MembershipRankInfo?> GetUserRankInfoAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;

            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return null;

            return await BuildRankInfoAsync(user);
        }

        public async Task<MembershipRankInfo?> GetUserRankInfoByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());

            if (user == null) return null;

            return await BuildRankInfoAsync(user);
        }

        private async Task<MembershipRankInfo> BuildRankInfoAsync(Users user)
        {
            var currentPoints = user.SavePoint ?? 0;
            var currentRank = await GetCurrentRankAsync(currentPoints);
            var (nextRank, pointsNeeded) = await GetNextRankTupleAsync(currentPoints);

            var info = new MembershipRankInfo
            {
                UserId = user.UserId,
                UserName = user.FullName,
                CurrentPoints = currentPoints,
                RankId = currentRank?.MembershipRankId ?? "",
                RankName = currentRank?.Name ?? "Thành viên mới",
                RankMinPoints = currentRank?.RequirePoint ?? 0,
                RankMaxPoints = currentRank?.MaxPoint,
                TicketDiscountPercent = currentRank?.TicketDiscountPercent ?? 0m,
                SnackDiscountPercent = currentRank?.SnackDiscountPercent ?? 0m,
                PointMultiplier = currentRank?.PointMultiplier ?? 1m,
                OnlyNormalSeat = currentRank?.OnlyNormalSeat ?? false,
                Description = currentRank?.Description
            };

            if (nextRank != null)
            {
                info.NextRankId = nextRank.MembershipRankId;
                info.NextRankName = nextRank.Name;
                info.NextRankMinPoints = nextRank.RequirePoint ?? 0;
                info.PointsToNextRank = pointsNeeded;
                info.NextTicketDiscountPercent = nextRank.TicketDiscountPercent;
                info.NextSnackDiscountPercent = nextRank.SnackDiscountPercent;
                info.NextRankDescription = nextRank.Description;

                // Tính % tiến trình
                var rangeStart = currentRank?.RequirePoint ?? 0;
                var rangeEnd = nextRank.RequirePoint ?? 0;
                var rangeSize = rangeEnd - rangeStart;
                var progress = currentPoints - rangeStart;

                if (rangeSize > 0)
                {
                    info.ProgressPercent = Math.Min(100, (int)(progress * 100.0 / rangeSize));
                }
            }
            else
            {
                // Đã đạt hạng cao nhất
                info.ProgressPercent = 100;
            }

            return info;
        }

        public async Task<RankDiscountResult> CalculateRankDiscountAsync(
            string? userId,
            decimal ticketTotal,
            decimal snackTotal,
            List<SeatTypeInfo>? seatTypes = null)
        {
            var result = new RankDiscountResult
            {
                OriginalTicketTotal = ticketTotal,
                OriginalSnackTotal = snackTotal,
                TicketDiscountPercent = 0,
                SnackDiscountPercent = 0,
                PointMultiplier = 1m
            };

            if (string.IsNullOrWhiteSpace(userId))
                return result;

            var rankInfo = await GetUserRankInfoAsync(userId);
            if (rankInfo == null)
                return result;

            result.RankId = rankInfo.RankId;
            result.RankName = rankInfo.RankName;
            result.PointMultiplier = rankInfo.PointMultiplier;

            // Tính giảm giá vé
            if (rankInfo.TicketDiscountPercent > 0)
            {
                result.TicketDiscountPercent = rankInfo.TicketDiscountPercent;

                if (rankInfo.OnlyNormalSeat && seatTypes != null && seatTypes.Any())
                {
                    // Chỉ giảm giá cho ghế thường
                    var normalSeatsTotal = seatTypes.Where(s => s.IsNormal).Sum(s => s.Price);
                    result.TicketDiscountAmount = Math.Round(normalSeatsTotal * rankInfo.TicketDiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
                }
                else
                {
                    // Giảm giá cho tất cả vé
                    result.TicketDiscountAmount = Math.Round(ticketTotal * rankInfo.TicketDiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
                }
            }

            // Tính giảm giá đồ ăn
            if (rankInfo.SnackDiscountPercent > 0)
            {
                result.SnackDiscountPercent = rankInfo.SnackDiscountPercent;
                result.SnackDiscountAmount = Math.Round(snackTotal * rankInfo.SnackDiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
            }

            return result;
        }

        public async Task<OrderDiscountResult> CalculateOrderDiscountAsync(
            string? userId,
            decimal ticketTotal,
            decimal snackTotal,
            bool hasNonNormalSeats = false)
        {
            var result = new OrderDiscountResult
            {
                TotalFinal = ticketTotal + snackTotal,
                PointMultiplier = 1m
            };

            if (string.IsNullOrWhiteSpace(userId))
                return result;

            var rankInfo = await GetUserRankInfoAsync(userId);
            if (rankInfo == null)
                return result;

            result.RankId = rankInfo.RankId;
            result.RankName = rankInfo.RankName;
            result.PointMultiplier = rankInfo.PointMultiplier;
            result.TicketDiscountPercent = rankInfo.TicketDiscountPercent;
            result.SnackDiscountPercent = rankInfo.SnackDiscountPercent;

            // Tính giảm giá vé
            if (rankInfo.TicketDiscountPercent > 0)
            {
                if (rankInfo.OnlyNormalSeat && hasNonNormalSeats)
                {
                    // Không áp dụng giảm giá vé nếu có ghế không phải NORMAL và hạng chỉ giảm cho ghế thường
                    result.TicketDiscount = 0;
                }
                else
                {
                    result.TicketDiscount = Math.Round(ticketTotal * rankInfo.TicketDiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
                }
            }

            // Tính giảm giá đồ ăn
            if (rankInfo.SnackDiscountPercent > 0)
            {
                result.SnackDiscount = Math.Round(snackTotal * rankInfo.SnackDiscountPercent / 100m, 0, MidpointRounding.AwayFromZero);
            }

            result.TotalDiscount = result.TicketDiscount + result.SnackDiscount;
            result.TotalFinal = (ticketTotal - result.TicketDiscount) + (snackTotal - result.SnackDiscount);

            return result;
        }

        public async Task<int> CalculatePointsAsync(string? userId, decimal baseAmount)
        {
            // Mặc định: 1.000đ = 1 điểm
            var basePoints = (int)(baseAmount / 1000m);

            if (string.IsNullOrWhiteSpace(userId))
                return basePoints;

            var rankInfo = await GetUserRankInfoAsync(userId);
            if (rankInfo == null || rankInfo.PointMultiplier <= 1m)
                return basePoints;

            // Nhân với hệ số của hạng
            return (int)Math.Round(basePoints * rankInfo.PointMultiplier, 0, MidpointRounding.AwayFromZero);
        }

        public async Task UpdateUserRankAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return;

            var currentPoints = user.SavePoint ?? 0;
            var newRank = await GetCurrentRankAsync(currentPoints);

            if (newRank != null && user.MembershipRankId != newRank.MembershipRankId)
            {
                user.MembershipRankId = newRank.MembershipRankId;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}
