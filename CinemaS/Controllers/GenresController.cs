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
    public class GenresController : Controller
    {
        private readonly CinemaContext _context;

        public GenresController(CinemaContext context)
        {
            _context = context;
        }

        // HÀM TẠO ID THỂ LOẠI TIẾP THEO
        private string GenerateNextGenresId()
        {
            // Lấy ID lớn nhất hiện có (theo thứ tự string)
            var lastId = _context.Genres
                                 .OrderByDescending(g => g.GenresId)
                                 .Select(g => g.GenresId)
                                 .FirstOrDefault();

            if (string.IsNullOrEmpty(lastId))
            {
                // Bảng rỗng → ID đầu tiên
                return "GR001";
            }

            // Tách prefix chữ + phần số
            var prefix = new string(lastId.TakeWhile(c => !char.IsDigit(c)).ToArray());
            var numberPart = new string(lastId.Skip(prefix.Length).ToArray());

            if (!int.TryParse(numberPart, out var number))
            {
                // Nếu không parse được thì giữ nguyên hoặc tự chọn fallback
                return lastId;
            }

            number++;

            // Giữ nguyên số lượng chữ số như cũ (ví dụ 3 chữ số → 001, 002,…)
            var formattedNumber = number.ToString(new string('0', numberPart.Length));

            return prefix + formattedNumber;
        }

        // GET: Genres
        public async Task<IActionResult> Index(int page = 1)
        {
            // ===== PHÂN TRANG =====
            const int PageSize = 8;                 // mỗi trang 8 thể loại
            if (page < 1) page = 1;                 // chống page âm/0

            // Tổng số dòng
            var totalItems = await _context.Genres.CountAsync();

            // Tổng số trang (ceil)
            var totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
            if (totalPages < 1) totalPages = 1;     // nếu bảng rỗng vẫn để 1 trang

            // Nếu page vượt quá totalPages thì kéo về trang cuối
            if (page > totalPages) page = totalPages;

            // Lấy dữ liệu theo trang (nên có OrderBy để ổn định)
            var items = await _context.Genres
                .AsNoTracking()
                .OrderBy(g => g.GenresId)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // ===== GỬI DỮ LIỆU PHÂN TRANG QUA VIEWBAG =====
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = PageSize;
            ViewBag.TotalItems = totalItems;

            return View(items);
        }


        // GET: Genres/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var genres = await _context.Genres
                .FirstOrDefaultAsync(m => m.GenresId == id);
            if (genres == null) return NotFound();

            return View(genres);
        }

        // GET: Genres/Create
        public IActionResult Create()
        {
            // Gợi ý ID tiếp theo
            ViewBag.SuggestedId = GenerateNextGenresId();
            return View();
        }

        // POST: Genres/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("GenresId,Name,Description")] Genres genres)
        {
            if (ModelState.IsValid)
            {
                _context.Add(genres);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Khi lỗi validate, không cần gợi ý lại nếu user đã nhập
            return View(genres);
        }

        // GET: Genres/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var genres = await _context.Genres.FindAsync(id);
            if (genres == null) return NotFound();

            return View(genres);
        }

        // POST: Genres/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("GenresId,Name,Description")] Genres genres)
        {
            if (id != genres.GenresId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(genres);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GenresExists(genres.GenresId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(genres);
        }

        // GET: Genres/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var genres = await _context.Genres
                .FirstOrDefaultAsync(m => m.GenresId == id);
            if (genres == null) return NotFound();

            return View(genres);
        }

        // POST: Genres/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var genres = await _context.Genres.FindAsync(id);
            if (genres != null)
            {
                _context.Genres.Remove(genres);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool GenresExists(string id)
        {
            return _context.Genres.Any(e => e.GenresId == id);
        }
    }
}
