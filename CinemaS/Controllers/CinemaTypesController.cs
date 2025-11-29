using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CinemaTypesController : Controller
    {
        private readonly CinemaContext _context;

        public CinemaTypesController(CinemaContext context)
        {
            _context = context;
        }

        // GET: CinemaTypes
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var cinemaTypes = from c in _context.CinemaTypes
                              select c;

            if (!String.IsNullOrEmpty(searchString))
            {
                cinemaTypes = cinemaTypes.Where(c => c.Name!.Contains(searchString) ||
                   c.Code!.Contains(searchString));
            }

            return View(await cinemaTypes.ToListAsync());
        }

        // API: Search CinemaTypes (AJAX)
        [HttpGet]
        public async Task<IActionResult> SearchCinemaTypes(string searchString)
        {
            var cinemaTypes = from c in _context.CinemaTypes
                              select c;

            if (!String.IsNullOrEmpty(searchString))
            {
                cinemaTypes = cinemaTypes.Where(c => c.Name!.Contains(searchString) ||
                       c.Code!.Contains(searchString));
            }

            var results = await cinemaTypes.Select(c => new
            {
                c.CinemaTypeId,
                c.Name,
                c.Description,
                c.Code
            }).ToListAsync();

            return Json(results);
        }

        // GET: CinemaTypes/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cinemaTypes = await _context.CinemaTypes
                  .FirstOrDefaultAsync(m => m.CinemaTypeId == id);
            if (cinemaTypes == null)
            {
                return NotFound();
            }

            return View(cinemaTypes);
        }

        // GET: CinemaTypes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: CinemaTypes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Code")] CinemaTypes cinemaTypes)
        {
            // Remove ID from ModelState validation
            ModelState.Remove(nameof(cinemaTypes.CinemaTypeId));

            Console.WriteLine("=== CREATE CINEMA TYPE ===");
            Console.WriteLine($"Name: {cinemaTypes?.Name}");
            Console.WriteLine($"Code: {cinemaTypes?.Code}");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("❌ ModelState INVALID");
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin đã nhập!";
                return View(cinemaTypes);
            }

            try
            {
                // Auto-generate ID: CTY001, CTY002, CTY003...
                cinemaTypes!.CinemaTypeId = await GenerateNewCinemaTypeIdAsync();
                Console.WriteLine($"Generated ID: {cinemaTypes.CinemaTypeId}");

                _context.Add(cinemaTypes);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"✅ Tạo loại phòng chiếu '{cinemaTypes.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EXCEPTION: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ INNER: {ex.InnerException.Message}");
                }

                TempData["Error"] = ex.InnerException == null
                ? $"Lỗi: {ex.Message}"
                 : $"Lỗi: {ex.Message} | Chi tiết: {ex.InnerException.Message}";
                return View(cinemaTypes);
            }
        }

        // GET: CinemaTypes/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cinemaTypes = await _context.CinemaTypes.FindAsync(id);
            if (cinemaTypes == null)
            {
                return NotFound();
            }
            return View(cinemaTypes);
        }

        // POST: CinemaTypes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("CinemaTypeId,Name,Description,Code")] CinemaTypes cinemaTypes)
        {
            if (id != cinemaTypes.CinemaTypeId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin đã nhập!";
                return View(cinemaTypes);
            }

            try
            {
                // Check if this CinemaType is being used
                var inUse = await _context.CinemaTheaters
         .AnyAsync(ct => ct.CinemaTypeId == id);

                if (inUse)
                {
                    TempData["Warning"] = "⚠️ Loại phòng chiếu này đang được sử dụng. Cập nhật sẽ ảnh hưởng đến các phòng chiếu hiện có.";
                }

                _context.Update(cinemaTypes);
                await _context.SaveChangesAsync();

                TempData["Message"] = "✅ Cập nhật loại phòng chiếu thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CinemaTypesExists(cinemaTypes.CinemaTypeId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EXCEPTION: {ex.Message}");
                TempData["Error"] = $"Lỗi: {ex.Message}";
                return View(cinemaTypes);
            }
        }

        // GET: CinemaTypes/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cinemaTypes = await _context.CinemaTheaters
          .FirstOrDefaultAsync(m => m.CinemaTypeId == id);
            if (cinemaTypes == null)
            {
                return NotFound();
            }

            // Check if being used
            var usageCount = await _context.CinemaTheaters
         .CountAsync(ct => ct.CinemaTypeId == id);
            ViewBag.UsageCount = usageCount;

            return View(cinemaTypes);
        }

        // POST: CinemaTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            try
            {
                // Check if being used
                var inUse = await _context.CinemaTheaters
            .AnyAsync(ct => ct.CinemaTypeId == id);

                if (inUse)
                {
                    TempData["Error"] = "⛔ Không thể xóa vì đang được sử dụng bởi các phòng chiếu khác!";
                    return RedirectToAction(nameof(Index));
                }

                var cinemaTypes = await _context.CinemaTypes.FindAsync(id);
                if (cinemaTypes != null)
                {
                    _context.CinemaTypes.Remove(cinemaTypes);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "🗑️ Đã xóa loại phòng chiếu thành công!";
                }
                else
                {
                    TempData["Error"] = "Không tìm thấy loại phòng chiếu!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ DELETE ERROR: {ex.Message}");
                TempData["Error"] = $"Lỗi khi xóa: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool CinemaTypesExists(string id)
        {
            return _context.CinemaTypes.Any(e => e.CinemaTypeId == id);
        }

        // Auto-generate ID: CTY001, CTY002, CTY003...
        private async Task<string> GenerateNewCinemaTypeIdAsync()
        {
            var last = await _context.CinemaTypes
              .OrderByDescending(ct => ct.CinemaTypeId)
       .FirstOrDefaultAsync();

            if (last == null) return "CTY001";

            var num = int.Parse(last.CinemaTypeId.Substring(3));
            return $"CTY{(num + 1):D3}";
        }
    }
}
