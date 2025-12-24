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
    public class TicketTypesController : Controller
    {
        private readonly CinemaContext _context;

        public TicketTypesController(CinemaContext context)
        {
            _context = context;
        }

        // GET: TicketTypes
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var ticketTypes = from t in _context.TicketTypes
                              select t;

            if (!string.IsNullOrEmpty(searchString))
            {
                ticketTypes = ticketTypes.Where(t => 
                    t.TicketTypeId.Contains(searchString) ||
                    (t.Name != null && t.Name.Contains(searchString)) ||
                    (t.Description != null && t.Description.Contains(searchString))
                );
            }

            ticketTypes = ticketTypes.OrderBy(t => t.TicketTypeId);
            return View(await ticketTypes.ToListAsync());
        }

        // GET: TicketTypes/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticketTypes = await _context.TicketTypes
                .FirstOrDefaultAsync(m => m.TicketTypeId == id);
            if (ticketTypes == null)
            {
                return NotFound();
            }

            return View(ticketTypes);
        }

        // GET: TicketTypes/Create
        public async Task<IActionResult> Create()
        {
            try
            {
                var nextId = await GenerateNewTicketTypeIdAsync();
                ViewBag.NextTicketTypeId = nextId;
            }
            catch
            {
                ViewBag.NextTicketTypeId = null;
            }

            return View();
        }

        // POST: TicketTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Price")] TicketTypes ticketTypes)
        {
            ModelState.Remove(nameof(ticketTypes.TicketTypeId));

            if (!ModelState.IsValid)
            {
                return View(ticketTypes);
            }

            try
            {
                var nameExists = await _context.TicketTypes
                    .AnyAsync(tt => tt.Name == ticketTypes.Name);

                if (nameExists)
                {
                    TempData["Error"] = "❌ Tên loại vé đã tồn tại!";
                    return View(ticketTypes);
                }

                ticketTypes.TicketTypeId = await GenerateNewTicketTypeIdAsync();

                _context.Add(ticketTypes);
                await _context.SaveChangesAsync();
                
                TempData["Message"] = $"✅ Tạo loại vé '{ticketTypes.Name}' thành công! (Mã: {ticketTypes.TicketTypeId})";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Lỗi: {ex.InnerException?.Message ?? ex.Message}";
                return View(ticketTypes);
            }
        }

        // GET: TicketTypes/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticketTypes = await _context.TicketTypes.FindAsync(id);
            if (ticketTypes == null)
            {
                return NotFound();
            }
            return View(ticketTypes);
        }

        // POST: TicketTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("TicketTypeId,Name,Description,Price")] TicketTypes ticketTypes)
        {
            if (id != ticketTypes.TicketTypeId)
            {
                TempData["Error"] = "❌ Mã loại vé không khớp!";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(ticketTypes);
            }

            try
            {
                if (!ticketTypes.TicketTypeId.StartsWith("TT") || ticketTypes.TicketTypeId.Length != 5)
                {
                    TempData["Error"] = "❌ Mã loại vé phải theo định dạng TTxxx (VD: TT001)!";
                    return View(ticketTypes);
                }

                var inUse = await _context.Tickets
                    .AnyAsync(t => t.TicketTypeId == id);

                if (inUse)
                {
                    TempData["Warning"] = "⚠️ Loại vé này đang được sử dụng. Cập nhật sẽ ảnh hưởng đến các vé hiện có.";
                }

                var nameExists = await _context.TicketTypes
                    .AnyAsync(tt => tt.Name == ticketTypes.Name && tt.TicketTypeId != id);

                if (nameExists)
                {
                    TempData["Error"] = "❌ Tên loại vé đã tồn tại!";
                    return View(ticketTypes);
                }

                _context.Update(ticketTypes);
                await _context.SaveChangesAsync();
                TempData["Message"] = "✅ Cập nhật loại vé thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketTypesExists(ticketTypes.TicketTypeId))
                {
                    TempData["Error"] = "❌ Loại vé không tồn tại!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Lỗi: {ex.Message}";
                return View(ticketTypes);
            }
        }

        // GET: TicketTypes/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticketTypes = await _context.TicketTypes
                .FirstOrDefaultAsync(m => m.TicketTypeId == id);
            if (ticketTypes == null)
            {
                return NotFound();
            }

            return View(ticketTypes);
        }

        // POST: TicketTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            try
            {
                var ticketTypes = await _context.TicketTypes.FindAsync(id);
                if (ticketTypes == null)
                {
                    TempData["Error"] = "❌ Không tìm thấy loại vé cần xóa.";
                    return RedirectToAction(nameof(Index));
                }

                var inUse = await _context.Tickets.AnyAsync(t => t.TicketTypeId == id);
                if (inUse)
                {
                    TempData["Error"] = "❌ Không thể xóa loại vé đang được sử dụng!";
                    return RedirectToAction(nameof(Index));
                }

                _context.TicketTypes.Remove(ticketTypes);
                await _context.SaveChangesAsync();
                TempData["Message"] = "✅ Xóa loại vé thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Lỗi: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ================== HELPER METHODS ==================

        private bool TicketTypesExists(string id)
        {
            return _context.TicketTypes.Any(e => e.TicketTypeId == id);
        }

        private async Task<string> GenerateNewTicketTypeIdAsync()
        {
            var last = await _context.TicketTypes
                .OrderByDescending(tt => tt.TicketTypeId)
                .FirstOrDefaultAsync();

            if (last == null) return "TT001";

            var lastNumber = int.Parse(last.TicketTypeId.Substring(2));
            return $"TT{(lastNumber + 1):D3}";
        }
    }
}
