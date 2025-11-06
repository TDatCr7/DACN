using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;
using CinemaS.Models.ViewModels;

namespace CinemaS.Controllers
{
    public class BookingController : Controller
    {
        private readonly CinemaContext _context;

        public BookingController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Booking/SeatSelection/ST001
        [Authorize] // ✅ BẮT BUỘC ĐĂNG NHẬP
        public async Task<IActionResult> SeatSelection(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var showTime = await _context.ShowTimes
                .FirstOrDefaultAsync(st => st.ShowTimeId == id);

            if (showTime == null)
                return NotFound();

            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == showTime.MoviesId);
            var theater = await _context.CinemaTheaters.FirstOrDefaultAsync(ct => ct.CinemaTheaterId == showTime.CinemaTheaterId);

            if (movie == null || theater == null)
                return NotFound();

            // Lấy danh sách ghế
            var seats = await _context.Seats
                .Where(s => s.CinemaTheaterId == showTime.CinemaTheaterId)
                .OrderBy(s => s.RowIndex)
                .ThenBy(s => s.ColumnIndex)
                .ToListAsync();

            // Lấy danh sách ghế đã đặt (Status = 1 và chưa hết hạn)
            var bookedSeatIds = await _context.Tickets
                .Where(t => t.ShowTimeId == id
 && t.Status == 1
  && t.Expire > DateTime.UtcNow) // ✅ Chỉ tính ghế chưa hết hạn
                .Select(t => t.SeatId)
                .ToListAsync();

            // Lấy thông tin loại ghế
            var seatTypes = await _context.SeatTypes.ToListAsync();

            var seatVMs = seats.Select(s =>
            {
                var seatType = seatTypes.FirstOrDefault(st => st.SeatTypeId == s.SeatTypeId);
                var isBooked = bookedSeatIds.Contains(s.SeatId);

                return new SeatVM
                {
                    SeatId = s.SeatId,
                    SeatTypeId = s.SeatTypeId,
                    SeatTypeName = seatType?.Name,
                    SeatTypePrice = seatType?.Price,
                    RowIndex = s.RowIndex,
                    ColumnIndex = s.ColumnIndex,
                    Label = s.Label,
                    Status = isBooked ? "Booked" : "Available",
                    IsCouple = seatType?.Name == "COUPLE",
                    IsVIP = seatType?.Name == "VIP"
                };
            }).ToList();

            // ✅ Lấy danh sách Snacks (có Price) để hiển thị
            var snacks = await _context.Snacks
            .Where(s => s.IsActive == true) // ✅ Chỉ lấy snacks đang bán
       .ToListAsync();
            ViewBag.Snacks = snacks;

            var vm = new SeatSelectionVM
            {
                ShowTimeId = showTime.ShowTimeId,
                MoviesId = movie.MoviesId,
                MovieTitle = movie.Title,
                MoviePoster = movie.PosterImage,
                CinemaTheaterName = theater.Name,
                ShowDate = showTime.ShowDate,
                StartTime = showTime.StartTime,
                EndTime = showTime.EndTime, // ✅ Thêm thời gian kết thúc
                Seats = seatVMs,
                NumOfRows = theater.NumOfRows ?? 6,
                NumOfColumns = theater.NumOfColumns ?? 14
            };

            return View(vm);
        }

        // API: Lấy trạng thái ghế real-time
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeatsStatus(string showTimeId)
        {
            try
            {
                var bookedSeatIds = await _context.Tickets
                       .Where(t => t.ShowTimeId == showTimeId
                        && t.Status == 1
                    && t.Expire > DateTime.UtcNow) // ✅ Chỉ lấy ghế chưa hết hạn
                .Select(t => t.SeatId)
                             .ToListAsync();

                return Json(new { success = true, bookedSeats = bookedSeatIds });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // API: Đặt ghế (lưu vào database)
        [HttpPost]
        [Authorize] // ✅ BẮT BUỘC ĐĂNG NHẬP
        public async Task<IActionResult> BookSeats([FromBody] BookSeatRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ShowTimeId) || !request.SeatIds.Any())
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            try
            {
                // ✅ LOG
                Console.WriteLine($"=== BOOK SEATS ===");
                Console.WriteLine($"ShowTimeId: {request.ShowTimeId}");
                Console.WriteLine($"SeatIds: {string.Join(",", request.SeatIds)}");
                Console.WriteLine($"User: {User.Identity?.Name}");

                // ✅ 1. Kiểm tra user đã đăng nhập
                var userEmail = User.Identity?.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập để đặt vé!" });
                }

                var customer = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (customer == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin khách hàng!" });
                }

                Console.WriteLine($"Customer: {customer.UserId}");

                // ✅ 2. Kiểm tra suất chiếu tồn tại
                var showTime = await _context.ShowTimes.FirstOrDefaultAsync(st => st.ShowTimeId == request.ShowTimeId);
                if (showTime == null)
                {
                    return Json(new { success = false, message = "Suất chiếu không tồn tại!" });
                }

                // ✅ 3. Kiểm tra ghế còn trống
                var bookedSeats = await _context.Tickets
     .Where(t => t.ShowTimeId == request.ShowTimeId
            && request.SeatIds.Contains(t.SeatId)
          && t.Status == 1
       && t.Expire > DateTime.UtcNow) // ✅ Chỉ tính ghế chưa hết hạn
    .Select(t => new { t.SeatId })
     .ToListAsync();

                if (bookedSeats.Any())
                {
                    var bookedSeatIds = string.Join(", ", bookedSeats.Select(s => s.SeatId));
                    return Json(new { success = false, message = $"Ghế {bookedSeatIds} đã được đặt bởi khách khác!" });
                }

                // ✅ 4. Lấy thông tin ghế để tính giá
                var seats = await _context.Seats
.Where(s => request.SeatIds.Contains(s.SeatId))
             .ToListAsync();

                var seatTypes = await _context.SeatTypes.ToListAsync();
                decimal totalPrice = 0;

                // ✅ 5. Tạo invoice
                var invoice = new Invoices
                {
                    InvoiceId = await GenerateInvoiceIdAsync(),
                    CustomerId = customer.UserId, // ✅ UserId của user đăng nhập
                    CreatedAt = DateTime.UtcNow,
                    TotalTicket = request.SeatIds.Count,
                    TotalPrice = 0 // Tính sau
                };

                Console.WriteLine($"Created Invoice: {invoice.InvoiceId}");

                // ✅ 6. Tạo danh sách Tickets
                var tickets = new List<Tickets>();
                foreach (var seatId in request.SeatIds)
                {
                    var seat = seats.FirstOrDefault(s => s.SeatId == seatId);
                    if (seat == null)
                    {
                        Console.WriteLine($"⚠️ Seat not found: {seatId}");
                        continue;
                    }

                    var seatType = seatTypes.FirstOrDefault(st => st.SeatTypeId == seat.SeatTypeId);
                    var price = seatType?.Price ?? 0;
                    totalPrice += price;

                    var ticket = new Tickets
                    {
                        TicketId = await GenerateTicketIdAsync(),
                        InvoiceId = invoice.InvoiceId,
                        TicketTypeId = "TT001", // Mặc định
                        ShowTimeId = request.ShowTimeId,
                        SeatId = seatId,
                        Status = 1, // Đã đặt
                        Price = (int)price,
                        CreatedBooking = DateTime.UtcNow,
                        Expire = DateTime.UtcNow.AddMinutes(15) // Giữ ghế 15 phút
                    };

                    tickets.Add(ticket);
                    Console.WriteLine($"  Ticket: {ticket.TicketId} - Seat: {seatId} - Price: {price}");
                }

                invoice.TotalPrice = (int)totalPrice;

                // ✅ 7. Lưu đồ ăn kèm vào DetailBookingSnacks
                if (request.Snacks != null && request.Snacks.Any())
                {
                    foreach (var snackItem in request.Snacks)
                    {
                        // ✅ Lấy thông tin Snack từ bảng Snacks
                        var snack = await _context.Snacks.FirstOrDefaultAsync(s => s.SnackId == snackItem.SnackId);
                        if (snack == null) continue;

                        var snackPrice = (snack.Price ?? 0) * snackItem.Quantity;
                        totalPrice += snackPrice;

                        var detailSnack = new DetailBookingSnacks
                        {
                            DetailBookingSnackId = await GenerateDetailBookingSnackIdAsync(),
                            InvoiceId = invoice.InvoiceId,
                            SnackId = snackItem.SnackId,
                            // ✅ TẠM THỜI BỎ QUA 2 CỘT NÀY (nếu database chưa có)
                            // SnackTypeId = snack.SnackTypeId, 
                            // Quantity = snackItem.Quantity,
                            TotalSnack = snackItem.Quantity,
                            TotalPrice = snackPrice
                        };

                        _context.DetailBookingSnacks.Add(detailSnack);
                    }

                    // Cập nhật lại TotalPrice
                    invoice.TotalPrice = (int)totalPrice;
                }

                // ✅ 8. Lưu vào database
                _context.Invoices.Add(invoice);
                _context.Tickets.AddRange(tickets);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Saved: {tickets.Count} tickets, {request.Snacks?.Count ?? 0} snacks, Total: {totalPrice} đ");

                return Json(new
                {
                    success = true,
                    message = $"Đặt vé thành công! {request.SeatIds.Count} ghế",
                    invoiceId = invoice.InvoiceId,
                    totalPrice = totalPrice,
                    ticketCount = request.SeatIds.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception: {ex.Message}");
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // GET: Booking/Create?movieId=MV0000005
        // Hiển thị danh sách suất chiếu của phim để chọn
        [Authorize] // ✅ BẮT BUỘC ĐĂNG NHẬP
        public async Task<IActionResult> Create(string movieId)
        {
            if (string.IsNullOrEmpty(movieId))
                return BadRequest("Vui lòng chọn phim!");

            // ✅ Kiểm tra phim tồn tại
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == movieId);
            if (movie == null)
                return NotFound("Phim không tồn tại!");

            // ✅ Lấy danh sách suất chiếu của phim (ngày hôm nay trở đi)
            var today = DateTime.Today;
            var showTimes = await _context.ShowTimes
                .Where(st => st.MoviesId == movieId && st.ShowDate >= today && st.CinemaTheaterId != null)
                .OrderBy(st => st.ShowDate)
                .ThenBy(st => st.StartTime)
                .ToListAsync();
            if (!showTimes.Any())
            {
                TempData["Message"] = "Phim này hiện không có suất chiếu nào!";
            }


            // ✅ Lấy thông tin thêm (phòng chiếu, ghế còn trống)
            var showTimeVMs = new List<ShowTimeVM>();
            foreach (var st in showTimes)
            {
                var cinema = await _context.CinemaTheaters
                    .FirstOrDefaultAsync(ct => ct.CinemaTheaterId == st.CinemaTheaterId);

                var totalSeats = await _context.Seats
     .CountAsync(s => s.CinemaTheaterId == st.CinemaTheaterId);

                var bookedSeats = await _context.Tickets
                .CountAsync(t => t.ShowTimeId == st.ShowTimeId
                      && t.Status == 1
               && t.Expire > DateTime.UtcNow);

                showTimeVMs.Add(new ShowTimeVM
                {
                    ShowTimeId = st.ShowTimeId,
                    MovieTitle = movie.Title,
                    CinemaName = cinema?.Name ?? "Unknown",
                    ShowDate = st.ShowDate,
                    StartTime = st.StartTime,
                    EndTime = st.EndTime,
                    TotalSeats = totalSeats,
                    AvailableSeats = totalSeats - bookedSeats,
                    Price = st.OriginPrice ?? 75000
                });
            }

            // ✅ Pass dữ liệu sang View
            ViewBag.Movie = movie;
            ViewBag.ShowTimes = showTimeVMs;

            return View(showTimeVMs);
        }

        // GET: Booking/SeatSelection/ST001

        // ✅ Generate IDs
        private async Task<string> GenerateInvoiceIdAsync()
        {
            var last = await _context.Invoices
                    .OrderByDescending(i => i.InvoiceId)
                           .FirstOrDefaultAsync();

            if (last == null) return "INV001";

            // Parse "INV001" → 1
            var num = int.Parse(last.InvoiceId.Substring(3));
            return $"INV{(num + 1):D3}";
        }

        private async Task<string> GenerateTicketIdAsync()
        {
            var last = await _context.Tickets
     .OrderByDescending(t => t.TicketId)
        .FirstOrDefaultAsync();

            if (last == null) return "T000001";

            // Parse "T000001" → 1
            var num = int.Parse(last.TicketId.Substring(1));
            return $"T{(num + 1):D6}";
        }

        private async Task<string> GenerateDetailBookingSnackIdAsync()
        {
            var last = await _context.DetailBookingSnacks
     .OrderByDescending(d => d.DetailBookingSnackId)
       .FirstOrDefaultAsync();

            if (last == null) return "DBS001";

            // Parse "DBS001" → 1
            var num = int.Parse(last.DetailBookingSnackId.Substring(3));
            return $"DBS{(num + 1):D3}";
        }
    }

    // Request model
    public class BookSeatRequest
    {
        public string ShowTimeId { get; set; } = default!;
        public List<string> SeatIds { get; set; } = new();
        public List<SnackRequest>? Snacks { get; set; } // ✅ Danh sách snacks
    }

    public class SnackRequest
    {
        public string SnackId { get; set; } = default!; // ✅ Đổi từ SnackTypeId sang SnackId
        public int Quantity { get; set; }
    }
}
