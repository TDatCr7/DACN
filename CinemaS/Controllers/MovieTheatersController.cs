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
    public class MovieTheatersController : Controller
    {
        private readonly CinemaContext _context;

        public MovieTheatersController(CinemaContext context)
        {
            _context = context;
        }

        // GET: MovieTheaters
        public async Task<IActionResult> Index()
        {
            return View(await _context.MovieTheaters.ToListAsync());
        }

        // GET: MovieTheaters/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movieTheaters = await _context.MovieTheaters
                .FirstOrDefaultAsync(m => m.MovieTheaterId == id);
            if (movieTheaters == null)
            {
                return NotFound();
            }

            return View(movieTheaters);
        }

        // GET: MovieTheaters/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MovieTheaters/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MovieTheaterId,Name,Address,Hotline,Status,IFrameCode,ProvinceId")] MovieTheaters movieTheaters)
        {
            if (ModelState.IsValid)
            {
                _context.Add(movieTheaters);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(movieTheaters);
        }

        // GET: MovieTheaters/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movieTheaters = await _context.MovieTheaters.FindAsync(id);
            if (movieTheaters == null)
            {
                return NotFound();
            }
            return View(movieTheaters);
        }

        // POST: MovieTheaters/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MovieTheaterId,Name,Address,Hotline,Status,IFrameCode,ProvinceId")] MovieTheaters movieTheaters)
        {
            if (id != movieTheaters.MovieTheaterId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(movieTheaters);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieTheatersExists(movieTheaters.MovieTheaterId))
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
            return View(movieTheaters);
        }

        // GET: MovieTheaters/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movieTheaters = await _context.MovieTheaters
                .FirstOrDefaultAsync(m => m.MovieTheaterId == id);
            if (movieTheaters == null)
            {
                return NotFound();
            }

            return View(movieTheaters);
        }

        // POST: MovieTheaters/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var movieTheaters = await _context.MovieTheaters.FindAsync(id);
            if (movieTheaters != null)
            {
                _context.MovieTheaters.Remove(movieTheaters);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MovieTheatersExists(string id)
        {
            return _context.MovieTheaters.Any(e => e.MovieTheaterId == id);
        }
    }
}
