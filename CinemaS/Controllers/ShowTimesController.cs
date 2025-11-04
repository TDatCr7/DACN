using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CinemaS.Models;
using CinemaS.Models.ViewModels; // <-- để dùng ShowTimeVM
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ShowTimesController : Controller
    {
        private readonly CinemaContext _context;

        public ShowTimesController(CinemaContext context)
        {
            _context = context;
        }

        // ======================= INDEX (dùng VM, có tên phim/phòng + ghế trống) =======================
        public async Task<IActionResult> Index()
        {
            var list = await _context.ShowTimes
                .Select(st => new ShowTimeVM
                {
                    ShowTimeId = st.ShowTimeId,
                    MoviesId = st.MoviesId,
                    CinemaTheaterId = st.CinemaTheaterId,
                    ShowDate = st.ShowDate,
                    StartTime = st.StartTime,
                    EndTime = st.EndTime,
                    OriginPrice = st.OriginPrice,
                    TotalCinema = st.TotalCinema
                })
                .OrderByDescending(st => st.ShowDate)
                .ToListAsync();

            // Bổ sung thông tin tên phim/phòng + ghế trống (nếu các bảng liên quan có sẵn)
            foreach (var st in list)
            {
                var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);
                var theater = await _context.CinemaTheaters.FirstOrDefaultAsync(ct => ct.CinemaTheaterId == st.CinemaTheaterId);

                st.MovieTitle = movie?.Title;
                st.CinemaTheaterName = theater?.Name;

                // Nếu có bảng Seats & Tickets:
                if (_context.Seats != null && _context.Tickets != null)
                {
                    var totalSeats = await _context.Seats.CountAsync(s => s.CinemaTheaterId == st.CinemaTheaterId);
                    var bookedSeats = await _context.Tickets.CountAsync(t => t.ShowTimeId == st.ShowTimeId && t.Status == 1);
                    st.AvailableSeats = totalSeats - bookedSeats;
                }
            }

            return View(list);
        }

        // ======================= DETAILS =======================
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var showTimes = await _context.ShowTimes
                .FirstOrDefaultAsync(m => m.ShowTimeId == id);

            if (showTimes == null) return NotFound();

            return View(showTimes);
        }

        // ======================= CREATE (GET) =======================
        public IActionResult Create()
        {
            LoadDropdowns();

            // Nếu có SeatTypes:
            if (_context.SeatTypes != null)
                ViewBag.SeatTypes = _context.SeatTypes.ToList();

            // Gửi duration phim ra View (nếu dùng để auto tính giờ kết thúc)
            var moviesWithDuration = _context.Movies
                .Where(m => m.StatusId == "RELEASED" || m.StatusId == "COMING")
                .Select(m => new { m.MoviesId, m.Duration })
                .ToDictionary(m => m.MoviesId, m => m.Duration ?? 0);

            ViewBag.MoviesWithDuration = moviesWithDuration;

            return View();
        }

        // ======================= CREATE (POST) =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ShowTimes showTime,
            string startTimeInput,
            string endTimeInput,
            Dictionary<string, decimal>? seatTypePrices) // <- cho phép null (không bắt buộc)
        {
            // Bỏ validate 3 field parse thủ công
            ModelState.Remove(nameof(showTime.ShowTimeId));
            ModelState.Remove(nameof(showTime.StartTime));
            ModelState.Remove(nameof(showTime.EndTime));

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                if (_context.SeatTypes != null)
                    ViewBag.SeatTypes = _context.SeatTypes.ToList();
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin đã nhập!";
                return View(showTime);
            }

            try
            {
                // 1) Ngày chiếu
                if (showTime.ShowDate.HasValue && showTime.ShowDate.Value.Date < DateTime.Today)
                {
                    TempData["Error"] = "Ngày chiếu không được trong quá khứ!";
                    LoadDropdowns();
                    if (_context.SeatTypes != null)
                        ViewBag.SeatTypes = _context.SeatTypes.ToList();
                    return View(showTime);
                }

                // 2) Parse giờ bắt đầu
                if (string.IsNullOrWhiteSpace(startTimeInput) ||
                    !TimeSpan.TryParse(startTimeInput, out var startSpan))
                {
                    TempData["Error"] = "Giờ bắt đầu không hợp lệ! (HH:mm)";
                    LoadDropdowns();
                    if (_context.SeatTypes != null)
                        ViewBag.SeatTypes = _context.SeatTypes.ToList();
                    return View(showTime);
                }

                // 3) Parse giờ kết thúc
                if (string.IsNullOrWhiteSpace(endTimeInput) ||
                    !TimeSpan.TryParse(endTimeInput, out var endSpan))
                {
                    TempData["Error"] = "Giờ kết thúc không hợp lệ! (HH:mm)";
                    LoadDropdowns();
                    if (_context.SeatTypes != null)
                        ViewBag.SeatTypes = _context.SeatTypes.ToList();
                    return View(showTime);
                }

                if (endSpan <= startSpan)
                {
                    TempData["Error"] = "Giờ kết thúc phải sau giờ bắt đầu!";
                    LoadDropdowns();
                    if (_context.SeatTypes != null)
                        ViewBag.SeatTypes = _context.SeatTypes.ToList();
                    return View(showTime);
                }

                // Gán Start/EndTime (combine với ShowDate)
                if (!showTime.ShowDate.HasValue)
                {
                    TempData["Error"] = "Vui lòng chọn ngày chiếu!";
                    LoadDropdowns();
                    if (_context.SeatTypes != null)
                        ViewBag.SeatTypes = _context.SeatTypes.ToList();
                    return View(showTime);
                }

                showTime.StartTime = showTime.ShowDate.Value.Date.Add(startSpan);
                showTime.EndTime = showTime.ShowDate.Value.Date.Add(endSpan);

                // 4) Check tồn tại phim/phòng
                var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == showTime.MoviesId);
                if (movie == null)
                {
                    TempData["Error"] = "Phim không tồn tại!";
                    LoadDropdowns();
                    if (_context.SeatTypes != null)
                        ViewBag.SeatTypes = _context.SeatTypes.ToList();
                    return View(showTime);
                }

                var cinema = await _context.CinemaTheaters.FirstOrDefaultAsync(ct => ct.CinemaTheaterId == showTime.CinemaTheaterId);
                if (cinema == null)
                {
                    TempData["Error"] = "Phòng chiếu không tồn tại!";
                    LoadDropdowns();
                    if (_context.SeatTypes != null)
                        ViewBag.SeatTypes = _context.SeatTypes.ToList();
                    return View(showTime);
                }

                // 5) Check trùng suất (cùng phòng/cùng ngày/đè thời gian)
                var conflict = await _context.ShowTimes
                    .Where(st => st.CinemaTheaterId == showTime.CinemaTheaterId &&
                                 st.ShowDate == showTime.ShowDate &&
                                (
                                    (showTime.StartTime >= st.StartTime && showTime.StartTime < st.EndTime) ||
                                    (showTime.EndTime > st.StartTime && showTime.EndTime <= st.EndTime) ||
                                    (showTime.StartTime <= st.StartTime && showTime.EndTime >= st.EndTime)
                                ))
                    .FirstOrDefaultAsync();

                if (conflict != null)
                {
                    TempData["Error"] =
                        $"Phòng '{cinema.Name}' đã có suất chiếu trùng thời gian! " +
                        $"({conflict.StartTime:HH:mm} - {conflict.EndTime:HH:mm} | {conflict.ShowDate:dd/MM/yyyy})";
                    LoadDropdowns();
                    if (_context.SeatTypes != null)
                        ViewBag.SeatTypes = _context.SeatTypes.ToList();
                    return View(showTime);
                }

                // 6) Sinh ID tự động
                showTime.ShowTimeId = await GenerateNewIdAsync();

                // 7) Đếm tổng ghế
                if (_context.Seats != null)
                    showTime.TotalCinema = await _context.Seats.CountAsync(s => s.CinemaTheaterId == showTime.CinemaTheaterId);

                // 8) Timestamps
                showTime.CreatedAt = DateTime.UtcNow;
                showTime.UpdatedAt = DateTime.UtcNow;

                // 9) Giá vé: nếu có seatTypePrices thì dùng; nếu không, giữ OriginPrice nhập sẵn
                if (seatTypePrices != null && seatTypePrices.Any())
                {
                    foreach (var kv in seatTypePrices)
                    {
                        if (kv.Value <= 0)
                        {
                            TempData["Error"] = "Giá vé phải lớn hơn 0!";
                            LoadDropdowns();
                            if (_context.SeatTypes != null)
                                ViewBag.SeatTypes = _context.SeatTypes.ToList();
                            return View(showTime);
                        }
                    }

                    // Lấy giá ghế thường nếu có (ST001/NORMAL) – fallback 75k
                    var normal = seatTypePrices.FirstOrDefault(x =>
                        x.Key.Contains("ST001", StringComparison.OrdinalIgnoreCase) ||
                        x.Key.Contains("NORMAL", StringComparison.OrdinalIgnoreCase));
                    showTime.OriginPrice = normal.Value > 0 ? (int)normal.Value : (showTime.OriginPrice > 0 ? showTime.OriginPrice : 75000);
                }

                _context.Add(showTime);
                await _context.SaveChangesAsync();

                TempData["Message"] =
                    $"Đã tạo suất chiếu!\n" +
                    $"Phim: {movie.Title}\n" +
                    $"Phòng: {cinema.Name}\n" +
                    $"Ngày: {showTime.ShowDate:dd/MM/yyyy}\n" +
                    $"Giờ: {showTime.StartTime:HH:mm} - {showTime.EndTime:HH:mm}";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
                LoadDropdowns();
                if (_context.SeatTypes != null)
                    ViewBag.SeatTypes = _context.SeatTypes.ToList();
                return View(showTime);
            }
        }

        // ======================= EDIT (giữ như file 1) =======================
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();
            var showTimes = await _context.ShowTimes.FindAsync(id);
            if (showTimes == null) return NotFound();
            return View(showTimes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("ShowTimeId,MoviesId,CinemaTheaterId,OriginPrice,ShowDate,StartTime,EndTime,TotalCinema,CreatedAt,UpdatedAt")] ShowTimes showTimes)
        {
            if (id != showTimes.ShowTimeId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    showTimes.UpdatedAt = DateTime.UtcNow;
                    _context.Update(showTimes);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShowTimesExists(showTimes.ShowTimeId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(showTimes);
        }

        // ======================= DELETE (giữ như file 1) =======================
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var showTimes = await _context.ShowTimes
                .FirstOrDefaultAsync(m => m.ShowTimeId == id);
            if (showTimes == null) return NotFound();

            return View(showTimes);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var showTimes = await _context.ShowTimes.FindAsync(id);
            if (showTimes != null)
            {
                _context.ShowTimes.Remove(showTimes);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ======================= Helpers =======================
        private bool ShowTimesExists(string id)
        {
            return _context.ShowTimes.Any(e => e.ShowTimeId == id);
        }

        private async Task<string> GenerateNewIdAsync()
        {
            var last = await _context.ShowTimes
                .OrderByDescending(st => st.ShowTimeId)
                .FirstOrDefaultAsync();

            if (last == null) return "ST001";
            var n = int.Parse(last.ShowTimeId.Substring(2));
            return $"ST{(n + 1):D3}";
        }

        private void LoadDropdowns()
        {
            // Phim đang chiếu/đang đến
            ViewBag.Movies = new SelectList(
                _context.Movies
                    .Where(m => m.StatusId == "RELEASED" || m.StatusId == "COMING")
                    .OrderBy(m => m.Title),
                "MoviesId", "Title");

            // Phòng đang hoạt động
            ViewBag.CinemaTheaters = new SelectList(
                _context.CinemaTheaters
                    .Where(ct => ct.Status == 1)
                    .OrderBy(ct => ct.Name),
                "CinemaTheaterId", "Name");
        }
    }
}
