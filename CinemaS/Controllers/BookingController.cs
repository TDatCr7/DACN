using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;
using CinemaS.Models.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace CinemaS.Controllers
{
    [Route("[controller]")]
    public class BookingController : Controller
    {
        private readonly CinemaContext _context;
        private readonly UserManager<AppUser> _userManager;

        public BookingController(CinemaContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /* ============= Seat selection ============= */
        [HttpGet("SeatSelection/{id}")]
        [Authorize]
        public async Task<IActionResult> SeatSelection(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var showTime = await _context.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShowTimeId == id);
            if (showTime == null) return NotFound();

            var movie = await _context.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == showTime.MoviesId);
            var theater = await _context.CinemaTheaters.AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.CinemaTheaterId == showTime.CinemaTheaterId);
            if (movie == null || theater == null) return NotFound();

            var paidSeatIds = await _context.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == id && t.Status == 2)
                .Select(t => t.SeatId)
                .ToListAsync();

            var seats = await _context.Seats.AsNoTracking()
                .Where(s => s.CinemaTheaterId == theater.CinemaTheaterId && !s.IsDeleted)
                .OrderBy(s => s.RowIndex)
                .ThenBy(s => s.ColumnIndex)
                .ToListAsync();

            var seatTypes = await _context.SeatTypes.AsNoTracking().ToListAsync();

            var seatVMs = seats.Select(s =>
            {
                var st = seatTypes.FirstOrDefault(x => x.SeatTypeId == s.SeatTypeId);
                return new SeatVM
                {
                    SeatId = s.SeatId,
                    SeatTypeId = s.SeatTypeId,
                    SeatTypeName = st?.Name,
                    SeatTypePrice = st?.Price,
                    RowIndex = s.RowIndex,
                    ColumnIndex = s.ColumnIndex,
                    Label = s.Label,
                    Status = paidSeatIds.Contains(s.SeatId) ? "Booked" : "Available",
                    IsCouple = string.Equals(st?.Name, "COUPLE", StringComparison.OrdinalIgnoreCase),
                    IsVIP = string.Equals(st?.Name, "VIP", StringComparison.OrdinalIgnoreCase),
                    IsActive = s.IsActive,
                    PairId = s.PairId,
                    IsAisle = s.IsAisle
                };
            }).ToList();

            ViewBag.Snacks = await _context.Snacks.AsNoTracking()
                .Where(s => s.IsActive)
                .ToListAsync();

            var vm = new SeatSelectionVM
            {
                ShowTimeId = showTime.ShowTimeId,
                MoviesId = movie.MoviesId,
                MovieTitle = movie.Title,
                MoviePoster = movie.PosterImage,
                CinemaTheaterName = theater.Name,
                ShowDate = showTime.ShowDate,
                StartTime = showTime.StartTime,
                EndTime = showTime.EndTime,
                Seats = seatVMs,
                NumOfRows = theater.NumOfRows ?? 6,
                NumOfColumns = theater.NumOfColumns ?? 14
            };
            return View("UserBooking", vm);
        }

        [HttpGet("GetSeatsStatus")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeatsStatus(string showTimeId)
        {
            if (string.IsNullOrWhiteSpace(showTimeId))
                return Json(new { success = false, bookedSeats = Array.Empty<string>() });

            var paid = await _context.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == showTimeId && t.Status == 2)
                .Select(t => t.SeatId)
                .ToListAsync();

            return Json(new { success = true, bookedSeats = paid });
        }

        [HttpPost("ValidatePromotion")]
        [Authorize]
        public async Task<IActionResult> ValidatePromotion([FromBody] PromotionValidateRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code) || request.Amount <= 0)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });

            var result = await ValidatePromotionInternalAsync(request.Code, request.Amount);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new
            {
                success = true,
                message = result.Message,
                promotionName = result.PromotionName,
                discountPercent = result.DiscountPercent,
                discountAmount = result.DiscountAmount,
                totalAfterDiscount = result.TotalAfterDiscount
            });
        }

        [HttpGet("Create")]
        [Authorize]
        public async Task<IActionResult> Create(string movieId)
        {
            if (string.IsNullOrWhiteSpace(movieId)) return BadRequest("Vui lòng chọn phim!");

            var movie = await _context.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == movieId);
            if (movie == null) return NotFound("Phim không tồn tại!");

            var today = DateTime.Today;
            var showTimes = await _context.ShowTimes.AsNoTracking()
                .Where(st => st.MoviesId == movieId && st.ShowDate >= today && st.CinemaTheaterId != null)
                .OrderBy(st => st.ShowDate)
                .ThenBy(st => st.StartTime)
                .ToListAsync();

            var list = new List<ShowTimeVM>();
            foreach (var st in showTimes)
            {
                var cinema = await _context.CinemaTheaters.AsNoTracking()
                    .FirstOrDefaultAsync(ct => ct.CinemaTheaterId == st.CinemaTheaterId);

                var totalSeats = await _context.Seats.AsNoTracking()
                    .CountAsync(s => s.CinemaTheaterId == st.CinemaTheaterId && !s.IsDeleted && !s.IsAisle);

                var paid = await _context.Tickets.AsNoTracking()
                    .CountAsync(t => t.ShowTimeId == st.ShowTimeId && t.Status == 2);

                list.Add(new ShowTimeVM
                {
                    ShowTimeId = st.ShowTimeId,
                    MovieTitle = movie.Title,
                    CinemaName = cinema?.Name ?? "Unknown",
                    ShowDate = st.ShowDate,
                    StartTime = st.StartTime,
                    EndTime = st.EndTime,
                    TotalSeats = totalSeats,
                    AvailableSeats = totalSeats - paid,
                    Price = st.OriginPrice ?? 75000
                });
            }

            ViewBag.Movie = movie;
            ViewBag.ShowTimes = list;
            return View(list);
        }

        /* ============= BookSeats: USER tạo Invoice Pending ============= */
        [HttpPost("BookSeats")]
        [Authorize]
        public async Task<IActionResult> BookSeats([FromBody] BookSeatRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.ShowTimeId) ||
                request.SeatIds == null ||
                !request.SeatIds.Any())
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            var userEmail = User.Identity?.Name;
            var currentUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (currentUser == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản đang đăng nhập." });

            var showTime = await _context.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(st => st.ShowTimeId == request.ShowTimeId);
            if (showTime == null)
                return Json(new { success = false, message = "Suất chiếu không tồn tại!" });

            var distinctSeatIds = request.SeatIds
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToList();

            if (!distinctSeatIds.Any())
                return Json(new { success = false, message = "Danh sách ghế không hợp lệ!" });

            // ===== baseTotal =====
            var seatTypes = await _context.Seats.AsNoTracking()
                .Where(s => distinctSeatIds.Contains(s.SeatId) && !s.IsDeleted)
                .Join(_context.SeatTypes.AsNoTracking(),
                    s => s.SeatTypeId,
                    t => t.SeatTypeId,
                    (s, t) => new { s.SeatId, t.Price })
                .ToListAsync();

            decimal baseTotal = seatTypes.Sum(x => Convert.ToDecimal(x.Price ?? 0m));

            if (request.Snacks?.Any() == true)
            {
                var reqSnackIds = request.Snacks.Select(x => x.SnackId).Distinct().ToList();

                var snacks = await _context.Snacks.AsNoTracking()
                    .Where(s => reqSnackIds.Contains(s.SnackId))
                    .ToDictionaryAsync(s => s.SnackId, s => (decimal)(s.Price ?? 0m));

                foreach (var s in request.Snacks)
                {
                    if (snacks.TryGetValue(s.SnackId, out var p))
                        baseTotal += p * Math.Max(1, s.Quantity);
                }
            }

            // ===== promo =====
            string? appliedPromoCode = null;
            decimal discountAmount = 0m;
            decimal payableTotal = baseTotal;
            string? promoIdToSave = null;

            if (!string.IsNullOrWhiteSpace(request.PromoCode))
            {
                var code = request.PromoCode.Trim();

                var promoCheck = await ValidatePromotionInternalAsync(code, baseTotal);
                if (promoCheck.Success)
                {
                    appliedPromoCode = code;
                    discountAmount = promoCheck.DiscountAmount;
                    payableTotal = promoCheck.TotalAfterDiscount;

                    var norm = code.Trim().ToLower();
                    var promoEntity = await _context.Promotion.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Code != null && p.Code.Trim().ToLower() == norm);

                    if (promoEntity != null)
                        promoIdToSave = promoEntity.PromotionId;
                }
            }

            var invoiceId = await GenerateInvoiceIdAsync();
            var now = DateTime.UtcNow;

            var invoice = new Invoices
            {
                InvoiceId = invoiceId,

                // ✅ USER booking: StaffId phải null
                StaffId = null,

                CustomerId = currentUser.UserId,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                CreatedAt = now,
                UpdatedAt = now,
                TotalTicket = distinctSeatIds.Count,

                OriginalTotal = baseTotal,
                TotalPrice = payableTotal,
                PromotionId = promoIdToSave,

                Status = 0
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            var payload = new PendingSelection
            {
                ShowTimeId = request.ShowTimeId,
                SeatIds = distinctSeatIds,
                Snacks = request.Snacks ?? new List<SnackRequest>(),
                PromoCode = appliedPromoCode,
                DiscountAmount = discountAmount
            };

            HttpContext.Session.SetString(
                $"pending:{invoiceId}",
                JsonSerializer.Serialize(payload));

            return Json(new
            {
                success = true,
                invoiceId,
                totalPrice = payableTotal
            });
        }

        [HttpGet("Snacks")]
        [Authorize]
        public async Task<IActionResult> Snacks()
        {
            ViewBag.Snacks = await _context.Snacks.AsNoTracking()
                .Where(s => s.IsActive)
                .ToListAsync();

            return View();
        }

        /* ============= BookSnacks: USER tạo Invoice Pending ============= */
        [HttpPost("BookSnacks")]
        [Authorize]
        public async Task<IActionResult> BookSnacks([FromBody] BookSnacksRequest request)
        {
            if (request == null || request.Snacks == null || !request.Snacks.Any())
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 món!" });

            var userEmail = User.Identity?.Name;
            var currentUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (currentUser == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản đang đăng nhập." });

            var reqSnackIds = request.Snacks.Select(x => x.SnackId).Distinct().ToList();
            var snacks = await _context.Snacks.AsNoTracking()
                .Where(s => reqSnackIds.Contains(s.SnackId))
                .ToDictionaryAsync(s => s.SnackId, s => (decimal)(s.Price ?? 0m));

            decimal baseTotal = 0m;
            foreach (var s in request.Snacks)
            {
                if (snacks.TryGetValue(s.SnackId, out var p))
                    baseTotal += p * Math.Max(1, s.Quantity);
            }

            string? appliedPromoCode = null;
            decimal discountAmount = 0m;
            decimal payableTotal = baseTotal;
            string? promoIdToSave = null;

            if (!string.IsNullOrWhiteSpace(request.PromoCode))
            {
                var code = request.PromoCode.Trim();
                var promoCheck = await ValidatePromotionInternalAsync(code, baseTotal);

                if (promoCheck.Success)
                {
                    appliedPromoCode = code;
                    discountAmount = promoCheck.DiscountAmount;
                    payableTotal = promoCheck.TotalAfterDiscount;

                    var norm = code.Trim().ToLower();
                    var promoEntity = await _context.Promotion.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Code != null && p.Code.Trim().ToLower() == norm);

                    if (promoEntity != null)
                        promoIdToSave = promoEntity.PromotionId;
                }
            }

            var invoiceId = await GenerateInvoiceIdAsync();
            var now = DateTime.UtcNow;

            var invoice = new Invoices
            {
                InvoiceId = invoiceId,

                // ✅ USER booking: StaffId phải null
                StaffId = null,

                CustomerId = currentUser.UserId,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                CreatedAt = now,
                UpdatedAt = now,
                TotalTicket = 0,

                OriginalTotal = baseTotal,
                TotalPrice = payableTotal,
                PromotionId = promoIdToSave,

                Status = 0
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            var payload = new PendingSnacksSelection
            {
                Snacks = request.Snacks
            };

            HttpContext.Session.SetString(
                $"pending_snacks:{invoiceId}",
                JsonSerializer.Serialize(payload));

            return Json(new
            {
                success = true,
                invoiceId,
                totalPrice = payableTotal
            });
        }

        /* ============= Helpers ============= */
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

        private async Task<CinemaS.Models.Users?> GetCurrentUserEntityAsync()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email)) return null;

            return await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        private async Task<string> GenerateInvoiceIdAsync()
        {
            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
            var today = vietnamTime.Date;

            var todayCount = await _context.Invoices.AsNoTracking()
                .Where(i => i.CreatedAt.HasValue && i.CreatedAt.Value.Date == today)
                .CountAsync();

            int sequenceNumber = todayCount + 1;

            return $"INV{sequenceNumber:D4}_{vietnamTime:HH}h{vietnamTime:mm}m{vietnamTime:dd}{vietnamTime:MM}{vietnamTime:yyyy}";
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

            var currentUser = await GetCurrentUserEntityAsync();
            if (currentUser == null)
            {
                return new PromotionValidateResult
                {
                    Success = false,
                    Message = "Không tìm thấy tài khoản đang đăng nhập.",
                    PromotionName = "-",
                    DiscountPercent = null,
                    DiscountAmount = 0m,
                    TotalAfterDiscount = amount
                };
            }

            var normCode = code.Trim();

            var promo = await _context.Promotion.AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Code != null &&
                    p.Code.Trim().ToLower() == normCode.ToLower());

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

        public class BookSeatRequest
        {
            public string ShowTimeId { get; set; } = default!;
            public List<string> SeatIds { get; set; } = new();
            public List<SnackRequest>? Snacks { get; set; }
            public string? PromoCode { get; set; }
        }

        public class BookSnacksRequest
        {
            public List<SnackRequest> Snacks { get; set; } = new();
            public string? PromoCode { get; set; }
        }

        public class SnackRequest
        {
            public string SnackId { get; set; } = default!;
            public int Quantity { get; set; }
        }

        public class PromotionValidateRequest
        {
            public string Code { get; set; } = default!;
            public decimal Amount { get; set; }
        }

        private class PendingSelection
        {
            public string ShowTimeId { get; set; } = default!;
            public List<string> SeatIds { get; set; } = new();
            public List<SnackRequest> Snacks { get; set; } = new();
            public string? PromoCode { get; set; }
            public decimal DiscountAmount { get; set; }
        }

        private class PendingSnacksSelection
        {
            public List<SnackRequest> Snacks { get; set; } = new();
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
