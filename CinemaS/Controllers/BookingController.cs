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

            var showTime = await _context.ShowTimes.AsNoTracking().FirstOrDefaultAsync(x => x.ShowTimeId == id);
            if (showTime == null) return NotFound();

            var movie = await _context.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.MoviesId == showTime.MoviesId);
            var theater = await _context.CinemaTheaters.AsNoTracking().FirstOrDefaultAsync(ct => ct.CinemaTheaterId == showTime.CinemaTheaterId);
            if (movie == null || theater == null) return NotFound();

            // Chỉ khoá ghế đã thanh toán (Status = 2)
            var paidSeatIds = await _context.Tickets.AsNoTracking()
                                  .Where(t => t.ShowTimeId == id && t.Status == 2)
                                  .Select(t => t.SeatId)
                                  .ToListAsync();

            // ✅ FIX: Filter ra ghế IsDeleted = true - chỉ load ghế chưa bị xóa
            var seats = await _context.Seats.AsNoTracking()
                            .Where(s => s.CinemaTheaterId == theater.CinemaTheaterId && !s.IsDeleted)
                            .OrderBy(s => s.RowIndex).ThenBy(s => s.ColumnIndex)
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
                    PairId = s.PairId
                };
            }).ToList();

            ViewBag.Snacks = await _context.Snacks.AsNoTracking().Where(s => s.IsActive == true).ToListAsync();

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
            return View(vm);
        }

        /* ============= Real-time status (chỉ ghế đã thanh toán) ============= */
        [HttpGet("GetSeatsStatus")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeatsStatus(string showTimeId)
        {
            var paid = await _context.Tickets.AsNoTracking()
                         .Where(t => t.ShowTimeId == showTimeId && t.Status == 2)
                         .Select(t => t.SeatId).ToListAsync();
            return Json(new { success = true, bookedSeats = paid });
        }

        /* ============= Chọn suất ============= */
        [HttpGet("Create")]
        [Authorize]
        public async Task<IActionResult> Create(string movieId)
        {
            if (string.IsNullOrWhiteSpace(movieId)) return BadRequest("Vui lòng chọn phim!");

            var movie = await _context.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.MoviesId == movieId);
            if (movie == null) return NotFound("Phim không tồn tại!");

            var today = DateTime.Today;
            var showTimes = await _context.ShowTimes.AsNoTracking()
                .Where(st => st.MoviesId == movieId && st.ShowDate >= today && st.CinemaTheaterId != null)
                .OrderBy(st => st.ShowDate).ThenBy(st => st.StartTime)
                .ToListAsync();

            var list = new List<ShowTimeVM>();
            foreach (var st in showTimes)
            {
                var cinema = await _context.CinemaTheaters.AsNoTracking()
                                  .FirstOrDefaultAsync(ct => ct.CinemaTheaterId == st.CinemaTheaterId);
                var totalSeats = await _context.Seats.AsNoTracking()
                                  .CountAsync(s => s.CinemaTheaterId == st.CinemaTheaterId);
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

        /* ============= BookSeats: tạo Invoice Pending + lưu Session ============= */
        [HttpPost("BookSeats")]
        [Authorize]
        public async Task<IActionResult> BookSeats([FromBody] BookSeatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ShowTimeId)
                || request.SeatIds == null || !request.SeatIds.Any())
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });

            var userEmail = User.Identity?.Name;

            // BẢNG KHÁCH HÀNG (Users), KHÔNG PHẢI AppUser
            var currentUser = await _context.Users.AsNoTracking()
                                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (currentUser == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản đang đăng nhập." });

            var showTime = await _context.ShowTimes.AsNoTracking()
                                .FirstOrDefaultAsync(st => st.ShowTimeId == request.ShowTimeId);
            if (showTime == null)
                return Json(new { success = false, message = "Suất chiếu không tồn tại!" });

            // ===== Xác định khách được tích điểm (loyaltyUser) =====
            // Nếu admin gõ SĐT/Email khách -> tìm user tương ứng
            // Nếu bỏ trống -> mặc định chính user đang đăng nhập
            string? loyaltyKey = request.LoyaltyKey?.Trim().ToLower(); // NORMALIZE: trim + lowercase

            CinemaS.Models.Users? loyaltyUser = null;
            bool memberNotFound = false;

            if (!string.IsNullOrWhiteSpace(loyaltyKey))
            {
                loyaltyUser = await _context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        (u.PhoneNumber != null && u.PhoneNumber.ToLower() == loyaltyKey) ||
                        (u.Email != null && u.Email.ToLower() == loyaltyKey));

                if (loyaltyUser == null)
                {
                    // vẫn cho đặt vé nhưng KHÔNG tích điểm
                    memberNotFound = true;
                }
            }
            else
            {
                loyaltyUser = currentUser;
            }

            // Cả loyaltyUser và currentUser đều là CinemaS.Models.Users
            var invoiceOwner = loyaltyUser ?? currentUser;

            // ===== TÍNH TỔNG TIỀN =====
            // tiền ghế
            var seatTypes = await _context.Seats.AsNoTracking()
                               .Where(s => request.SeatIds.Contains(s.SeatId))
                               .Join(_context.SeatTypes.AsNoTracking(),
                                     s => s.SeatTypeId, t => t.SeatTypeId,
                                     (s, t) => new { s.SeatId, t.Price })
                               .ToListAsync();

            decimal total = seatTypes.Sum(x => Convert.ToDecimal(x.Price ?? 0m));

            // tiền snack
            if (request.Snacks?.Any() == true)
            {
                var reqSnackIds = request.Snacks.Select(x => x.SnackId).Distinct().ToList();

                var snacks = await _context.Snacks.AsNoTracking()
                                  .Where(s => reqSnackIds.Contains(s.SnackId))
                                  .ToDictionaryAsync(s => s.SnackId, s => (decimal)(s.Price ?? 0m));

                foreach (var s in request.Snacks)
                {
                    if (snacks.TryGetValue(s.SnackId, out var p))
                    {
                        total += p * Math.Max(1, s.Quantity);
                    }
                }
            }

            // ===== TẠO HÓA ĐƠN PENDING =====
            var invoiceId = await GenerateInvoiceIdAsync();
            var now = DateTime.UtcNow;

            var invoice = new Invoices
            {
                InvoiceId = invoiceId,
                CustomerId = invoiceOwner.UserId,   // người đứng tên hóa đơn
                Email = invoiceOwner.Email,
                PhoneNumber = invoiceOwner.PhoneNumber,
                CreatedAt = now,
                UpdatedAt = now,
                TotalTicket = request.SeatIds.Count,
                TotalPrice = total,
                Status = 0,                         // Pending
                PaymentMethod = "Chưa thanh toán"
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // ===== LƯU LỰA CHỌN VÀO SESSION (chưa tạo Ticket ở đây) =====
            var payload = new PendingSelection
            {
                ShowTimeId = request.ShowTimeId,
                SeatIds = request.SeatIds,
                Snacks = request.Snacks ?? new List<SnackRequest>()
            };

            HttpContext.Session.SetString(
                $"pending:{invoiceId}",
                JsonSerializer.Serialize(payload));

            return Json(new
            {
                success = true,
                invoiceId,
                totalPrice = total,
                memberNotFound // để JS hiển thị cảnh báo "tài khoản tích điểm không tồn tại"
            });
        }

        /* ============= Snacks - Đặt riêng đồ ăn ============= */
        [HttpGet("Snacks")]
        [Authorize]
        public async Task<IActionResult> Snacks()
        {
            ViewBag.Snacks = await _context.Snacks.AsNoTracking()
                .Where(s => s.IsActive == true)
                .ToListAsync();

            return View();
        }

        /* ============= BookSnacks - Đặt riêng đồ ăn ============= */
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

            // ===== TÍNH TỔNG TIỀN =====
            var reqSnackIds = request.Snacks.Select(x => x.SnackId).Distinct().ToList();
            var snacks = await _context.Snacks.AsNoTracking()
                              .Where(s => reqSnackIds.Contains(s.SnackId))
                              .ToDictionaryAsync(s => s.SnackId, s => (decimal)(s.Price ?? 0m));

            decimal total = 0m;
            foreach (var s in request.Snacks)
            {
                if (snacks.TryGetValue(s.SnackId, out var p))
                {
                    total += p * Math.Max(1, s.Quantity);
                }
            }

            // ===== TẠO HÓA ĐƠN PENDING =====
            var invoiceId = await GenerateInvoiceIdAsync();
            var now = DateTime.UtcNow;

            var invoice = new Invoices
            {
                InvoiceId = invoiceId,
                CustomerId = currentUser.UserId,
                Email = currentUser.Email,
                PhoneNumber = currentUser.PhoneNumber,
                CreatedAt = now,
                UpdatedAt = now,
                TotalTicket = 0, // Không có vé, chỉ đồ ăn
                TotalPrice = total,
                Status = 0, // Pending
                PaymentMethod = "Chưa thanh toán"
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // ===== LƯU LỰA CHỌN VÀO SESSION =====
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
                totalPrice = total
            });
        }

        /* ============= Check customer account real-time ============= */
        [HttpGet("CheckCustomerAccount")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckCustomerAccount(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Json(new { exists = false, message = "Vui lòng nhập thông tin" });

            key = key.Trim().ToLower(); // Normalize: trim + lowercase

            // Bước 1: Tìm trong bảng Users (dbo.Users) - ưu tiên vì có đầy đủ thông tin
            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (u.PhoneNumber != null && u.PhoneNumber.ToLower() == key) ||
                    (u.Email != null && u.Email.ToLower() == key));

            if (user != null)
            {
                // Tính tuổi từ DateOfBirth
                int? age = null;
                if (user.DateOfBirth.HasValue)
                {
                    age = DateTime.Now.Year - user.DateOfBirth.Value.Year;
                    if (user.DateOfBirth.Value.Date > DateTime.Now.AddYears(-age.Value).Date)
                        age--;
                }

                // Kiểm tra xem user có phải Admin không (từ AspNetUsers)
                bool isAdmin = false;
                if (!string.IsNullOrEmpty(user.Email))
                {
                    var appUser = await _userManager.FindByEmailAsync(user.Email);
                    if (appUser != null)
                    {
                        isAdmin = await _userManager.IsInRoleAsync(appUser, "Admin");
                    }
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

            // Bước 2: Nếu không tìm thấy trong Users, tìm trong AspNetUsers
            var appUserOnly = await _userManager.Users.AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (u.PhoneNumber != null && u.PhoneNumber.ToLower() == key) ||
                    (u.Email != null && u.Email.ToLower() == key));

            if (appUserOnly != null)
            {
                // Kiểm tra role Admin
                bool isAdmin = await _userManager.IsInRoleAsync(appUserOnly, "Admin");

                // Tính tuổi từ Age field (nếu có)
                int? age = null;
                if (!string.IsNullOrWhiteSpace(appUserOnly.Age) && int.TryParse(appUserOnly.Age, out var parsedAge))
                {
                    age = parsedAge;
                }

                return Json(new
                {
                    exists = true,
                    message = $"Tìm thấy tài khoản: {appUserOnly.FullName ?? appUserOnly.Email}",
                    userId = appUserOnly.Id, // AspNetUsers dùng Id (GUID)
                    fullName = appUserOnly.FullName ?? "",
                    email = appUserOnly.Email ?? "",
                    phone = appUserOnly.PhoneNumber ?? "",
                    address = appUserOnly.Address ?? "",
                    isAdmin = isAdmin
                });
            }

            // Không tìm thấy ở cả 2 bảng
            return Json(new
            {
                exists = false,
                message = "Không tìm thấy tài khoản với thông tin này"
            });
        }

        /* ============= Test API - chỉ để debug ============= */
        [HttpGet("TestCheckAccount")]
        [AllowAnonymous]
        public async Task<IActionResult> TestCheckAccount(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Content("Vui lòng nhập key");

            key = key.Trim();

            var allUsers = await _context.Users.AsNoTracking()
                .Select(u => new { u.UserId, u.Email, u.PhoneNumber, u.FullName })
                .ToListAsync();

            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (u.PhoneNumber != null && u.PhoneNumber == key) ||
                    (u.Email != null && u.Email == key));

            var result = $@"
Key: {key}
All users in DB:
{string.Join("\n", allUsers.Select(u => $"{u.UserId}: {u.Email} / {u.PhoneNumber} / {u.FullName}"))}

Found user: {(user != null ? $"{user.UserId}: {user.Email} / {user.PhoneNumber} / {user.FullName}" : "NULL")}
";

            return Content(result);
        }

        /* ============= Helpers ============= */
        private async Task<string> GenerateInvoiceIdAsync()
        {
            // Get count of invoices today to determine sequence number
            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
            var today = vietnamTime.Date;

            var todayCount = await _context.Invoices.AsNoTracking()
                .Where(i => i.CreatedAt.HasValue && i.CreatedAt.Value.Date == today)
                .CountAsync();

            int sequenceNumber = todayCount + 1;

            string formatted = $"INV{sequenceNumber:D4}_{vietnamTime:HH}h{vietnamTime:mm}m{vietnamTime:dd}{vietnamTime:MM}{vietnamTime:yyyy}";

            return formatted;
        }

        public class BookSeatRequest
        {
            public string ShowTimeId { get; set; } = default!;
            public List<string> SeatIds { get; set; } = new();
            public List<SnackRequest>? Snacks { get; set; }

            // NEW – SĐT hoặc Email để tích điểm cho khách
            public string? LoyaltyKey { get; set; }
        }

        public class BookSnacksRequest
        {
            public List<SnackRequest> Snacks { get; set; } = new();
        }

        public class SnackRequest
        {
            public string SnackId { get; set; } = default!;
            public int Quantity { get; set; }
        }

        private class PendingSelection
        {
            public string ShowTimeId { get; set; } = default!;
            public List<string> SeatIds { get; set; } = new();
            public List<SnackRequest> Snacks { get; set; } = new();
        }

        private class PendingSnacksSelection
        {
            public List<SnackRequest> Snacks { get; set; } = new();
        }
    }
}
