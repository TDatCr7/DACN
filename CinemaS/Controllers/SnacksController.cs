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
    public class SnacksController : Controller
    {
        private readonly CinemaContext _context;

        public SnacksController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Snacks
        public async Task<IActionResult> Index()
        {
            return View(await _context.Snacks.ToListAsync());
        }

        // GET: Snacks/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var snacks = await _context.Snacks
                .FirstOrDefaultAsync(m => m.SnackId == id);
            if (snacks == null)
            {
                return NotFound();
            }

            return View(snacks);
        }

        // GET: Snacks/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Snacks/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SnackId,SnackTypeId,Name,Price,Image,Description,IsActive")] Snacks snacks)
        {
            if (ModelState.IsValid)
            {
                _context.Add(snacks);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(snacks);
        }

        // GET: Snacks/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var snacks = await _context.Snacks.FindAsync(id);
            if (snacks == null)
            {
                return NotFound();
            }
            return View(snacks);
        }

        // POST: Snacks/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("SnackId,SnackTypeId,Name,Price,Image,Description,IsActive")] Snacks snacks)
        {
            if (id != snacks.SnackId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(snacks);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SnacksExists(snacks.SnackId))
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
            return View(snacks);
        }

        // GET: Snacks/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var snacks = await _context.Snacks
                .FirstOrDefaultAsync(m => m.SnackId == id);
            if (snacks == null)
            {
                return NotFound();
            }

            return View(snacks);
        }

        // POST: Snacks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var snacks = await _context.Snacks.FindAsync(id);
            if (snacks != null)
            {
                _context.Snacks.Remove(snacks);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SnacksExists(string id)
        {
            return _context.Snacks.Any(e => e.SnackId == id);
        }
    }
}
