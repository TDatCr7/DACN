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
    public class PointHistoriesController : Controller
    {
        private readonly CinemaContext _context;

        public PointHistoriesController(CinemaContext context)
        {
            _context = context;
        }

        // GET: PointHistories
        public async Task<IActionResult> Index()
        {
            return View(await _context.PointHistories.ToListAsync());
        }

        // GET: PointHistories/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pointHistories = await _context.PointHistories
                .FirstOrDefaultAsync(m => m.PointHistoryId == id);
            if (pointHistories == null)
            {
                return NotFound();
            }

            return View(pointHistories);
        }

        // GET: PointHistories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: PointHistories/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PointHistoryId,UserId,InvoiceId,ChangeAmount,Reason,CreatedAt,UpdatedAt")] PointHistories pointHistories)
        {
            if (ModelState.IsValid)
            {
                _context.Add(pointHistories);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(pointHistories);
        }

        // GET: PointHistories/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pointHistories = await _context.PointHistories.FindAsync(id);
            if (pointHistories == null)
            {
                return NotFound();
            }
            return View(pointHistories);
        }

        // POST: PointHistories/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PointHistoryId,UserId,InvoiceId,ChangeAmount,Reason,CreatedAt,UpdatedAt")] PointHistories pointHistories)
        {
            if (id != pointHistories.PointHistoryId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pointHistories);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PointHistoriesExists(pointHistories.PointHistoryId))
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
            return View(pointHistories);
        }

        // GET: PointHistories/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pointHistories = await _context.PointHistories
                .FirstOrDefaultAsync(m => m.PointHistoryId == id);
            if (pointHistories == null)
            {
                return NotFound();
            }

            return View(pointHistories);
        }

        // POST: PointHistories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var pointHistories = await _context.PointHistories.FindAsync(id);
            if (pointHistories != null)
            {
                _context.PointHistories.Remove(pointHistories);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PointHistoriesExists(string id)
        {
            return _context.PointHistories.Any(e => e.PointHistoryId == id);
        }
    }
}
