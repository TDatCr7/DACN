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
        public IActionResult Create()
        {
            return View();
        }

        // POST: TicketTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TicketTypeId,Name,Description,Price")] TicketTypes ticketTypes)
        {
            if (ModelState.IsValid)
            {
                _context.Add(ticketTypes);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Tạo loại vé mới thành công!";
                return RedirectToAction(nameof(Index));
            }
            TempData["Error"] = "Không thể tạo loại vé. Vui lòng kiểm tra lại thông tin.";
            return View(ticketTypes);
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
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(ticketTypes);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "Cập nhật loại vé thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TicketTypesExists(ticketTypes.TicketTypeId))
                    {
                        TempData["Error"] = "Loại vé không tồn tại.";
                        return NotFound();
                    }
                    else
                    {
                        TempData["Error"] = "Có lỗi xảy ra khi cập nhật loại vé.";
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            TempData["Error"] = "Không thể cập nhật loại vé. Vui lòng kiểm tra lại thông tin.";
            return View(ticketTypes);
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
            var ticketTypes = await _context.TicketTypes.FindAsync(id);
            if (ticketTypes != null)
            {
                _context.TicketTypes.Remove(ticketTypes);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Xóa loại vé thành công!";
            }
            else
            {
                TempData["Error"] = "Không tìm thấy loại vé cần xóa.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool TicketTypesExists(string id)
        {
            return _context.TicketTypes.Any(e => e.TicketTypeId == id);
        }
    }
}
