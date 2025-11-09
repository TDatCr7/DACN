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
    public class SeatTypesController : Controller
    {
        private readonly CinemaContext _context;

        public SeatTypesController(CinemaContext context)
        {
            _context = context;
        }

        // GET: SeatTypes
        public async Task<IActionResult> Index(string searchString)
        {
            var seatTypes = from s in _context.SeatTypes
                            select s;

            // Nếu có chuỗi tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.Trim().ToLower();

                seatTypes = seatTypes.Where(s =>
                    s.Name.ToLower().Contains(searchString) ||
                    s.Price.ToString().Contains(searchString)
                );
            }

            seatTypes = seatTypes.OrderBy(s => s.SeatTypeId);
            return View(await seatTypes.ToListAsync());
        }

        // GET: SeatTypes/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seatTypes = await _context.SeatTypes
                .FirstOrDefaultAsync(m => m.SeatTypeId == id);
            if (seatTypes == null)
            {
                return NotFound();
            }

            return View(seatTypes);
        }

        // GET: SeatTypes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: SeatTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SeatTypeId,Name,Price")] SeatTypes seatTypes)
        {
            if (ModelState.IsValid)
            {
                _context.Add(seatTypes);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(seatTypes);
        }

        // GET: SeatTypes/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seatTypes = await _context.SeatTypes.FindAsync(id);
            if (seatTypes == null)
            {
                return NotFound();
            }
            return View(seatTypes);
        }

        // POST: SeatTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("SeatTypeId,Name,Price")] SeatTypes seatTypes)
        {
            if (id != seatTypes.SeatTypeId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(seatTypes);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SeatTypesExists(seatTypes.SeatTypeId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(seatTypes);
        }

        // GET: SeatTypes/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seatTypes = await _context.SeatTypes
                .FirstOrDefaultAsync(m => m.SeatTypeId == id);
            if (seatTypes == null)
            {
                return NotFound();
            }

            return View(seatTypes);
        }

        // POST: SeatTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var seatTypes = await _context.SeatTypes.FindAsync(id);
            if (seatTypes != null)
            {
                _context.SeatTypes.Remove(seatTypes);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SeatTypesExists(string id)
        {
            return _context.SeatTypes.Any(e => e.SeatTypeId == id);
        }
    }
}
