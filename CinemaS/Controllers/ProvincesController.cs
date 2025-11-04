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
    public class ProvincesController : Controller
    {
        private readonly CinemaContext _context;

        public ProvincesController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Provinces
        public async Task<IActionResult> Index()
        {
            return View(await _context.Provinces.ToListAsync());
        }

        // GET: Provinces/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var provinces = await _context.Provinces
                .FirstOrDefaultAsync(m => m.ProvinceId == id);
            if (provinces == null)
            {
                return NotFound();
            }

            return View(provinces);
        }

        // GET: Provinces/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Provinces/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProvinceId,Name,CreatedAt,UpdatedAt")] Provinces provinces)
        {
            if (ModelState.IsValid)
            {
                _context.Add(provinces);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(provinces);
        }

        // GET: Provinces/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var provinces = await _context.Provinces.FindAsync(id);
            if (provinces == null)
            {
                return NotFound();
            }
            return View(provinces);
        }

        // POST: Provinces/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("ProvinceId,Name,CreatedAt,UpdatedAt")] Provinces provinces)
        {
            if (id != provinces.ProvinceId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(provinces);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProvincesExists(provinces.ProvinceId))
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
            return View(provinces);
        }

        // GET: Provinces/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var provinces = await _context.Provinces
                .FirstOrDefaultAsync(m => m.ProvinceId == id);
            if (provinces == null)
            {
                return NotFound();
            }

            return View(provinces);
        }

        // POST: Provinces/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var provinces = await _context.Provinces.FindAsync(id);
            if (provinces != null)
            {
                _context.Provinces.Remove(provinces);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProvincesExists(string id)
        {
            return _context.Provinces.Any(e => e.ProvinceId == id);
        }
    }
}
