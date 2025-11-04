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
    public class GenresController : Controller
    {
        private readonly CinemaContext _context;

        public GenresController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Genres
        public async Task<IActionResult> Index()
        {
            return View(await _context.Genres.ToListAsync());
        }

        // GET: Genres/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var genres = await _context.Genres
                .FirstOrDefaultAsync(m => m.GenresId == id);
            if (genres == null)
            {
                return NotFound();
            }

            return View(genres);
        }

        // GET: Genres/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Genres/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
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
            return View(genres);
        }

        // GET: Genres/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var genres = await _context.Genres.FindAsync(id);
            if (genres == null)
            {
                return NotFound();
            }
            return View(genres);
        }

        // POST: Genres/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("GenresId,Name,Description")] Genres genres)
        {
            if (id != genres.GenresId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(genres);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GenresExists(genres.GenresId))
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
            return View(genres);
        }

        // GET: Genres/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var genres = await _context.Genres
                .FirstOrDefaultAsync(m => m.GenresId == id);
            if (genres == null)
            {
                return NotFound();
            }

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
