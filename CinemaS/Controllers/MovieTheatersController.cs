// Controllers/MovieTheatersController.cs
using CinemaS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CinemaS.Controllers
{
    [Authorize]
    public class MovieTheatersController : Controller
    {
        private readonly CinemaContext _context;

        public MovieTheatersController(CinemaContext context)
        {
            _context = context;
        }

        // USER: chỉ xem chi tiết
        [AllowAnonymous] // nếu muốn ai cũng xem được detail. Nếu muốn phải đăng nhập mới xem: bỏ AllowAnonymous
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "❌ Không tìm thấy mã rạp chiếu!";
                return RedirectToAction("Index", "Home");
            }

            var movieTheaters = await _context.MovieTheaters
                .FirstOrDefaultAsync(m => m.MovieTheaterId == id);

            if (movieTheaters == null)
            {
                TempData["Error"] = "❌ Không tìm thấy rạp chiếu!";
                return RedirectToAction("Index", "Home");
            }

            var province = await _context.Provinces
                .FirstOrDefaultAsync(p => p.ProvinceId == movieTheaters.ProvinceId);

            ViewBag.ProvinceName = province?.Name ?? "Không xác định";
            return View(movieTheaters);
        }

        // ADMIN: quản lý
        [Authorize(Roles = "Admin")]
        public IActionResult Management() => View();

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var movieTheaters = from mt in _context.MovieTheaters select mt;

            if (!string.IsNullOrEmpty(searchString))
            {
                movieTheaters = movieTheaters.Where(mt =>
                    mt.Name!.Contains(searchString) ||
                    mt.Address!.Contains(searchString) ||
                    mt.MovieTheaterId!.Contains(searchString));
            }

            return View(await movieTheaters.OrderBy(mt => mt.MovieTheaterId).ToListAsync());
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> SearchMovieTheaters(string searchString)
        {
            var movieTheaters = from mt in _context.MovieTheaters select mt;

            if (!string.IsNullOrEmpty(searchString))
            {
                movieTheaters = movieTheaters.Where(mt =>
                    mt.Name!.Contains(searchString) ||
                    mt.Address!.Contains(searchString) ||
                    mt.MovieTheaterId!.Contains(searchString));
            }

            var results = await movieTheaters
                .OrderBy(mt => mt.MovieTheaterId)
                .Select(mt => new
                {
                    mt.MovieTheaterId,
                    mt.Name,
                    mt.Address,
                    mt.Hotline,
                    mt.Status,
                    mt.IFrameCode,
                    mt.ProvinceId
                })
                .ToListAsync();

            return Json(results);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Address,Hotline,Status,IFrameCode,ProvinceId")] MovieTheaters movieTheaters)
        {
            ModelState.Remove(nameof(movieTheaters.MovieTheaterId));

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(movieTheaters);
            }

            try
            {
                var provinceExists = await _context.Provinces.AnyAsync(p => p.ProvinceId == movieTheaters.ProvinceId);
                if (!provinceExists)
                {
                    TempData["Error"] = "❌ Tỉnh/Thành phố không tồn tại!";
                    LoadDropdowns();
                    return View(movieTheaters);
                }

                movieTheaters.MovieTheaterId = await GenerateNewIdAsync();

                if (movieTheaters.Status == null) movieTheaters.Status = 1;

                _context.Add(movieTheaters);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"✅ Tạo rạp chiếu '{movieTheaters.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                LoadDropdowns();
                TempData["Error"] = ex.InnerException == null ? $"❌ Lỗi: {ex.Message}" : $"❌ Lỗi: {ex.InnerException.Message}";
                return View(movieTheaters);
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "❌ Không tìm thấy mã rạp chiếu!";
                return RedirectToAction(nameof(Index));
            }

            var movieTheaters = await _context.MovieTheaters.FindAsync(id);
            if (movieTheaters == null)
            {
                TempData["Error"] = "❌ Không tìm thấy rạp chiếu!";
                return RedirectToAction(nameof(Index));
            }

            LoadDropdowns();
            return View(movieTheaters);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MovieTheaterId,Name,Address,Hotline,Status,IFrameCode,ProvinceId")] MovieTheaters movieTheaters)
        {
            if (id != movieTheaters.MovieTheaterId)
            {
                TempData["Error"] = "❌ Mã rạp chiếu không khớp!";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(movieTheaters);
            }

            try
            {
                _context.Update(movieTheaters);
                await _context.SaveChangesAsync();

                TempData["Message"] = "✅ Cập nhật rạp chiếu thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.MovieTheaters.Any(e => e.MovieTheaterId == movieTheaters.MovieTheaterId))
                {
                    TempData["Error"] = "❌ Rạp chiếu không tồn tại!";
                    return RedirectToAction(nameof(Index));
                }
                throw;
            }
            catch (Exception ex)
            {
                LoadDropdowns();
                TempData["Error"] = $"❌ Lỗi: {ex.Message}";
                return View(movieTheaters);
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "❌ Không tìm thấy mã rạp chiếu!";
                return RedirectToAction(nameof(Index));
            }

            var movieTheaters = await _context.MovieTheaters.FirstOrDefaultAsync(m => m.MovieTheaterId == id);
            if (movieTheaters == null)
            {
                TempData["Error"] = "❌ Không tìm thấy rạp chiếu!";
                return RedirectToAction(nameof(Index));
            }

            var usageCount = await _context.CinemaTheaters.CountAsync(ct => ct.MovieTheaterId == id);
            ViewBag.UsageCount = usageCount;

            var province = await _context.Provinces.FirstOrDefaultAsync(p => p.ProvinceId == movieTheaters.ProvinceId);
            ViewBag.ProvinceName = province?.Name ?? "Không xác định";

            return View(movieTheaters);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            try
            {
                var inUse = await _context.CinemaTheaters.AnyAsync(ct => ct.MovieTheaterId == id);
                if (inUse)
                {
                    TempData["Error"] = "⛔ Không thể xóa vì rạp này đang có phòng chiếu!";
                    return RedirectToAction(nameof(Index));
                }

                var movieTheaters = await _context.MovieTheaters.FindAsync(id);
                if (movieTheaters != null)
                {
                    _context.MovieTheaters.Remove(movieTheaters);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "🗑️ Đã xóa rạp chiếu thành công!";
                }
                else
                {
                    TempData["Error"] = "❌ Không tìm thấy rạp chiếu!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Lỗi khi xóa: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private void LoadDropdowns()
        {
            ViewBag.ProvinceId = new SelectList(_context.Provinces.OrderBy(p => p.Name), "ProvinceId", "Name");
        }

        private async Task<string> GenerateNewIdAsync()
        {
            var last = await _context.MovieTheaters
                .OrderByDescending(mt => mt.MovieTheaterId)
                .FirstOrDefaultAsync();

            if (last == null) return "MT001";

            var lastNumber = int.Parse(last.MovieTheaterId.Substring(2));
            return $"MT{(lastNumber + 1):D3}";
        }
    }
}
