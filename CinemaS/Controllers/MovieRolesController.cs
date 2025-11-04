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
    public class MovieRolesController : Controller
    {
        private readonly CinemaContext _context;

        public MovieRolesController(CinemaContext context)
        {
            _context = context;
        }

        // GET: MovieRoles
        public async Task<IActionResult> Index()
        {
            return View(await _context.MovieRoles.ToListAsync());
        }

        // GET: MovieRoles/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movieRole = await _context.MovieRoles
                .FirstOrDefaultAsync(m => m.MovieRoleId == id);
            if (movieRole == null)
            {
                return NotFound();
            }

            return View(movieRole);
        }

        // GET: MovieRoles/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MovieRoles/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MovieRoleId,Name,Description")] MovieRole movieRole)
        {
            if (ModelState.IsValid)
            {
                _context.Add(movieRole);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(movieRole);
        }

        // GET: MovieRoles/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movieRole = await _context.MovieRoles.FindAsync(id);
            if (movieRole == null)
            {
                return NotFound();
            }
            return View(movieRole);
        }

        // POST: MovieRoles/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MovieRoleId,Name,Description")] MovieRole movieRole)
        {
            if (id != movieRole.MovieRoleId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(movieRole);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieRoleExists(movieRole.MovieRoleId))
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
            return View(movieRole);
        }

        // GET: MovieRoles/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var movieRole = await _context.MovieRoles
                .FirstOrDefaultAsync(m => m.MovieRoleId == id);
            if (movieRole == null)
            {
                return NotFound();
            }

            return View(movieRole);
        }

        // POST: MovieRoles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var movieRole = await _context.MovieRoles.FindAsync(id);
            if (movieRole != null)
            {
                _context.MovieRoles.Remove(movieRole);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MovieRoleExists(string id)
        {
            return _context.MovieRoles.Any(e => e.MovieRoleId == id);
        }
    }
}
