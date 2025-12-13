using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using CinemaS.Models;
using CinemaS.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly CinemaContext _db;
        private readonly UserManager<AppUser> _userManager;

        private static readonly TimeZoneInfo _vnTz =
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public AdminController(CinemaContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Admin"))
                return RedirectToAction("Index", "Home");

            return View();
        }

        /* ===================== ADMIN BOOKING - ĐẶT VÉ HỘ ===================== */

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminBooking(string showTimeId)
        {
            if (string.IsNullOrWhiteSpace(showTimeId))
                return BadRequest("Vui lòng chọn suất chiếu!");

            var showTime = await _db.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShowTimeId == showTimeId);

            if (showTime == null)
                return NotFound("Suất chiếu không tồn tại!");

            var movie = await _db.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == showTime.MoviesId);

            var theater = await _db.CinemaTheaters.AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.CinemaTheaterId == showTime.CinemaTheaterId);

            if (movie == null || theater == null)
                return NotFound();

            // Chỉ khoá ghế đã thanh toán (Status = 2)
            var paidSeatIds = await _db.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == showTimeId && t.Status == 2)
                .Select(t => t.SeatId)
                .ToListAsync();

            // Chỉ load ghế chưa bị xóa
            var seats = await _db.Seats.AsNoTracking()
                .Where(s => s.CinemaTheaterId == theater.CinemaTheaterId && !s.IsDeleted)
                .OrderBy(s => s.RowIndex)
                .ThenBy(s => s.ColumnIndex)
                .ToListAsync();

            var seatTypes = await _db.SeatTypes.AsNoTracking().ToListAsync();

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
                    PairId = s.PairId
                };
            }).ToList();

            ViewBag.Snacks = await _db.Snacks.AsNoTracking()
                .Where(s => s.IsActive == true)
                .ToListAsync();

            var vm = new AdminBookingVM
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

            return View("~/Views/Admin/AdminBooking.cshtml", vm);
        }

        /* ===================== ADMIN SNACKS ===================== */

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminSnacks()
        {
            ViewBag.Snacks = await _db.Snacks.AsNoTracking()
                .Where(s => s.IsActive)
                .ToListAsync();

            return View("~/Views/Admin/AdminSnacks.cshtml");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AdminBookSnacks([FromBody] AdminBookSnacksRequest request)
        {
            if (request == null || request.Snacks == null || !request.Snacks.Any())
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 món!" });

            var userEmail = User.Identity?.Name;
            var currentUser = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (currentUser == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản đang đăng nhập." });

            string? loyaltyKey = request.LoyaltyKey?.Trim().ToLower();
            CinemaS.Models.Users? loyaltyUser = null;
            bool memberNotFound = false;

            if (!string.IsNullOrWhiteSpace(loyaltyKey))
            {
                loyaltyUser = await _db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        (u.PhoneNumber != null && u.PhoneNumber.ToLower() == loyaltyKey) ||
                        (u.Email != null && u.Email.ToLower() == loyaltyKey));

                if (loyaltyUser == null)
                    memberNotFound = true;
            }
            else
            {
                loyaltyUser = currentUser;
            }

            var invoiceOwner = loyaltyUser ?? currentUser;

            var reqSnackIds = request.Snacks.Select(x => x.SnackId).Distinct().ToList();
            var snacks = await _db.Snacks.AsNoTracking()
                .Where(s => reqSnackIds.Contains(s.SnackId))
                .ToDictionaryAsync(s => s.SnackId, s => (decimal)(s.Price ?? 0m));

            decimal total = 0m;
            foreach (var s in request.Snacks)
            {
                if (snacks.TryGetValue(s.SnackId, out var p))
                    total += p * Math.Max(1, s.Quantity);
            }

            var invoiceId = await GenerateInvoiceIdAsync();
            var now = DateTime.UtcNow;

            var invoice = new Invoices
            {
                InvoiceId = invoiceId,
                CustomerId = invoiceOwner.UserId,
                Email = invoiceOwner.Email,
                PhoneNumber = invoiceOwner.PhoneNumber,
                CreatedAt = now,
                UpdatedAt = now,
                TotalTicket = 0,
                TotalPrice = total, // baseTotal (chưa áp promotion)
                Status = 0,
                PromotionId = null
            };

            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync();

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
                totalPrice = total,
                memberNotFound
            });
        }

        /* ===================== PROMOTION (ADMIN) ===================== */

        public class PromoValidateRequest
        {
            public string Code { get; set; } = default!;
            public decimal Amount { get; set; }
        }

        public class ApplyPromoToInvoiceRequest
        {
            public string InvoiceId { get; set; } = default!;
            public string? Code { get; set; }
        }

        private static decimal GetDiscountPercentForDisplay(double discountValue)
        {
            if (discountValue <= 0d) return 0m;
            if (discountValue <= 1d) return (decimal)discountValue * 100m;     // 0.5 => 50%
            if (discountValue <= 100d) return (decimal)discountValue;          // 50  => 50%
            return 0m;                                                         // >100 là trừ thẳng tiền, không có %
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ValidatePromotion([FromBody] PromoValidateRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Code))
                return Json(new { success = false, message = "Vui lòng nhập mã khuyến mãi." });

            if (req.Amount <= 0)
                return Json(new { success = false, message = "Tổng tiền không hợp lệ." });

            var (ok, promo, msg) = await GetValidPromotionByCodeAsync(req.Code);
            if (!ok || promo == null)
                return Json(new { success = false, message = msg });

            var discountValue = promo.Discount ?? 0d;
            var (discountAmount, totalAfterDiscount) = CalcDiscount(req.Amount, discountValue);
            var discountPercent = GetDiscountPercentForDisplay(discountValue);

            return Json(new
            {
                success = true,
                message = "Mã hợp lệ.",
                promotionId = promo.PromotionId,
                promotionName = promo.Name,
                discountPercent,
                discountAmount,
                totalAfterDiscount
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ApplyPromotionToInvoice([FromBody] ApplyPromoToInvoiceRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.InvoiceId))
                return Json(new { success = false, message = "Thiếu mã hóa đơn." });

            var invoiceId = req.InvoiceId.Trim();

            var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (invoice == null)
                return Json(new { success = false, message = "Hóa đơn không tồn tại." });

            if (invoice.Status != 0)
                return Json(new { success = false, message = "Hóa đơn không ở trạng thái chờ thanh toán." });

            var baseTotal = await GetInvoiceBaseTotalAsync(invoiceId, invoice.TotalPrice);

            // Gỡ mã
            if (string.IsNullOrWhiteSpace(req.Code))
            {
                invoice.PromotionId = null;
                invoice.TotalPrice = baseTotal; // reset về base
                invoice.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Đã gỡ mã khuyến mãi.",
                    promotionId = (string?)null,
                    promotionName = (string?)null,
                    discountPercent = 0m,
                    discountAmount = 0m,
                    totalAfterDiscount = baseTotal
                });
            }

            var (ok, promo, msg) = await GetValidPromotionByCodeAsync(req.Code);
            if (!ok || promo == null)
                return Json(new { success = false, message = msg });

            var discountValue = promo.Discount ?? 0d;
            var (discountAmount, totalAfterDiscount) = CalcDiscount(baseTotal, discountValue);
            var discountPercent = GetDiscountPercentForDisplay(discountValue);

            invoice.PromotionId = promo.PromotionId;
            invoice.TotalPrice = baseTotal;                
            invoice.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã áp mã vào hóa đơn.",
                promotionId = promo.PromotionId,
                promotionName = promo.Name,
                discountPercent,
                discountAmount,
                totalAfterDiscount
            });
        }

        private static DateTime VnNow() =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vnTz);

        private async Task<(bool ok, Promotion? promo, string message)> GetValidPromotionByCodeAsync(string code)
        {
            var key = (code ?? string.Empty).Trim().ToUpper();
            if (string.IsNullOrWhiteSpace(key))
                return (false, null, "Mã khuyến mãi trống.");

            var promo = await _db.Promotion.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code != null && p.Code.Trim().ToUpper() == key);

            if (promo == null)
                return (false, null, "Mã khuyến mãi không tồn tại.");

            if (promo.Status != true)
                return (false, promo, "Mã khuyến mãi đang tắt.");

            var now = VnNow();

            if (promo.StartDay.HasValue && now < promo.StartDay.Value)
                return (false, promo, "Mã khuyến mãi chưa bắt đầu.");

            if (promo.EndDay.HasValue && now > promo.EndDay.Value)
                return (false, promo, "Mã khuyến mãi đã hết hạn.");

            if (!promo.Discount.HasValue)
                return (false, promo, "Mã khuyến mãi thiếu giá trị giảm.");

            return (true, promo, "OK");
        }

        // Quy ước xử lý Discount:
        // - 0 < Discount <= 1    => tỷ lệ (vd 0.1 = 10%)
        // - 1 < Discount <= 100  => % (vd 10 = 10%)
        // - Discount > 100       => số tiền trừ thẳng
        private static (decimal discountAmount, decimal totalAfterDiscount) CalcDiscount(decimal baseTotal, double discountValue)
        {
            if (baseTotal <= 0m || discountValue <= 0d)
                return (0m, baseTotal);

            decimal discount;
            if (discountValue <= 1d)
                discount = baseTotal * (decimal)discountValue;
            else if (discountValue <= 100d)
                discount = baseTotal * ((decimal)discountValue / 100m);
            else
                discount = (decimal)discountValue;

            discount = decimal.Round(discount, 0, MidpointRounding.AwayFromZero);
            if (discount < 0m) discount = 0m;
            if (discount > baseTotal) discount = baseTotal;

            return (discount, baseTotal - discount);
        }

        private async Task<decimal> GetInvoiceBaseTotalAsync(string invoiceId, decimal? fallbackTotalPrice)
        {
            decimal ticketSum = await _db.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .SumAsync(t => (decimal)(t.Price ?? 0));

            decimal snackSum = await _db.DetailBookingSnacks.AsNoTracking()
                .Where(s => s.InvoiceId == invoiceId)
                .SumAsync(s => (decimal)(s.TotalPrice ?? 0));

            var total = ticketSum + snackSum;
            if (total <= 0m && fallbackTotalPrice.HasValue && fallbackTotalPrice.Value > 0m)
                total = fallbackTotalPrice.Value;

            return total;
        }

        /* ===================== Helpers: InvoiceId ===================== */

        private async Task<string> GenerateInvoiceIdAsync()
        {
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vnTz);
            var today = vnTime.Date;

            var todayCount = await _db.Invoices.AsNoTracking()
                .Where(i => i.CreatedAt.HasValue && i.CreatedAt.Value.Date == today)
                .CountAsync();

            int sequenceNumber = todayCount + 1;

            return $"INV{sequenceNumber:D4}_{vnTime:HH}h{vnTime:mm}m{vnTime:dd}{vnTime:MM}{vnTime:yyyy}";
        }

        /* ===================== DTOs ===================== */

        public class AdminSnackRequest
        {
            public string SnackId { get; set; } = default!;
            public int Quantity { get; set; }
        }

        public class AdminBookSnacksRequest
        {
            public List<AdminSnackRequest> Snacks { get; set; } = new();
            public string? LoyaltyKey { get; set; }
        }

        private class PendingSnacksSelection
        {
            public List<AdminSnackRequest> Snacks { get; set; } = new();
        }
    }
}
