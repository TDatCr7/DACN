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
        public async Task<IActionResult> Index()
        {
            return View(await _context.SnackTypes.ToListAsync());
        }

        // GET: SnackTypes/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var snackTypes = await _context.SnackTypes
                .FirstOrDefaultAsync(m => m.SnackTypeId == id);
            if (snackTypes == null)
            {
                return NotFound();
            }

            return View(snackTypes);
        }

        // GET: SnackTypes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: SnackTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SnackTypeId,Name,Description")] SnackTypes snackTypes)
        {
            if (ModelState.IsValid)
            {
                _context.Add(snackTypes);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(snackTypes);
        }

        // GET: SnackTypes/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var snackTypes = await _context.SnackTypes.FindAsync(id);
            if (snackTypes == null)
            {
                return NotFound();
            }
            return View(snackTypes);
        }

        // POST: SnackTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("SnackTypeId,Name,Description")] SnackTypes snackTypes)
        {
            if (id != snackTypes.SnackTypeId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(snackTypes);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SnackTypesExists(snackTypes.SnackTypeId))
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
            return View(snackTypes);
        }

        // GET: SnackTypes/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var snackTypes = await _context.SnackTypes
                .FirstOrDefaultAsync(m => m.SnackTypeId == id);
            if (snackTypes == null)
            {
                return NotFound();
            }

            return View(snackTypes);
        }

        // POST: SnackTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var snackTypes = await _context.SnackTypes.FindAsync(id);
            if (snackTypes != null)
            {
                _context.SnackTypes.Remove(snackTypes);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SnackTypesExists(string id)
        {
            return _context.SnackTypes.Any(e => e.SnackTypeId == id);
        }
    }
}
