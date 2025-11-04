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
    public class CinemaTypesController : Controller
    {
        private readonly CinemaContext _context;

        public CinemaTypesController(CinemaContext context)
        {
            _context = context;
        }

        // GET: CinemaTypes
        public async Task<IActionResult> Index()
        {
            return View(await _context.CinemaTypes.ToListAsync());
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
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CinemaTypeId,Name,Description,Code")] CinemaTypes cinemaTypes)
        {
            if (ModelState.IsValid)
            {
                _context.Add(cinemaTypes);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(cinemaTypes);
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
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("CinemaTypeId,Name,Description,Code")] CinemaTypes cinemaTypes)
        {
            if (id != cinemaTypes.CinemaTypeId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cinemaTypes);
                    await _context.SaveChangesAsync();
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
                return RedirectToAction(nameof(Index));
            }
            return View(cinemaTypes);
        }

        // GET: CinemaTypes/Delete/5
        public async Task<IActionResult> Delete(string id)
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

        // POST: CinemaTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var cinemaTypes = await _context.CinemaTypes.FindAsync(id);
            if (cinemaTypes != null)
            {
                _context.CinemaTypes.Remove(cinemaTypes);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CinemaTypesExists(string id)
        {
            return _context.CinemaTypes.Any(e => e.CinemaTypeId == id);
        }
    }
}
