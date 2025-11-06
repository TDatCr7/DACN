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
    public class SnackTypesController : Controller
    {
    private readonly CinemaContext _context;

    public SnackTypesController(CinemaContext context)
        {
    _context = context;
   }

        // GET: SnackTypes
  public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

    var snackTypes = from st in _context.SnackTypes
     select st;

            if (!String.IsNullOrEmpty(searchString))
         {
   snackTypes = snackTypes.Where(st => 
       st.Name!.Contains(searchString) ||
          st.SnackTypeId!.Contains(searchString) ||
             st.Description!.Contains(searchString));
            }

    return View(await snackTypes.OrderBy(st => st.SnackTypeId).ToListAsync());
        }

        // API: Search SnackTypes (AJAX)
        [HttpGet]
        public async Task<IActionResult> SearchSnackTypes(string searchString)
        {
        var snackTypes = from st in _context.SnackTypes
   select st;

            if (!String.IsNullOrEmpty(searchString))
            {
    snackTypes = snackTypes.Where(st => 
           st.Name!.Contains(searchString) ||
       st.SnackTypeId!.Contains(searchString) ||
       st.Description!.Contains(searchString));
            }

            var results = await snackTypes
      .OrderBy(st => st.SnackTypeId)
         .Select(st => new
    {
      st.SnackTypeId,
 st.Name,
      st.Description
  }).ToListAsync();

    return Json(results);
        }

        // GET: SnackTypes/Details/5
        public async Task<IActionResult> Details(string id)
  {
     if (id == null)
     {
       TempData["Error"] = "❌ Không tìm thấy mã loại đồ ăn!";
    return RedirectToAction(nameof(Index));
            }

            var snackTypes = await _context.SnackTypes
   .FirstOrDefaultAsync(m => m.SnackTypeId == id);
            
        if (snackTypes == null)
    {
      TempData["Error"] = "❌ Không tìm thấy loại đồ ăn!";
     return RedirectToAction(nameof(Index));
 }

// Đếm số đồ ăn thuộc loại này
            var snackCount = await _context.Snacks
              .CountAsync(s => s.SnackTypeId == id);
   ViewBag.SnackCount = snackCount;

            return View(snackTypes);
        }

        // GET: SnackTypes/Create
    public IActionResult Create()
        {
 return View();
        }

// POST: SnackTypes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description")] SnackTypes snackTypes)
        {
            // Bỏ qua validate ID vì tự sinh
          ModelState.Remove(nameof(snackTypes.SnackTypeId));

            Console.WriteLine("=== CREATE SNACK TYPE ===");
          Console.WriteLine($"Name: {snackTypes?.Name}");
            Console.WriteLine($"Description: {snackTypes?.Description}");

    if (!ModelState.IsValid)
            {
          Console.WriteLine("❌ ModelState INVALID");
        foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
  {
            Console.WriteLine($"   - {error.ErrorMessage}");
      }
            TempData["Error"] = "❌ Vui lòng kiểm tra lại thông tin đã nhập!";
        return View(snackTypes);
      }

try
            {
 // Kiểm tra tên loại đã tồn tại chưa
    var nameExists = await _context.SnackTypes
      .AnyAsync(st => st.Name == snackTypes.Name);
    
                if (nameExists)
   {
              TempData["Error"] = "❌ Tên loại đồ ăn đã tồn tại!";
         return View(snackTypes);
          }

           // Auto-generate ID: ST001, ST002, ST003...
    snackTypes.SnackTypeId = await GenerateNewIdAsync();
     Console.WriteLine($"✅ Generated ID: {snackTypes.SnackTypeId}");

  _context.Add(snackTypes);
       await _context.SaveChangesAsync();

           TempData["Message"] = $"✅ Tạo loại đồ ăn '{snackTypes.Name}' thành công!";
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
? $"❌ Lỗi: {ex.Message}"
            : $"❌ Lỗi: {ex.InnerException.Message}";
 return View(snackTypes);
            }
    }

    // GET: SnackTypes/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
  if (id == null)
 {
                TempData["Error"] = "❌ Không tìm thấy mã loại đồ ăn!";
       return RedirectToAction(nameof(Index));
      }

      var snackTypes = await _context.SnackTypes.FindAsync(id);
         if (snackTypes == null)
      {
    TempData["Error"] = "❌ Không tìm thấy loại đồ ăn!";
   return RedirectToAction(nameof(Index));
      }

return View(snackTypes);
 }

        // POST: SnackTypes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("SnackTypeId,Name,Description")] SnackTypes snackTypes)
        {
  if (id != snackTypes.SnackTypeId)
         {
      TempData["Error"] = "❌ Mã loại đồ ăn không khớp!";
         return RedirectToAction(nameof(Index));
   }

    if (!ModelState.IsValid)
     {
          Console.WriteLine("❌ ModelState INVALID");
            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
  {
      Console.WriteLine($"   - {error.ErrorMessage}");
            }
  TempData["Error"] = "❌ Vui lòng kiểm tra lại thông tin đã nhập!";
      return View(snackTypes);
            }

      try
  {
                // Kiểm tra xem có đồ ăn nào đang sử dụng loại này không
  var inUse = await _context.Snacks
      .AnyAsync(s => s.SnackTypeId == id);

    if (inUse)
  {
TempData["Warning"] = "⚠️ Loại đồ ăn này đang được sử dụng. Cập nhật sẽ ảnh hưởng đến các đồ ăn hiện có.";
      }

            // Kiểm tra tên trùng (trừ chính nó)
    var nameExists = await _context.SnackTypes
.AnyAsync(st => st.Name == snackTypes.Name && st.SnackTypeId != id);
         
        if (nameExists)
    {
        TempData["Error"] = "❌ Tên loại đồ ăn đã tồn tại!";
      return View(snackTypes);
           }

    _context.Update(snackTypes);
                await _context.SaveChangesAsync();

           TempData["Message"] = "✅ Cập nhật loại đồ ăn thành công!";
       return RedirectToAction(nameof(Index));
}
            catch (DbUpdateConcurrencyException)
  {
          if (!SnackTypesExists(snackTypes.SnackTypeId))
                {
    TempData["Error"] = "❌ Loại đồ ăn không tồn tại!";
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
            TempData["Error"] = $"❌ Lỗi: {ex.Message}";
    return View(snackTypes);
         }
   }

        // GET: SnackTypes/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
     if (id == null)
     {
   TempData["Error"] = "❌ Không tìm thấy mã loại đồ ăn!";
     return RedirectToAction(nameof(Index));
 }

     var snackTypes = await _context.SnackTypes
       .FirstOrDefaultAsync(m => m.SnackTypeId == id);
    
     if (snackTypes == null)
       {
   TempData["Error"] = "❌ Không tìm thấy loại đồ ăn!";
       return RedirectToAction(nameof(Index));
      }

            // Kiểm tra xem có đang được sử dụng không
            var usageCount = await _context.Snacks
       .CountAsync(s => s.SnackTypeId == id);
  ViewBag.UsageCount = usageCount;

       return View(snackTypes);
        }

        // POST: SnackTypes/Delete/5
  [HttpPost, ActionName("Delete")]
      [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
try
   {
       // Kiểm tra xem có đang được sử dụng không
          var inUse = await _context.Snacks
     .AnyAsync(s => s.SnackTypeId == id);

 if (inUse)
      {
     TempData["Error"] = "⛔ Không thể xóa vì loại đồ ăn này đang được sử dụng!";
       return RedirectToAction(nameof(Index));
     }

                var snackTypes = await _context.SnackTypes.FindAsync(id);
    if (snackTypes != null)
         {
        _context.SnackTypes.Remove(snackTypes);
      await _context.SaveChangesAsync();
      TempData["Message"] = "🗑️ Đã xóa loại đồ ăn thành công!";
   }
       else
    {
          TempData["Error"] = "❌ Không tìm thấy loại đồ ăn!";
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

        private bool SnackTypesExists(string id)
 {
      return _context.SnackTypes.Any(e => e.SnackTypeId == id);
        }

        // Auto-generate ID: ST001, ST002, ST003...
        private async Task<string> GenerateNewIdAsync()
        {
       var last = await _context.SnackTypes
     .OrderByDescending(st => st.SnackTypeId)
         .FirstOrDefaultAsync();

          if (last == null) return "ST001";

// Parse số từ ID cuối (VD: ST001 -> 1)
       var lastNumber = int.Parse(last.SnackTypeId.Substring(2));
 return $"ST{(lastNumber + 1):D3}";
        }
    }
}
