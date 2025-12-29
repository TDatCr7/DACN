using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CinemaS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Controllers
{
    [ApiController]
    [Route("api")]
    public class MobileBookingController : ControllerBase
    {
        private readonly CinemaContext _context;

        public MobileBookingController(CinemaContext context)
        {
            _context = context;
        }

        // GET: /api/movies/{movieId}/showtimes
        [HttpGet("movies/{movieId}/showtimes")]
        public async Task<IActionResult> GetShowtimes(string movieId)
        {
            if (string.IsNullOrWhiteSpace(movieId)) return BadRequest();

            var data = await (from st in _context.ShowTimes.AsNoTracking()
                              join room in _context.CinemaTheaters.AsNoTracking()
                                  on st.CinemaTheaterId equals room.CinemaTheaterId
                              join theater in _context.MovieTheaters.AsNoTracking()
                                  on room.MovieTheaterId equals theater.MovieTheaterId into th
                              from theater in th.DefaultIfEmpty()
                              where st.MoviesId == movieId
                              orderby st.ShowDate, st.StartTime
                              select new
                              {
                                  showTimeId = st.ShowTimeId,
                                  showDate = st.ShowDate,
                                  startTime = st.StartTime,
                                  endTime = st.EndTime,
                                  cinemaName = theater != null ? theater.Name : "",
                                  screenName = room != null ? room.Name : ""
                              })
                              .ToListAsync();

            return Ok(data);
        }

        // GET: /api/showtimes/{showtimeId}/seats
        [HttpGet("showtimes/{showtimeId}/seats")]
        public async Task<IActionResult> GetSeats(string showtimeId)
        {
            if (string.IsNullOrWhiteSpace(showtimeId)) return BadRequest();

            var st = await _context.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShowTimeId == showtimeId);
            if (st == null) return NotFound();

            var now = DateTime.Now;

            var blockedSeatIds = await _context.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == showtimeId &&
                            (t.Status == 2 || (t.Status == 1 && t.Expire != null && t.Expire > now)))
                .Select(t => t.SeatId)
                .ToListAsync();

            decimal priceAdjustmentPercent = st.PriceAdjustmentPercent ?? 0m;

            var seats = await (from s in _context.Seats.AsNoTracking()
                               join stype in _context.SeatTypes.AsNoTracking()
                                   on s.SeatTypeId equals stype.SeatTypeId into stg
                               from stype in stg.DefaultIfEmpty()
                               where s.CinemaTheaterId == st.CinemaTheaterId
                                     && !s.IsDeleted          // ✅ FIX: giống web
                               orderby s.RowIndex, s.ColumnIndex
                               select new
                               {
                                   seatId = s.SeatId,
                                   rowLabel = s.RowIndex,
                                   colIndex = s.ColumnIndex,
                                   label = s.Label ?? "",
                                   seatTypeId = s.SeatTypeId,
                                   seatTypeName = stype != null ? (stype.Name ?? "") : "",
                                   basePrice = stype != null ? (stype.Price ?? 0m) : 0m,
                                   finalPrice = Math.Round(
                                       (stype != null ? (stype.Price ?? 0m) : 0m) * (1 + priceAdjustmentPercent / 100m),
                                       0, MidpointRounding.AwayFromZero
                                   ),
                                   isBooked = blockedSeatIds.Contains(s.SeatId),
                                   isAisle = s.IsAisle,
                                   pairId = s.PairId ?? "",
                                   isActive = s.IsActive,
                                   isCouple = (stype != null && stype.Name != null &&
                                               stype.Name.Trim().Equals("COUPLE", StringComparison.OrdinalIgnoreCase)),
                                   isVip = (stype != null && stype.Name != null &&
                                            stype.Name.Trim().Equals("VIP", StringComparison.OrdinalIgnoreCase)),
                               })
                               .ToListAsync();

            return Ok(seats);
        }


        // GET: /api/snacks
        [HttpGet("snacks")]
        public async Task<IActionResult> GetSnacks()
        {
            var snacks = await _context.Snacks.AsNoTracking()
                .Where(x => x.IsActive == true)
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    snackId = x.SnackId,
                    name = x.Name,
                    price = x.Price,
                    image = x.Image,
                    description = x.Description
                })
                .ToListAsync();

            return Ok(snacks);
        }

        // =========================
        // POST: /api/bookings/preview
        // =========================
        [HttpPost("bookings/preview")]
        public async Task<IActionResult> Preview([FromBody] PreviewRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ShowTimeId) || request.SeatIds == null || request.SeatIds.Count == 0)
                return BadRequest(new { message = "Dữ liệu không hợp lệ!" });

            var st = await _context.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShowTimeId == request.ShowTimeId);
            if (st == null) return NotFound(new { message = "Suất chiếu không tồn tại!" });

            var distinctSeatIds = request.SeatIds.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).Distinct().ToList();
            if (distinctSeatIds.Count == 0) return BadRequest(new { message = "Danh sách ghế không hợp lệ!" });

            var now = DateTime.Now;
            var blocked = await _context.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == request.ShowTimeId &&
                            distinctSeatIds.Contains(t.SeatId) &&
                            (t.Status == 2 || (t.Status == 1 && t.Expire != null && t.Expire > now)))
                .Select(t => t.SeatId)
                .ToListAsync();
            if (blocked.Count > 0)
                return Conflict(new { message = "Một số ghế đã được giữ/đã bán.", blockedSeatIds = blocked });

            // ===== meta (FIX) =====
            static string NormalizePath(string p)
            {
                p = (p ?? "").Trim();
                if (string.IsNullOrWhiteSpace(p)) return "";
                if (p.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return p;
                return p.StartsWith("/") ? p : "/" + p;
            }

            string cinemaName = "";
            string screenName = "";
            string movieTitle = "";
            string poster = "";
            string genres = "";
            string ageRating = "";
            int? durationMinutes = null;
            string movieId = "";

            // NOTE: tuyệt đối KHÔNG gọi NormalizePath trong select
            var joined = await (from x in _context.ShowTimes.AsNoTracking()
                                join room in _context.CinemaTheaters.AsNoTracking()
                                    on x.CinemaTheaterId equals room.CinemaTheaterId
                                join theater in _context.MovieTheaters.AsNoTracking()
                                    on room.MovieTheaterId equals theater.MovieTheaterId into th
                                from theater in th.DefaultIfEmpty()
                                join mv in _context.Movies.AsNoTracking()
                                    on x.MoviesId equals mv.MoviesId into mvj
                                from mv in mvj.DefaultIfEmpty()
                                where x.ShowTimeId == request.ShowTimeId
                                select new
                                {
                                    cinemaName = theater != null ? (theater.Name ?? "") : "",
                                    screenName = room != null ? (room.Name ?? "") : "",
                                    movieId = mv != null ? (mv.MoviesId ?? "") : "",
                                    movieTitle = mv != null ? (mv.Title ?? "") : "",
                                    posterRaw = mv != null ? (mv.PosterImage ?? "") : "",
                                    duration = mv != null ? mv.Duration : null,
                                    age = mv != null ? mv.Age : null
                                })
                                .FirstOrDefaultAsync();

            if (joined != null)
            {
                cinemaName = joined.cinemaName;
                screenName = joined.screenName;
                movieId = (joined.movieId ?? "").Trim();
                movieTitle = joined.movieTitle;
                poster = NormalizePath(joined.posterRaw); // normalize sau khi query xong
                durationMinutes = joined.duration;
                ageRating = (joined.age.HasValue && joined.age.Value > 0) ? $"T{joined.age.Value}" : "";
            }

            if (!string.IsNullOrWhiteSpace(movieId))
            {
                var gnames = await (from mg in _context.MoviesGenres.AsNoTracking()
                                    join g in _context.Genres.AsNoTracking()
                                        on mg.GenresId equals g.GenresId
                                    where mg.MoviesId == movieId
                                    select (g.Name ?? "").Trim())
                                   .Where(x => x != "")
                                   .Distinct()
                                   .ToListAsync();

                genres = gnames.Any() ? string.Join(", ", gnames) : "";
            }

            // ===== seats =====
            decimal priceAdjustmentPercent = st.PriceAdjustmentPercent ?? 0m;

            var seatLines = await (from s in _context.Seats.AsNoTracking()
                                   join t in _context.SeatTypes.AsNoTracking()
                                       on s.SeatTypeId equals t.SeatTypeId into tg
                                   from t in tg.DefaultIfEmpty()
                                   where distinctSeatIds.Contains(s.SeatId) && !s.IsDeleted
                                   select new
                                   {
                                       seatId = s.SeatId,
                                       label = s.Label ?? "",
                                       seatTypeName = t != null ? (t.Name ?? "") : "",
                                       unitPrice = Math.Round(
                                           ((t != null ? (t.Price ?? 0m) : 0m) * (1 + priceAdjustmentPercent / 100m)),
                                           0, MidpointRounding.AwayFromZero)
                                   })
                                   .ToListAsync();

            decimal seatsTotal = seatLines.Sum(x => (decimal)x.unitPrice);

            // ===== snacks =====
            var snackReq = request.Snacks ?? new List<SnackReq>();
            var snackIds = snackReq.Select(x => x.SnackId).Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).Distinct().ToList();

            var snackDict = await _context.Snacks.AsNoTracking()
                .Where(s => snackIds.Contains(s.SnackId) && s.IsActive == true)
                .ToDictionaryAsync(s => s.SnackId, s => new { name = s.Name ?? "", price = (decimal)(s.Price ?? 0m), image = s.Image ?? "" });

            var snackLines = new List<object>();
            decimal snacksTotal = 0m;
            foreach (var item in snackReq)
            {
                if (string.IsNullOrWhiteSpace(item.SnackId)) continue;
                var id = item.SnackId.Trim();
                if (!snackDict.TryGetValue(id, out var sn)) continue;

                var qty = Math.Max(0, item.Quantity);
                if (qty == 0) continue;

                var lineTotal = sn.price * qty;
                snacksTotal += lineTotal;

                snackLines.Add(new
                {
                    snackId = id,
                    name = sn.name,
                    image = sn.image,
                    unitPrice = sn.price,
                    quantity = qty,
                    lineTotal
                });
            }

            var originalTotal = seatsTotal + snacksTotal;

            // ===== promo =====
            string promoCode = request.PromoCode?.Trim() ?? "";
            decimal discountAmount = 0m;
            decimal totalAfterDiscount = originalTotal;
            string promotionName = "-";
            decimal? discountPercent = null;

            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                var promoRes = await ValidatePromotionInternalAsync(promoCode, originalTotal);
                if (promoRes.Success)
                {
                    discountAmount = promoRes.DiscountAmount;
                    totalAfterDiscount = promoRes.TotalAfterDiscount;
                    promotionName = promoRes.PromotionName;
                    discountPercent = promoRes.DiscountPercent;
                }
            }

            return Ok(new
            {
                showTimeId = request.ShowTimeId,
                meta = new
                {
                    cinemaName,
                    screenName,
                    showDate = st.ShowDate,
                    startTime = st.StartTime,
                    endTime = st.EndTime,
                    movieTitle,
                    poster,
                    genres,
                    ageRating,
                    durationMinutes
                },
                seats = seatLines,
                snacks = snackLines,
                priceAdjustmentPercent,
                seatsTotal,
                snacksTotal,
                originalTotal,
                promo = new
                {
                    code = string.IsNullOrWhiteSpace(promoCode) ? null : promoCode,
                    promotionName,
                    discountPercent,
                    discountAmount
                },
                totalAfterDiscount
            });
            static string NormalizePathOnly(string p)
            {
                p = (p ?? "").Trim();
                if (string.IsNullOrWhiteSpace(p)) return "";
                if (p.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return p;
                return p.StartsWith("/") ? p : "/" + p;
            }

            string ToAbsoluteUrl(string p)
            {
                p = NormalizePathOnly(p);
                if (string.IsNullOrWhiteSpace(p)) return "";
                if (p.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return p;

                // Ghép host hiện tại của API
                return $"{Request.Scheme}://{Request.Host}{p}";
            }

        }

        // =========================
        // POST: /api/bookings/create-invoice
        // =========================
        [HttpPost("bookings/create-invoice")]
        public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ShowTimeId) || request.SeatIds == null || request.SeatIds.Count == 0)
                return BadRequest(new { message = "Dữ liệu không hợp lệ!" });

            var uid = (request.UserId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(uid))
                return Unauthorized(new { message = "Thiếu userId." });

            var currentUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => (u.UserId ?? "").Trim() == uid);

            if (currentUser == null)
                return Unauthorized(new { message = "Không tìm thấy tài khoản." });

            var st = await _context.ShowTimes.AsNoTracking().FirstOrDefaultAsync(x => x.ShowTimeId == request.ShowTimeId);
            if (st == null) return NotFound(new { message = "Suất chiếu không tồn tại!" });

            var distinctSeatIds = request.SeatIds.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).Distinct().ToList();
            if (distinctSeatIds.Count == 0) return BadRequest(new { message = "Danh sách ghế không hợp lệ!" });

            // block check
            var now = DateTime.Now;
            var blocked = await _context.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == request.ShowTimeId &&
                            distinctSeatIds.Contains(t.SeatId) &&
                            (t.Status == 2 || (t.Status == 1 && t.Expire != null && t.Expire > now)))
                .Select(t => t.SeatId)
                .ToListAsync();
            if (blocked.Count > 0)
                return Conflict(new { message = "Một số ghế đã được giữ/đã bán.", blockedSeatIds = blocked });

            decimal priceAdjustmentPercent = st.PriceAdjustmentPercent ?? 0m;

            var seatTypePrices = await _context.Seats.AsNoTracking()
                .Where(s => distinctSeatIds.Contains(s.SeatId) && !s.IsDeleted)
                .Join(_context.SeatTypes.AsNoTracking(),
                    s => s.SeatTypeId,
                    t => t.SeatTypeId,
                    (s, t) => new { s.SeatId, BasePrice = t.Price })
                .ToListAsync();

            decimal baseTotal = 0m;
            foreach (var seat in seatTypePrices)
            {
                decimal basePrice = seat.BasePrice ?? 0m;
                decimal adjusted = basePrice * (1 + priceAdjustmentPercent / 100m);
                adjusted = Math.Round(adjusted, 0, MidpointRounding.AwayFromZero);
                baseTotal += adjusted;
            }

            if (request.Snacks?.Any() == true)
            {
                var reqSnackIds = request.Snacks.Select(x => x.SnackId).Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim()).Distinct().ToList();

                var snacks = await _context.Snacks.AsNoTracking()
                    .Where(s => reqSnackIds.Contains(s.SnackId) && s.IsActive == true)
                    .ToDictionaryAsync(s => s.SnackId, s => (decimal)(s.Price ?? 0m));

                foreach (var s in request.Snacks)
                {
                    if (string.IsNullOrWhiteSpace(s.SnackId)) continue;
                    var id = s.SnackId.Trim();
                    if (snacks.TryGetValue(id, out var p))
                        baseTotal += p * Math.Max(0, s.Quantity);
                }
            }

            decimal discountAmount = 0m;
            decimal payableTotal = baseTotal;
            string? promoIdToSave = null;

            if (!string.IsNullOrWhiteSpace(request.PromoCode))
            {
                var code = request.PromoCode.Trim();
                var promoCheck = await ValidatePromotionInternalAsync(code, baseTotal);
                if (promoCheck.Success)
                {
                    discountAmount = promoCheck.DiscountAmount;
                    payableTotal = promoCheck.TotalAfterDiscount;

                    var norm = code.ToLower();
                    var promoEntity = await _context.Promotion.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Code != null && p.Code.Trim().ToLower() == norm);
                    if (promoEntity != null) promoIdToSave = promoEntity.PromotionId;
                }
            }

            var invoiceId = await GenerateInvoiceIdAsync();
            var utcNow = DateTime.UtcNow;

            var invoice = new Invoices
            {
                InvoiceId = invoiceId,
                StaffId = null,
                CustomerId = currentUser.UserId,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                TotalTicket = distinctSeatIds.Count,
                OriginalTotal = baseTotal,
                TotalPrice = payableTotal,
                PromotionId = promoIdToSave,
                Status = 0
            };

            _context.Invoices.Add(invoice);

            var expire = DateTime.Now.AddMinutes(10);
            foreach (var seatId in distinctSeatIds)
            {
                _context.Tickets.Add(new Tickets
                {
                    TicketId = Guid.NewGuid().ToString("N"),
                    InvoiceId = invoiceId,
                    ShowTimeId = request.ShowTimeId,
                    SeatId = seatId,
                    Status = 1,
                    Expire = expire
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                invoiceId,
                originalTotal = baseTotal,
                discountAmount,
                totalPrice = payableTotal
            });
        }

        // =========================
        // DTOs
        // =========================
        public class SnackReq
        {
            public string SnackId { get; set; } = default!;
            public int Quantity { get; set; }
        }

        public class PreviewRequest
        {
            public string ShowTimeId { get; set; } = default!;
            public List<string> SeatIds { get; set; } = new();
            public List<SnackReq>? Snacks { get; set; }
            public string? PromoCode { get; set; }
        }

        public class CreateInvoiceRequest
        {
            public string UserId { get; set; }
            public string ShowTimeId { get; set; } = default!;
            public List<string> SeatIds { get; set; } = new();
            public List<SnackReq>? Snacks { get; set; }
            public string? PromoCode { get; set; }
        }

        // =========================
        // Helpers (giữ nguyên)
        // =========================
        private static DateTime NowVn()
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }

        private static (bool ok, double percent) NormalizeDiscountPercent(double raw)
        {
            if (raw <= 0 || raw > 100) return (false, 0);
            return (true, raw);
        }

        private async Task<string> GenerateInvoiceIdAsync()
        {
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
            var today = vnTime.Date;

            var todayCount = await _context.Invoices.AsNoTracking()
                .Where(i => i.CreatedAt.HasValue && i.CreatedAt.Value.Date == today)
                .CountAsync();

            int seq = todayCount + 1;
            return $"INV{seq:D4}_{vnTime:HH}h{vnTime:mm}m{vnTime:dd}{vnTime:MM}{vnTime:yyyy}";
        }

        private async Task<PromotionValidateResult> ValidatePromotionInternalAsync(string code, decimal amount)
        {
            if (string.IsNullOrWhiteSpace(code) || amount <= 0)
            {
                return new PromotionValidateResult
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ.",
                    PromotionName = "-",
                    DiscountPercent = null,
                    DiscountAmount = 0m,
                    TotalAfterDiscount = amount
                };
            }

            var normCode = code.Trim();

            var promo = await _context.Promotion.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code != null && p.Code.Trim().ToLower() == normCode.ToLower());

            if (promo == null)
            {
                return new PromotionValidateResult
                {
                    Success = false,
                    Message = "Mã khuyến mãi không tồn tại.",
                    PromotionName = "-",
                    DiscountPercent = null,
                    DiscountAmount = 0m,
                    TotalAfterDiscount = amount
                };
            }

            var nowVn = NowVn();

            if (promo.Status != true)
            {
                return new PromotionValidateResult
                {
                    Success = false,
                    Message = "Mã khuyến mãi không còn hoạt động.",
                    PromotionName = promo.Name ?? "-",
                    DiscountPercent = null,
                    DiscountAmount = 0m,
                    TotalAfterDiscount = amount
                };
            }

            if (promo.StartDay.HasValue && nowVn < promo.StartDay.Value)
            {
                return new PromotionValidateResult
                {
                    Success = false,
                    Message = "Mã khuyến mãi chưa đến ngày áp dụng.",
                    PromotionName = promo.Name ?? "-",
                    DiscountPercent = null,
                    DiscountAmount = 0m,
                    TotalAfterDiscount = amount
                };
            }

            if (promo.EndDay.HasValue && nowVn > promo.EndDay.Value)
            {
                return new PromotionValidateResult
                {
                    Success = false,
                    Message = "Mã khuyến mãi đã hết hạn.",
                    PromotionName = promo.Name ?? "-",
                    DiscountPercent = null,
                    DiscountAmount = 0m,
                    TotalAfterDiscount = amount
                };
            }

            if (!promo.Discount.HasValue)
            {
                return new PromotionValidateResult
                {
                    Success = false,
                    Message = "Mã khuyến mãi thiếu giá trị giảm.",
                    PromotionName = promo.Name ?? "-",
                    DiscountPercent = null,
                    DiscountAmount = 0m,
                    TotalAfterDiscount = amount
                };
            }

            var (ok, percent) = NormalizeDiscountPercent((double)promo.Discount.Value);
            if (!ok)
            {
                return new PromotionValidateResult
                {
                    Success = false,
                    Message = "Giá trị giảm không hợp lệ (phải trong (0..100]).",
                    PromotionName = promo.Name ?? "-",
                    DiscountPercent = null,
                    DiscountAmount = 0m,
                    TotalAfterDiscount = amount
                };
            }

            decimal discountAmount = Math.Round(amount * (decimal)percent / 100m, 0, MidpointRounding.AwayFromZero);
            decimal after = amount - discountAmount;
            if (after < 0) after = 0;

            return new PromotionValidateResult
            {
                Success = true,
                Message = "Mã hợp lệ.",
                PromotionName = promo.Name ?? "-",
                DiscountPercent = (decimal)percent,
                DiscountAmount = discountAmount,
                TotalAfterDiscount = after
            };
        }

        private class PromotionValidateResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public string PromotionName { get; set; } = "-";
            public decimal? DiscountPercent { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal TotalAfterDiscount { get; set; }
        }
    }
}
