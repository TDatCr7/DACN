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
    public class ParticipantsController : Controller
    {
        private readonly CinemaContext _context;

        public ParticipantsController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Participants
        public async Task<IActionResult> Index()
        {
            return View(await _context.Participants.ToListAsync());
        }

        // GET: Participants/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var participants = await _context.Participants
                .FirstOrDefaultAsync(m => m.ParticipantsId == id);
            if (participants == null)
            {
                return NotFound();
            }

            return View(participants);
        }

        // GET: Participants/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Participants/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ParticipantsId,BirthName,NickName,Gender,Nationality,MiniBio,Avatar")] Participants participants)
        {
            if (ModelState.IsValid)
            {
                _context.Add(participants);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(participants);
        }

        // GET: Participants/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var participants = await _context.Participants.FindAsync(id);
            if (participants == null)
            {
                return NotFound();
            }
            return View(participants);
        }

        // POST: Participants/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("ParticipantsId,BirthName,NickName,Gender,Nationality,MiniBio,Avatar")] Participants participants)
        {
            if (id != participants.ParticipantsId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(participants);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ParticipantsExists(participants.ParticipantsId))
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
            return View(participants);
        }

        // GET: Participants/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var participants = await _context.Participants
                .FirstOrDefaultAsync(m => m.ParticipantsId == id);
            if (participants == null)
            {
                return NotFound();
            }

            return View(participants);
        }

        // POST: Participants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var participants = await _context.Participants.FindAsync(id);
            if (participants != null)
            {
                _context.Participants.Remove(participants);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ParticipantsExists(string id)
        {
            return _context.Participants.Any(e => e.ParticipantsId == id);
        }
    }
}
