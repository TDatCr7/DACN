
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
        private async Task<string?> GetCurrentAdminStaffIdAsync()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email)) return null;

            // Ưu tiên dbo.Users.UserId (thường là mã 10 ký tự)
            var staff = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());

            if (staff != null && !string.IsNullOrWhiteSpace(staff.UserId))
                return staff.UserId;

            // Fallback AspNetUsers.Id
            var adminAppUser = await _userManager.GetUserAsync(User);
            return adminAppUser?.Id;
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
                    IsAisle = s.IsAisle,
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

            // ===== Lấy StaffId = admin đang đăng nhập =====
            var staffId = await GetCurrentAdminStaffIdAsync();
            if (string.IsNullOrWhiteSpace(staffId))
                return Json(new { success = false, message = "Không xác định được mã nhân viên (StaffId)." });


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

                StaffId = staffId,
                CustomerId = invoiceOwner.UserId,

                Email = invoiceOwner.Email,
                PhoneNumber = invoiceOwner.PhoneNumber,

                CreatedAt = now,
                UpdatedAt = now,
                TotalTicket = 0,

                // lưu giá gốc để tính điểm
                OriginalTotal = total,

                // TotalPrice là số tiền hiện tại (chưa giảm ở bước tạo)
                TotalPrice = total,
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

        /* ===================== ADMIN - CHECK CUSTOMER ACCOUNT ===================== */

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> CheckCustomerAccount(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Json(new { exists = false, message = "Vui lòng nhập thông tin" });

            key = key.Trim().ToLower();

            // 1) Tìm trong bảng dbo.Users (bảng tích điểm)
            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (u.PhoneNumber != null && u.PhoneNumber.ToLower() == key) ||
                    (u.Email != null && u.Email.ToLower() == key));

            if (user != null)
            {
                int? age = null;
                if (user.DateOfBirth.HasValue)
                {
                    age = DateTime.Now.Year - user.DateOfBirth.Value.Year;
                    if (user.DateOfBirth.Value.Date > DateTime.Now.AddYears(-age.Value).Date)
                        age--;
                }

                bool isAdmin = false;
                if (!string.IsNullOrEmpty(user.Email))
                {
                    var appUser = await _userManager.FindByEmailAsync(user.Email);
                    if (appUser != null)
                        isAdmin = await _userManager.IsInRoleAsync(appUser, "Admin");
                }

                return Json(new
                {
                    exists = true,
                    message = $"Tìm thấy tài khoản: {user.FullName ?? user.Email}",
                    userId = user.UserId,
                    fullName = user.FullName ?? "",
                    email = user.Email ?? "",
                    phone = user.PhoneNumber ?? "",
                    address = user.Address ?? "",
                    age = age,
                    dateOfBirth = user.DateOfBirth?.ToString("dd/MM/yyyy"),
                    isAdmin = isAdmin
                });
            }

            // 2) Fallback: có thể tồn tại trong AspNetUsers nhưng chưa có trong dbo.Users
            var appUserOnly = await _userManager.Users.AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (u.PhoneNumber != null && u.PhoneNumber.ToLower() == key) ||
                    (u.Email != null && u.Email.ToLower() == key));

            if (appUserOnly != null)
            {
                bool isAdmin = await _userManager.IsInRoleAsync(appUserOnly, "Admin");

                int? age = null;
                if (!string.IsNullOrWhiteSpace(appUserOnly.Age) &&
                    int.TryParse(appUserOnly.Age, out var parsedAge))
                {
                    age = parsedAge;
                }

                return Json(new
                {
                    exists = true,
                    message = $"Tìm thấy tài khoản: {appUserOnly.FullName ?? appUserOnly.Email}",
                    userId = appUserOnly.Id,
                    fullName = appUserOnly.FullName ?? "",
                    email = appUserOnly.Email ?? "",
                    phone = appUserOnly.PhoneNumber ?? "",
                    address = appUserOnly.Address ?? "",
                    age = age,
                    isAdmin = isAdmin
                });
            }

            return Json(new
            {
                exists = false,
                message = "Không tìm thấy tài khoản với thông tin này"
            });
        }
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BookSeats([FromBody] AdminBookSeatRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.ShowTimeId) ||
                request.SeatIds == null ||
                !request.SeatIds.Any())
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            var staffId = await GetCurrentAdminStaffIdAsync();
            if (string.IsNullOrWhiteSpace(staffId))
                return Json(new { success = false, message = "Không xác định được mã nhân viên (StaffId)." });

            var userEmail = User.Identity?.Name;
            var currentUser = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (currentUser == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản đang đăng nhập." });

            var showTimeId = request.ShowTimeId.Trim();

            var showTime = await _db.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(st => st.ShowTimeId == showTimeId);

            if (showTime == null)
                return Json(new { success = false, message = "Suất chiếu không tồn tại!" });

            var distinctSeatIds = request.SeatIds
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToList();

            if (!distinctSeatIds.Any())
                return Json(new { success = false, message = "Danh sách ghế không hợp lệ!" });

            // Chỉ khóa ghế đã thanh toán (Status = 2)
            var paidSeatIds = await _db.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == showTimeId && t.Status == 2)
                .Select(t => t.SeatId)
                .ToListAsync();

            var paidSet = new HashSet<string>(paidSeatIds);
            if (distinctSeatIds.Any(id => paidSet.Contains(id)))
                return Json(new { success = false, message = "Có ghế đã được thanh toán. Vui lòng tải lại và chọn ghế khác." });

            // ===== xác định customer để lưu hoá đơn =====
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
                // để trống => lưu vào tài khoản admin đang đăng nhập
                loyaltyUser = currentUser;
            }

            var invoiceOwner = loyaltyUser ?? currentUser;

            // ===== baseTotal: tính ghế =====
            var seatTypePrices = await _db.Seats.AsNoTracking()
                .Where(s => distinctSeatIds.Contains(s.SeatId) && !s.IsDeleted)
                .Join(_db.SeatTypes.AsNoTracking(),
                    s => s.SeatTypeId,
                    t => t.SeatTypeId,
                    (s, t) => new { s.SeatId, t.Price })
                .ToListAsync();

            decimal baseTotal = seatTypePrices.Sum(x => Convert.ToDecimal(x.Price ?? 0m));

            // ===== cộng snack =====
            var snacksReq = request.Snacks ?? new List<AdminSnackRequest>();
            if (snacksReq.Any())
            {
                var reqSnackIds = snacksReq.Select(x => x.SnackId).Distinct().ToList();

                var snacks = await _db.Snacks.AsNoTracking()
                    .Where(s => reqSnackIds.Contains(s.SnackId))
                    .ToDictionaryAsync(s => s.SnackId, s => (decimal)(s.Price ?? 0m));

                foreach (var s in snacksReq)
                {
                    if (snacks.TryGetValue(s.SnackId, out var p))
                        baseTotal += p * Math.Max(1, s.Quantity);
                }
            }

            var invoiceId = await GenerateInvoiceIdAsync();
            var now = DateTime.UtcNow;

            var invoice = new Invoices
            {
                InvoiceId = invoiceId,

                // admin booking: staff = admin, customer = người nhập (hoặc admin nếu để trống)
                StaffId = staffId,
                CustomerId = invoiceOwner.UserId,

                Email = invoiceOwner.Email,
                PhoneNumber = invoiceOwner.PhoneNumber,

                CreatedAt = now,
                UpdatedAt = now,

                TotalTicket = distinctSeatIds.Count,

                // Lưu giá gốc để lịch sử/điểm (đúng yêu cầu OriginalTotal)
                OriginalTotal = baseTotal,

                // Chưa áp promo trong bước này (promo sẽ ApplyPromotionToInvoice)
                TotalPrice = baseTotal,
                PromotionId = null,

                Status = 0
            };

            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync();

            // ===== lưu pending giống BookingController để Payment xử lý =====
            var payload = new PendingSelection
            {
                ShowTimeId = showTimeId,
                SeatIds = distinctSeatIds,
                Snacks = snacksReq
            };

            HttpContext.Session.SetString(
                $"pending:{invoiceId}",
                JsonSerializer.Serialize(payload));

            return Json(new
            {
                success = true,
                invoiceId,
                totalPrice = invoice.TotalPrice,
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

                invoice.TotalPrice = (invoice.OriginalTotal != null && invoice.OriginalTotal > 0)
                ? invoice.OriginalTotal
                : baseTotal;


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

            // Lưu tổng gốc lần đầu (chỉ set nếu chưa có)
            if (invoice.OriginalTotal == null || invoice.OriginalTotal <= 0)
            {
                invoice.OriginalTotal = baseTotal;
            }

            invoice.PromotionId = promo.PromotionId;

            // TotalPrice phải là giá phải trả sau giảm
            invoice.TotalPrice = totalAfterDiscount;

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
        public class AdminBookSeatRequest
        {
            public string ShowTimeId { get; set; } = default!;
            public List<string> SeatIds { get; set; } = new();
            public List<AdminSnackRequest>? Snacks { get; set; }
            public string? LoyaltyKey { get; set; }
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
        private class PendingSelection
        {
            public string ShowTimeId { get; set; } = default!;
            public List<string> SeatIds { get; set; } = new();
            public List<AdminSnackRequest> Snacks { get; set; } = new();
        }

    }
}
