using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CinemaS.Models;
using CinemaS.Models.ViewModels;
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

        // ======================= INDEX (có tìm kiếm) =======================
        public async Task<IActionResult> Index(string? searchTerm)
        {
            // Load all CinemaTheaters into a dictionary
            var cinemaTheaters = await _context.CinemaTheaters
                .Select(ct => new CinemaTheaterVM
                {
                    CinemaTheaterId = ct.CinemaTheaterId,
                    Name = ct.Name,
                    TotalSeats = _context.Seats.Count(s => s.CinemaTheaterId == ct.CinemaTheaterId)
                })
                .ToDictionaryAsync(ct => ct.CinemaTheaterId);

            // Load all ShowTimes into a list of ShowTimeVM
            var showTimes = await _context.ShowTimes
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
                .ToListAsync();

            // Map TotalSeats from CinemaTheaterVM to ShowTimeVM
            foreach (var st in showTimes)
            {
                if (cinemaTheaters.TryGetValue(st.CinemaTheaterId, out var theater))
                {
                    st.TotalSeats = theater.TotalSeats;
                }

                var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);
                var theaterName = await _context.CinemaTheaters.FirstOrDefaultAsync(ct => ct.CinemaTheaterId == st.CinemaTheaterId);

                st.MovieTitle = movie?.Title;
                st.CinemaTheaterName = theaterName?.Name;

                if (_context.Seats != null && _context.Tickets != null)
                {
                    var totalSeats = await _context.Seats.CountAsync(s => s.CinemaTheaterId == st.CinemaTheaterId && s.IsActive);
                    var bookedSeats = await _context.Tickets.CountAsync(t => t.ShowTimeId == st.ShowTimeId && t.Status == 1);
                    st.AvailableSeats = totalSeats - bookedSeats;
                }
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lower = searchTerm.ToLower();
                showTimes = showTimes.Where(st =>
                    (st.MovieTitle ?? "").ToLower().Contains(lower) ||
                    (st.CinemaTheaterName ?? "").ToLower().Contains(lower))
                    .ToList();
            }

            return View(showTimes);
        }

        // ======================= DETAILS =======================
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();
            var showTimes = await _context.ShowTimes.FirstOrDefaultAsync(m => m.ShowTimeId == id);
            if (showTimes == null) return NotFound();
            
            // Load movie and theater info
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == showTimes.MoviesId);
            var theater = await _context.CinemaTheaters.FirstOrDefaultAsync(ct => ct.CinemaTheaterId == showTimes.CinemaTheaterId);
            
            ViewBag.MovieTitle = movie?.Title;
            ViewBag.TheaterName = theater?.Name;
            
            return View(showTimes);
        }

        // ======================= CREATE (GET) =======================
        public IActionResult Create()
        {
            LoadDropdowns();

            if (_context.SeatTypes != null)
                ViewBag.SeatTypes = _context.SeatTypes.ToList();

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
            Dictionary<string, decimal>? seatTypePrices)
        {
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
                if (showTime.ShowDate.HasValue && showTime.ShowDate.Value.Date < DateTime.Today)
                {
                    TempData["Error"] = "Ngày chiếu không được trong quá khứ!";
                    LoadDropdowns();
                    return View(showTime);
                }

                if (!TimeSpan.TryParse(startTimeInput, out var startSpan))
                {
                    TempData["Error"] = "Giờ bắt đầu không hợp lệ!";
                    LoadDropdowns();
                    return View(showTime);
                }

                if (!TimeSpan.TryParse(endTimeInput, out var endSpan))
                {
                    TempData["Error"] = "Giờ kết thúc không hợp lệ!";
                    LoadDropdowns();
                    return View(showTime);
                }

                if (endSpan <= startSpan)
                {
                    TempData["Error"] = "Giờ kết thúc phải sau giờ bắt đầu!";
                    LoadDropdowns();
                    return View(showTime);
                }

                if (!showTime.ShowDate.HasValue)
                {
                    TempData["Error"] = "Vui lòng chọn ngày chiếu!";
                    LoadDropdowns();
                    return View(showTime);
                }

                showTime.StartTime = showTime.ShowDate.Value.Date.Add(startSpan);
                showTime.EndTime = showTime.ShowDate.Value.Date.Add(endSpan);

                var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == showTime.MoviesId);
                var cinema = await _context.CinemaTheaters.FirstOrDefaultAsync(ct => ct.CinemaTheaterId == showTime.CinemaTheaterId);

                if (movie == null || cinema == null)
                {
                    TempData["Error"] = "Phim hoặc phòng chiếu không tồn tại!";
                    LoadDropdowns();
                    return View(showTime);
                }

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
                    TempData["Error"] = $"Phòng '{cinema.Name}' đã có suất chiếu trùng thời gian ({conflict.StartTime:HH:mm}-{conflict.EndTime:HH:mm})!";
                    LoadDropdowns();
                    return View(showTime);
                }

                showTime.ShowTimeId = await GenerateNewIdAsync();
                showTime.TotalCinema = await _context.Seats.CountAsync(s => s.CinemaTheaterId == showTime.CinemaTheaterId && s.IsActive);
                showTime.CreatedAt = DateTime.UtcNow;
                showTime.UpdatedAt = DateTime.UtcNow;

                if (seatTypePrices != null && seatTypePrices.Any())
                {
                    var normal = seatTypePrices.FirstOrDefault(x =>
                        x.Key.Contains("ST001", StringComparison.OrdinalIgnoreCase) ||
                        x.Key.Contains("NORMAL", StringComparison.OrdinalIgnoreCase));
                    showTime.OriginPrice = normal.Value > 0 ? (int)normal.Value : 75000;
                }

                _context.Add(showTime);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"✅ Đã tạo suất chiếu: {movie.Title} - {cinema.Name} ({showTime.ShowDate:dd/MM/yyyy} {showTime.StartTime:HH:mm})";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
                LoadDropdowns();
                return View(showTime);
            }
        }

        // ======================= EDIT (GET) =======================
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var showTimes = await _context.ShowTimes.FindAsync(id);
            if (showTimes == null) return NotFound();

            LoadDropdowns();

            ViewBag.Movies = new SelectList(
                _context.Movies
                    .Where(m => m.StatusId == "RELEASED" || m.StatusId == "COMING")
                    .OrderBy(m => m.Title),
                "MoviesId", "Title", showTimes.MoviesId
            );

            ViewBag.CinemaTheaters = new SelectList(
                _context.CinemaTheaters
                    .Where(ct => ct.Status == 1)
                    .OrderBy(ct => ct.Name),
                "CinemaTheaterId", "Name", showTimes.CinemaTheaterId
            );

            return View(showTimes);
        }

        // ======================= EDIT (POST) =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id,
            [Bind("ShowTimeId,MoviesId,CinemaTheaterId,OriginPrice,ShowDate,StartTime,EndTime,TotalCinema,CreatedAt,UpdatedAt")]
            ShowTimes showTimes)
        {
            if (id != showTimes.ShowTimeId) return NotFound();

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                ViewBag.Movies = new SelectList(_context.Movies, "MoviesId", "Title", showTimes.MoviesId);
                ViewBag.CinemaTheaters = new SelectList(_context.CinemaTheaters, "CinemaTheaterId", "Name", showTimes.CinemaTheaterId);
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin!";
                return View(showTimes);
            }

            try
            {
                showTimes.UpdatedAt = DateTime.UtcNow;
                _context.Update(showTimes);
                await _context.SaveChangesAsync();
                TempData["Message"] = "✅ Cập nhật suất chiếu thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ShowTimesExists(showTimes.ShowTimeId)) return NotFound();
                else throw;
            }
        }

        // ======================= DELETE =======================
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
                await _context.SaveChangesAsync();
                TempData["Message"] = "🗑️ Đã xóa suất chiếu thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // ======================= Helpers =======================
        private bool ShowTimesExists(string id)
        {
            return _context.ShowTimes.Any(e => e.ShowTimeId == id);
        }

        private async Task<string> GenerateNewIdAsync()
        {
            var last = await _context.ShowTimes.OrderByDescending(st => st.ShowTimeId).FirstOrDefaultAsync();
            if (last == null) return "ST001";
            var n = int.Parse(last.ShowTimeId.Substring(2));
            return $"ST{(n + 1):D3}";
        }

        private void LoadDropdowns()
        {
            ViewBag.Movies = new SelectList(
                _context.Movies
                    .Where(m => m.StatusId == "RELEASED" || m.StatusId == "COMING")
                    .OrderBy(m => m.Title),
                "MoviesId", "Title");

            ViewBag.CinemaTheaters = new SelectList(
                _context.CinemaTheaters
                    .Where(ct => ct.Status == 1)
                    .OrderBy(ct => ct.Name),
                "CinemaTheaterId", "Name");
        }
    }
}
