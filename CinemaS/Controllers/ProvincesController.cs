using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProvincesController : Controller
    {
        private readonly CinemaContext _context;

        public ProvincesController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Provinces
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var provinces = from p in _context.Provinces
                            select p;

            if (!string.IsNullOrEmpty(searchString))
            {
                provinces = provinces.Where(p =>
                    p.Name.Contains(searchString) ||
                    p.ProvinceId.Contains(searchString));
            }

            var result = await provinces.OrderBy(p => p.Name).ToListAsync();
            return View(result);
        }

        // API Search (AJAX)
        [HttpGet]
        public async Task<IActionResult> SearchProvinces(string searchString)
        {
            var query = _context.Provinces.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(p =>
                    p.Name.Contains(searchString) ||
                    p.ProvinceId.Contains(searchString));
            }

            var results = await query
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.ProvinceId,
                    p.Name,
                    p.CreatedAt,
                    p.UpdatedAt
                }).ToListAsync();

            return Json(results);
        }

        // GET: Provinces/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                TempData["Error"] = "❌ Không tìm thấy mã tỉnh/thành phố!";
                return RedirectToAction(nameof(Index));
            }

            var province = await _context.Provinces
                .FirstOrDefaultAsync(p => p.ProvinceId == id);
            if (province == null)
            {
                TempData["Error"] = "❌ Tỉnh/Thành phố không tồn tại!";
                return RedirectToAction(nameof(Index));
            }

            return View(province);
        }

        // GET: Provinces/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Provinces/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProvinceId,Name,CreatedAt,UpdatedAt")] Provinces province)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "❌ Dữ liệu không hợp lệ! Vui lòng kiểm tra lại.";
                return View(province);
            }

            try
            {
                _context.Add(province);
                await _context.SaveChangesAsync();
                TempData["Message"] = $"✅ Đã tạo '{province.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"❌ Lỗi khi tạo: {ex.Message}";
                return View(province);
            }
        }

        // GET: Provinces/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                TempData["Error"] = "❌ Không tìm thấy mã tỉnh/thành phố!";
                return RedirectToAction(nameof(Index));
            }

            var province = await _context.Provinces.FindAsync(id);
            if (province == null)
            {
                TempData["Error"] = "❌ Không tìm thấy tỉnh/thành phố!";
                return RedirectToAction(nameof(Index));
            }

            return View(province);
        }

        // POST: Provinces/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("ProvinceId,Name,CreatedAt,UpdatedAt")] Provinces province)
        {
            if (id != province.ProvinceId)
            {
                TempData["Error"] = "❌ Mã không khớp!";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Error = "❌ Dữ liệu không hợp lệ! Vui lòng kiểm tra lại.";
                return View(province);
            }

            try
            {
                _context.Update(province);
                await _context.SaveChangesAsync();
                TempData["Message"] = "✅ Cập nhật thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Provinces.Any(p => p.ProvinceId == id))
                {
                    TempData["Error"] = "❌ Không tìm thấy bản ghi!";
                    return RedirectToAction(nameof(Index));
                }
                ViewBag.Error = "❌ Lỗi đồng bộ dữ liệu! Vui lòng thử lại.";
                return View(province);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"❌ Lỗi khi cập nhật: {ex.Message}";
                return View(province);
            }
        }

        // GET: Provinces/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                TempData["Error"] = "❌ Không tìm thấy mã tỉnh/thành phố!";
                return RedirectToAction(nameof(Index));
            }

            var province = await _context.Provinces
                .FirstOrDefaultAsync(p => p.ProvinceId == id);
            if (province == null)
            {
                TempData["Error"] = "❌ Không tìm thấy tỉnh/thành phố!";
                return RedirectToAction(nameof(Index));
            }

            return View(province);
        }

        // POST: Provinces/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var province = await _context.Provinces.FindAsync(id);
            if (province == null)
            {
                TempData["Error"] = "❌ Không tìm thấy bản ghi!";
                return RedirectToAction(nameof(Index));
            }

            _context.Provinces.Remove(province);
            await _context.SaveChangesAsync();
            TempData["Message"] = "🗑️ Đã xóa thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}
