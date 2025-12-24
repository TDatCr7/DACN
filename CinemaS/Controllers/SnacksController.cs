using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;

namespace CinemaS.Controllers
{
    public class SnacksController : Controller
    {
        private readonly CinemaContext _context;
        private readonly IWebHostEnvironment _env;

        public SnacksController(CinemaContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Snacks/Management
        public IActionResult Management()
        {
            return View();
        }

        // GET: Snacks
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var snacks = from s in _context.Snacks
                         select s;

            if (!String.IsNullOrEmpty(searchString))
            {
                snacks = snacks.Where(s =>
                     s.Name!.Contains(searchString) ||
              s.SnackId!.Contains(searchString) ||
              s.Description!.Contains(searchString));
            }

            return View(await snacks.OrderBy(s => s.SnackId).ToListAsync());
        }

        // API: Search Snacks (AJAX)
        [HttpGet]
        public async Task<IActionResult> SearchSnacks(string searchString)
        {
            var snacks = from s in _context.Snacks
                         select s;

            if (!String.IsNullOrEmpty(searchString))
            {
                snacks = snacks.Where(s =>
                  s.Name!.Contains(searchString) ||
         s.SnackId!.Contains(searchString) ||
       s.Description!.Contains(searchString));
            }

            var results = await snacks
    .OrderBy(s => s.SnackId)
         .Select(s => new
         {
             s.SnackId,
             s.SnackTypeId,
             s.Name,
             s.Price,
             s.Image,
             s.Description,
             s.IsActive
         }).ToListAsync();

            return Json(results);
        }

        // GET: Snacks/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                TempData["Error"] = "❌ Không tìm thấy mã đồ ăn!";
                return RedirectToAction(nameof(Index));
            }

            var snacks = await _context.Snacks
           .FirstOrDefaultAsync(m => m.SnackId == id);

            if (snacks == null)
            {
                TempData["Error"] = "❌ Không tìm thấy đồ ăn!";
                return RedirectToAction(nameof(Index));
            }

            // Lấy tên loại đồ ăn
            var snackType = await _context.SnackTypes.FirstOrDefaultAsync(st => st.SnackTypeId == snacks.SnackTypeId);
            ViewBag.SnackTypeName = snackType?.Name ?? "Không xác định";

            return View(snacks);
        }

        // GET: Snacks/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                var nextId = await GenerateNewIdAsync();
                ViewBag.NextSnackId = nextId;
            }
            catch
            {
                ViewBag.NextSnackId = null;
            }

            LoadDropdowns();
            var model = new Snacks { IsActive = true };
            return View(model);
        }

        // POST: Snacks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SnackTypeId,Name,Price,Description,IsActive")] Snacks snacks, IFormFile? imageFile)
        {
            ModelState.Remove(nameof(snacks.SnackId));
            ModelState.Remove(nameof(snacks.Image));

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(snacks);
            }

            try
            {
                var snackTypeExists = await _context.SnackTypes.AnyAsync(st => st.SnackTypeId == snacks.SnackTypeId);
                if (!snackTypeExists)
                {
                    TempData["Error"] = "❌ Loại đồ ăn không tồn tại!";
                    LoadDropdowns();
                    return View(snacks);
                }

                snacks.SnackId = await GenerateNewIdAsync();

                if (imageFile != null && imageFile.Length > 0)
                {
                    if (imageFile.Length > 5 * 1024 * 1024)
                    {
                        TempData["Error"] = "❌ Kích thước ảnh không được vượt quá 5MB!";
                        LoadDropdowns();
                        return View(snacks);
                    }

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["Error"] = "❌ Chỉ chấp nhận file ảnh JPG, PNG, GIF!";
                        LoadDropdowns();
                        return View(snacks);
                    }

                    snacks.Image = await SaveImageAsync(imageFile);
                }
                else
                {
                    snacks.Image = "/images/snacks/default-snack.png";
                }

                _context.Add(snacks);
                await _context.SaveChangesAsync();

                TempData["Message"] = $"✅ Tạo đồ ăn '{snacks.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                LoadDropdowns();
                TempData["Error"] = ex.InnerException == null
             ? $"❌ Lỗi: {ex.Message}"
      : $"❌ Lỗi: {ex.InnerException.Message}";
                return View(snacks);
            }
        }

        // GET: Snacks/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                TempData["Error"] = "❌ Không tìm thấy mã đồ ăn!";
                return RedirectToAction(nameof(Index));
            }

            var snacks = await _context.Snacks.FindAsync(id);
            if (snacks == null)
            {
                TempData["Error"] = "❌ Không tìm thấy đồ ăn!";
                return RedirectToAction(nameof(Index));
            }

            LoadDropdowns();
            return View(snacks);
        }

        // POST: Snacks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("SnackId,SnackTypeId,Name,Price,Image,Description,IsActive")] Snacks snacks, IFormFile? imageFile)
        {
            if (id != snacks.SnackId)
            {
                TempData["Error"] = "❌ Mã đồ ăn không khớp!";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(snacks);
            }

            try
            {
                var existingSnack = await _context.Snacks
                    .AsNoTracking()
                 .FirstOrDefaultAsync(s => s.SnackId == id);

                if (existingSnack == null)
                {
                    TempData["Error"] = "❌ Không tìm thấy đồ ăn!";
                    return RedirectToAction(nameof(Index));
                }

                if (imageFile != null && imageFile.Length > 0)
                {
                    if (imageFile.Length > 5 * 1024 * 1024)
                    {
                        TempData["Error"] = "❌ Kích thước ảnh không được vượt quá 5MB!";
                        LoadDropdowns();
                        return View(snacks);
                    }

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["Error"] = "❌ Chỉ chấp nhận file ảnh JPG, PNG, GIF!";
                        LoadDropdowns();
                        return View(snacks);
                    }

                    if (!string.IsNullOrEmpty(existingSnack.Image) &&
                        !existingSnack.Image.Contains("default-snack.png"))
                    {
                        TryDeletePhysicalFile(existingSnack.Image);
                    }

                    snacks.Image = await SaveImageAsync(imageFile);
                }
                else
                {
                    snacks.Image = existingSnack.Image;
                }

                var inUse = await _context.DetailBookingSnacks
           .AnyAsync(dbs => dbs.SnackId == id);

                if (inUse)
                {
                    TempData["Warning"] = "⚠️ Đồ ăn này đang được sử dụng trong đơn hàng. Cập nhật sẽ ảnh hưởng đến các đơn hàng hiện có.";
                }

                _context.Update(snacks);
                await _context.SaveChangesAsync();

                TempData["Message"] = "✅ Cập nhật đồ ăn thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SnacksExists(snacks.SnackId))
                {
                    TempData["Error"] = "❌ Đồ ăn không tồn tại!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                LoadDropdowns();
                TempData["Error"] = $"❌ Lỗi: {ex.Message}";
                return View(snacks);
            }
        }

        // GET: Snacks/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                TempData["Error"] = "❌ Không tìm thấy mã đồ ăn!";
                return RedirectToAction(nameof(Index));
            }

            var snacks = await _context.Snacks
                          .FirstOrDefaultAsync(m => m.SnackId == id);

            if (snacks == null)
            {
                TempData["Error"] = "❌ Không tìm thấy đồ ăn!";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra xem có đang được sử dụng không
            var usageCount = await _context.DetailBookingSnacks
   .CountAsync(dbs => dbs.SnackId == id);
            ViewBag.UsageCount = usageCount;

            // Lấy tên loại đồ ăn
            var snackType = await _context.SnackTypes.FirstOrDefaultAsync(st => st.SnackTypeId == snacks.SnackTypeId);
            ViewBag.SnackTypeName = snackType?.Name ?? "Không xác định";

            return View(snacks);
        }

        // POST: Snacks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            try
            {
                var inUse = await _context.DetailBookingSnacks
                .AnyAsync(dbs => dbs.SnackId == id);

                if (inUse)
                {
                    TempData["Error"] = "⛔ Không thể xóa vì đồ ăn này đang được sử dụng trong đơn hàng!";
                    return RedirectToAction(nameof(Index));
                }

                var snacks = await _context.Snacks.FindAsync(id);
                if (snacks != null)
                {
                    if (!string.IsNullOrEmpty(snacks.Image) &&
                          !snacks.Image.Contains("default-snack.png"))
                    {
                        TryDeletePhysicalFile(snacks.Image);
                    }

                    _context.Snacks.Remove(snacks);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "🗑️ Đã xóa đồ ăn thành công!";
                }
                else
                {
                    TempData["Error"] = "❌ Không tìm thấy đồ ăn!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Lỗi khi xóa: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // ================== HELPER METHODS ==================

        private bool SnacksExists(string id)
        {
            return _context.Snacks.Any(e => e.SnackId == id);
        }

        // Load SnackTypes dropdown
        private void LoadDropdowns()
        {
            ViewBag.SnackTypeId = new SelectList(
       _context.SnackTypes.OrderBy(st => st.Name),
     "SnackTypeId", "Name");
        }

        // Auto-generate ID: SNK001, SNK002, SNK003...
        private async Task<string> GenerateNewIdAsync()
        {
            var last = await _context.Snacks
                        .OrderByDescending(s => s.SnackId)
                        .FirstOrDefaultAsync();

            if (last == null) return "SNK001";

            // Parse số từ ID cuối (VD: SNK001 -> 1)
            var lastNumber = int.Parse(last.SnackId.Substring(3));
            return $"SNK{(lastNumber + 1):D3}";
        }

        // Lưu ảnh vào wwwroot/images/snacks/
        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "snacks");
            Directory.CreateDirectory(uploadsFolder);

            // Tạo tên file unique bằng Guid
            var uniqueFileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/images/snacks/{uniqueFileName}";
        }

        // Xóa file ảnh vật lý
        private void TryDeletePhysicalFile(string? webPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webPath)) return;

                var trimmed = webPath.StartsWith("/") ? webPath[1..] : webPath;
                var fullPath = Path.Combine(_env.WebRootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch (Exception)
            {
                // Silently fail
            }
        }
    }
}
