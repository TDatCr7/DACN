using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;

namespace CinemaS.Controllers
{
    public class MovieTheatersController : Controller
    {
        private readonly CinemaContext _context;

        public MovieTheatersController(CinemaContext context)
        {
            _context = context;
        }
        // GET: MovieTheaters/Management
        public IActionResult Management()
        {
            return View();
        }

        // GET: MovieTheaters
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var movieTheaters = from mt in _context.MovieTheaters
                                select mt;

            if (!String.IsNullOrEmpty(searchString))
            {
                movieTheaters = movieTheaters.Where(mt =>
    mt.Name!.Contains(searchString) ||
    mt.Address!.Contains(searchString) ||
 mt.MovieTheaterId!.Contains(searchString));
            }

            return View(await movieTheaters.OrderBy(mt => mt.MovieTheaterId).ToListAsync());
        }

        // API: Search MovieTheaters (AJAX)
        [HttpGet]
        public async Task<IActionResult> SearchMovieTheaters(string searchString)
        {
            var movieTheaters = from mt in _context.MovieTheaters
                                select mt;

            if (!String.IsNullOrEmpty(searchString))
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
           }).ToListAsync();

            return Json(results);
        }

        // GET: MovieTheaters/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                TempData["Error"] = "❌ Không tìm thấy mã rạp chiếu!";
                return RedirectToAction(nameof(Index));
            }

            var movieTheaters = await _context.MovieTheaters
           .FirstOrDefaultAsync(m => m.MovieTheaterId == id);

            if (movieTheaters == null)
            {
                TempData["Error"] = "❌ Không tìm thấy rạp chiếu!";
                return RedirectToAction(nameof(Index));
            }

            // Lấy tên tỉnh/thành phố
            var province = await _context.Provinces.FirstOrDefaultAsync(p => p.ProvinceId == movieTheaters.ProvinceId);
            ViewBag.ProvinceName = province?.Name ?? "Không xác định";

            return View(movieTheaters);
        }

        // GET: MovieTheaters/Create
        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        // POST: MovieTheaters/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Address,Hotline,Status,IFrameCode,ProvinceId")] MovieTheaters movieTheaters)
        {
            // Bỏ qua validate ID vì tự sinh
            ModelState.Remove(nameof(movieTheaters.MovieTheaterId));

            Console.WriteLine("=== CREATE MOVIE THEATER ===");
            Console.WriteLine($"Name: {movieTheaters?.Name}");
            Console.WriteLine($"ProvinceId: {movieTheaters?.ProvinceId}");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("❌ ModelState INVALID");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"   - {error.ErrorMessage}");
                }
                LoadDropdowns();
                TempData["Error"] = "❌ Vui lòng kiểm tra lại thông tin đã nhập!";
                return View(movieTheaters);
            }

            try
            {
                // Kiểm tra tỉnh/thành phố có tồn tại
                var provinceExists = await _context.Provinces.AnyAsync(p => p.ProvinceId == movieTheaters.ProvinceId);
                if (!provinceExists)
                {
                    TempData["Error"] = "❌ Tỉnh/Thành phố không tồn tại!";
                    LoadDropdowns();
                    return View(movieTheaters);
                }

                // Auto-generate ID: MT001, MT002, MT003...
                movieTheaters.MovieTheaterId = await GenerateNewIdAsync();
                Console.WriteLine($"✅ Generated ID: {movieTheaters.MovieTheaterId}");

                // Set default status nếu chưa có
                if (movieTheaters.Status == null)
                {
                    movieTheaters.Status = 1; // Active
                }

                _context.Add(movieTheaters);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"✅ Tạo rạp chiếu '{movieTheaters.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EXCEPTION: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ INNER: {ex.InnerException.Message}");
                }

                LoadDropdowns();
                TempData["Error"] = ex.InnerException == null
              ? $"❌ Lỗi: {ex.Message}"
                       : $"❌ Lỗi: {ex.InnerException.Message}";
                return View(movieTheaters);
            }
        }

        // GET: MovieTheaters/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
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

        // POST: MovieTheaters/Edit/5
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
                Console.WriteLine("❌ ModelState INVALID");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"- {error.ErrorMessage}");
                }
                LoadDropdowns();
                TempData["Error"] = "❌ Vui lòng kiểm tra lại thông tin đã nhập!";
                return View(movieTheaters);
            }

            try
            {
                // Kiểm tra xem có phòng chiếu nào đang sử dụng rạp này không
                var inUse = await _context.CinemaTheaters
               .AnyAsync(ct => ct.MovieTheaterId == id);

                if (inUse)
                {
                    TempData["Warning"] = "⚠️ Rạp này đang có phòng chiếu. Cập nhật sẽ ảnh hưởng đến các phòng chiếu hiện có.";
                }

                _context.Update(movieTheaters);
                await _context.SaveChangesAsync();

                TempData["Message"] = "✅ Cập nhật rạp chiếu thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MovieTheatersExists(movieTheaters.MovieTheaterId))
                {
                    TempData["Error"] = "❌ Rạp chiếu không tồn tại!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EXCEPTION: {ex.Message}");
                LoadDropdowns();
                TempData["Error"] = $"❌ Lỗi: {ex.Message}";
                return View(movieTheaters);
            }
        }

        // GET: MovieTheaters/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                TempData["Error"] = "❌ Không tìm thấy mã rạp chiếu!";
                return RedirectToAction(nameof(Index));
            }

            var movieTheaters = await _context.MovieTheaters
             .FirstOrDefaultAsync(m => m.MovieTheaterId == id);

            if (movieTheaters == null)
            {
                TempData["Error"] = "❌ Không tìm thấy rạp chiếu!";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra xem có đang được sử dụng không
            var usageCount = await _context.CinemaTheaters
            .CountAsync(ct => ct.MovieTheaterId == id);
            ViewBag.UsageCount = usageCount;

            // Lấy tên tỉnh/thành phố
            var province = await _context.Provinces.FirstOrDefaultAsync(p => p.ProvinceId == movieTheaters.ProvinceId);
            ViewBag.ProvinceName = province?.Name ?? "Không xác định";

            return View(movieTheaters);
        }

        // POST: MovieTheaters/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            try
            {
                // Kiểm tra xem có đang được sử dụng không
                var inUse = await _context.CinemaTheaters
                .AnyAsync(ct => ct.MovieTheaterId == id);

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
                Console.WriteLine($"❌ DELETE ERROR: {ex.Message}");
                TempData["Error"] = $"❌ Lỗi khi xóa: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // ================== HELPER METHODS ==================

        private bool MovieTheatersExists(string id)
        {
            return _context.MovieTheaters.Any(e => e.MovieTheaterId == id);
        }

        // Load Provinces dropdown
        private void LoadDropdowns()
        {
            ViewBag.ProvinceId = new SelectList(
                 _context.Provinces.OrderBy(p => p.Name),
          "ProvinceId", "Name");
        }

        // Auto-generate ID: MT001, MT002, MT003...
        private async Task<string> GenerateNewIdAsync()
        {
            var last = await _context.MovieTheaters
              .OrderByDescending(mt => mt.MovieTheaterId)
              .FirstOrDefaultAsync();

            if (last == null) return "MT001";

            // Parse số từ ID cuối (VD: MT001 -> 1)
            var lastNumber = int.Parse(last.MovieTheaterId.Substring(2));
            return $"MT{(lastNumber + 1):D3}";
        }
    }
}
