using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;
using CinemaS.Models.ViewModels;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CinemaTheatersController : Controller
    {
        private readonly CinemaContext _context;

        public CinemaTheatersController(CinemaContext context)
        {
            _context = context;
        }

        // ================== LIST + SEARCH ==================
        public async Task<IActionResult> Index(string? searchString)
        {
            // Lấy toàn bộ phòng chiếu + loại phòng + rạp
            var query = from ct in _context.CinemaTheaters
                        join type in _context.CinemaTypes on ct.CinemaTypeId equals type.CinemaTypeId
                        join theater in _context.MovieTheaters on ct.MovieTheaterId equals theater.MovieTheaterId
                        select new CinemaTheaterVM
                        {
                            CinemaTheaterId = ct.CinemaTheaterId,
                            Name = ct.Name,
                            CinemaTypeName = type.Name,
                            MovieTheaterName = theater.Name,
                            NumOfRows = ct.NumOfRows,
                            NumOfColumns = ct.NumOfColumns,
                            Status = ct.Status,
                            TotalSeats = _context.Seats.Count(s => s.CinemaTheaterId == ct.CinemaTheaterId)
                        };

            // Nếu có chuỗi tìm kiếm => lọc
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim().ToLower();
                query = query.Where(t =>
                    t.Name.ToLower().Contains(searchString) ||
                    t.CinemaTheaterId.ToLower().Contains(searchString) ||
                    t.CinemaTypeName.ToLower().Contains(searchString) ||
                    t.MovieTheaterName.ToLower().Contains(searchString)
                );
                ViewData["CurrentFilter"] = searchString;
            }

            var theaters = await query
                .OrderBy(t => t.CinemaTheaterId)
                .ToListAsync();

            return View(theaters);
        }

        // ================== AJAX SEARCH ==================
        [HttpGet]
        public async Task<IActionResult> SearchCinemaTheaters(string searchString)
        {
            if (string.IsNullOrWhiteSpace(searchString))
            {
                var all = await _context.CinemaTheaters
                    .Select(ct => new
                    {
                        ct.CinemaTheaterId,
                        ct.Name,
                        ct.NumOfRows,
                        ct.NumOfColumns,
                        ct.Status,
                        CinemaTypeName = _context.CinemaTypes
                            .Where(t => t.CinemaTypeId == ct.CinemaTypeId)
                            .Select(t => t.Name)
                            .FirstOrDefault(),
                        MovieTheaterName = _context.MovieTheaters
                            .Where(mt => mt.MovieTheaterId == ct.MovieTheaterId)
                            .Select(mt => mt.Name)
                            .FirstOrDefault(),
                        TotalSeats = _context.Seats.Count(s => s.CinemaTheaterId == ct.CinemaTheaterId)
                    }).ToListAsync();

                return Json(all);
            }

            searchString = searchString.Trim().ToLower();

            var result = await _context.CinemaTheaters
                .Where(ct =>
                    ct.CinemaTheaterId.ToLower().Contains(searchString) ||
                    ct.Name.ToLower().Contains(searchString) ||
                    _context.CinemaTypes.Any(t => t.CinemaTypeId == ct.CinemaTypeId && t.Name.ToLower().Contains(searchString)) ||
                    _context.MovieTheaters.Any(mt => mt.MovieTheaterId == ct.MovieTheaterId && mt.Name.ToLower().Contains(searchString))
                )
                .Select(ct => new
                {
                    ct.CinemaTheaterId,
                    ct.Name,
                    ct.NumOfRows,
                    ct.NumOfColumns,
                    ct.Status,
                    CinemaTypeName = _context.CinemaTypes
                        .Where(t => t.CinemaTypeId == ct.CinemaTypeId)
                        .Select(t => t.Name)
                        .FirstOrDefault(),
                    MovieTheaterName = _context.MovieTheaters
                        .Where(mt => mt.MovieTheaterId == ct.MovieTheaterId)
                        .Select(mt => mt.Name)
                        .FirstOrDefault(),
                    TotalSeats = _context.Seats.Count(s => s.CinemaTheaterId == ct.CinemaTheaterId)
                })
                .ToListAsync();

            return Json(result);
        }

        // ================== DETAILS ==================
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var cinemaTheater = await _context.CinemaTheaters
                .FirstOrDefaultAsync(m => m.CinemaTheaterId == id);

            if (cinemaTheater == null) return NotFound();

            return View(cinemaTheater);
        }

        // ================== CREATE ==================
        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CinemaTheaters cinemaTheater)
        {
            ModelState.Remove(nameof(cinemaTheater.CinemaTheaterId));

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin!";
                LoadDropdowns();
                return View(cinemaTheater);
            }

            try
            {
                var seatTypesCount = await _context.SeatTypes.CountAsync();
                if (seatTypesCount < 1)
                {
                    TempData["Error"] = "Cần có ít nhất 1 loại ghế NORMAL!";
                    LoadDropdowns();
                    return View(cinemaTheater);
                }

                cinemaTheater.CinemaTheaterId = await GenerateNewTheaterIdAsync();
                cinemaTheater.Status = 1;

                // Admin must input dimensions
                if (!cinemaTheater.NumOfRows.HasValue || cinemaTheater.NumOfRows.Value < 1)
                    cinemaTheater.NumOfRows = 6;
                if (!cinemaTheater.NumOfColumns.HasValue || cinemaTheater.NumOfColumns.Value < 1)
                    cinemaTheater.NumOfColumns = 14;

                // Clear old fixed row configurations
                cinemaTheater.RegularSeatRow = null;
                cinemaTheater.VIPSeatRow = null;
                cinemaTheater.DoubleSeatRow = null;

                _context.Add(cinemaTheater);
                await _context.SaveChangesAsync();

                // FIRST SAVE: Create base layout with Normal seats only
                await CreateBaseSeatLayout(cinemaTheater);

                TempData["Message"] = $"✅ Tạo phòng '{cinemaTheater.Name}' thành công! Bây giờ bạn có thể chỉnh sửa bố cục ghế.";
                
                // ✅ REDIRECT to Seats/Index for that room
                return RedirectToAction("Index", "Seats", new { cinemaTheaterId = cinemaTheater.CinemaTheaterId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
                LoadDropdowns();
                return View(cinemaTheater);
            }
        }

        // ================== EDIT ==================
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var ct = await _context.CinemaTheaters.FindAsync(id);
            if (ct == null) return NotFound();

            LoadDropdowns();
            return View(ct);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, CinemaTheaters ct)
        {
            if (id != ct.CinemaTheaterId) return NotFound();

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(ct);
            }

            try
            {
                _context.Update(ct);
                await _context.SaveChangesAsync();
                TempData["Message"] = "✅ Cập nhật phòng chiếu thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CinemaTheatersExists(ct.CinemaTheaterId))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // ================== DELETE ==================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var ct = await _context.CinemaTheaters.FirstOrDefaultAsync(m => m.CinemaTheaterId == id);
            if (ct == null) return NotFound();

            return View(ct);
        }

        [HttpGet]
        public JsonResult CheckInUse(string id)
        {
            var inUse = _context.ShowTimes.Any(st => st.CinemaTheaterId == id);
            return Json(new { inUse });
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            try
            {
                var entity = await _context.CinemaTheaters.FindAsync(id);
                if (entity == null)
                    return NotFound();

                _context.CinemaTheaters.Remove(entity);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa thành công!";
            }
            catch (Exception)
            {
                TempData["Error"] = "Không thể xóa vì đang được sử dụng!";
            }
            return RedirectToAction(nameof(Index));
        }

        // ================== AJAX DELETE ==================
        [HttpPost]
        [IgnoreAntiforgeryToken] // AJAX call doesn't need anti-forgery for simplicity
        public async Task<IActionResult> DeleteAjax(string id)
        {
            try
            {
                var entity = await _context.CinemaTheaters.FindAsync(id);
                if (entity == null)
                    return Json(new { success = false, message = "Phòng chiếu không tồn tại." });

                // Kiểm tra xem phòng có đang được sử dụng không
                var inUse = await _context.ShowTimes.AnyAsync(st => st.CinemaTheaterId == id);
                if (inUse)
                    return Json(new { success = false, message = "Phòng này đang được sử dụng trong suất chiếu. Không thể xóa!" });

                _context.CinemaTheaters.Remove(entity);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa phòng chiếu thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // ================== HELPERS ==================
        private bool CinemaTheatersExists(string id)
            => _context.CinemaTheaters.Any(e => e.CinemaTheaterId == id);

        private void LoadDropdowns()
        {
            ViewBag.CinemaTypes = new SelectList(_context.CinemaTypes.OrderBy(x => x.Name), "CinemaTypeId", "Name");
            ViewBag.MovieTheaters = new SelectList(_context.MovieTheaters.OrderBy(x => x.Name), "MovieTheaterId", "Name");
        }

        private async Task<string> GenerateNewTheaterIdAsync()
        {
            var last = await _context.CinemaTheaters
                .OrderByDescending(ct => ct.CinemaTheaterId)
                .FirstOrDefaultAsync();

            if (last == null) return "CT001";
            var num = int.Parse(last.CinemaTheaterId.Substring(2));
            return $"CT{(num + 1):D3}";
        }

        // ✅ NEW: Create base seat layout with all Normal seats
        private async Task CreateBaseSeatLayout(CinemaTheaters theater)
        {
            var seats = new List<Seats>();
            var seatTypes = await _context.SeatTypes.ToListAsync();

            var normal = seatTypes.FirstOrDefault(st => st.Name == "NORMAL");
            if (normal == null)
            {
                // Fallback: use first seat type
                normal = seatTypes.FirstOrDefault();
                if (normal == null) return;
            }

            var lastSeat = await _context.Seats.OrderByDescending(s => s.SeatId).FirstOrDefaultAsync();
            int counter = (lastSeat != null && lastSeat.SeatId.StartsWith("S"))
                ? int.Parse(lastSeat.SeatId.Substring(1)) + 1 : 1;

            int rows = theater.NumOfRows ?? 6;
            int cols = theater.NumOfColumns ?? 14;

            // Generate row labels dynamically: A, B, C, ...
            for (int r = 0; r < rows; r++)
            {
                char rowLabel = (char)('A' + r);
                
                for (int col = 1; col <= cols; col++)
                {
                    seats.Add(new Seats
                    {
                        SeatId = $"S{counter++:D6}",
                        SeatTypeId = normal.SeatTypeId,
                        CinemaTheaterId = theater.CinemaTheaterId,
                        RowIndex = rowLabel.ToString(),
                        ColumnIndex = col,
                        Label = $"{rowLabel}{col}",
                        IsActive = true,
                        PairId = null  // No pairs initially
                    });
                }
            }

            _context.Seats.AddRange(seats);
            await _context.SaveChangesAsync();
        }
    }
}
