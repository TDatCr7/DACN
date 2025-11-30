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

namespace CinemaS.Controllers
{
    [Route("[controller]")]
    public class BookingController : Controller
    {
        private readonly CinemaContext _context;

        public BookingController(CinemaContext context)
        {
            _context = context;
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

            var seats = await _context.Seats.AsNoTracking()
                            .Where(s => s.CinemaTheaterId == theater.CinemaTheaterId)
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
            var customer = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == userEmail);
            if (customer == null) return Json(new { success = false, message = "Không tìm thấy khách hàng" });

            var showTime = await _context.ShowTimes.AsNoTracking().FirstOrDefaultAsync(st => st.ShowTimeId == request.ShowTimeId);
            if (showTime == null) return Json(new { success = false, message = "Suất chiếu không tồn tại!" });

            // tính tổng tiền ghế
            var seatTypes = await _context.Seats.AsNoTracking()
                               .Where(s => request.SeatIds.Contains(s.SeatId))
                               .Join(_context.SeatTypes.AsNoTracking(),
                                     s => s.SeatTypeId, t => t.SeatTypeId,
                                     (s, t) => new { s.SeatId, t.Price }).ToListAsync();
            decimal total = seatTypes.Sum(x => Convert.ToDecimal(x.Price ?? 0m));

            // cộng tiền snack
            if (request.Snacks?.Any() == true)
            {
                var reqSnackIds = request.Snacks.Select(x => x.SnackId).Distinct().ToList();
                var snacks = await _context.Snacks.AsNoTracking()
                                  .Where(s => reqSnackIds.Contains(s.SnackId))
                                  .ToDictionaryAsync(s => s.SnackId, s => (decimal)(s.Price ?? 0m));
                foreach (var s in request.Snacks)
                    if (snacks.TryGetValue(s.SnackId, out var p)) total += p * Math.Max(1, s.Quantity);
            }

            // tạo invoice pending
            var invoiceId = await GenerateInvoiceIdAsync();
            var now = DateTime.UtcNow;
            var invoice = new Invoices
            {
                InvoiceId = invoiceId,
                CustomerId = customer.UserId,
                CreatedAt = now,
                UpdatedAt = now,
                TotalTicket = request.SeatIds.Count,
                TotalPrice = total,     // decimal (money)
                Status = 0,             // 0: Pending
                PaymentMethod = "VNPAY"
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // LƯU lựa chọn vào Session (không tạo Tickets tại đây)
            var payload = new PendingSelection
            {
                ShowTimeId = request.ShowTimeId,
                SeatIds = request.SeatIds,
                Snacks = request.Snacks ?? new List<SnackRequest>()
            };
            HttpContext.Session.SetString($"pending:{invoiceId}",
                JsonSerializer.Serialize(payload));

            return Json(new
            {
                success = true,
                invoiceId,
                totalPrice = total
            });
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
    }
}
