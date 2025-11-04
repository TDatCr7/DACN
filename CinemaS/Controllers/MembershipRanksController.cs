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
    public class MembershipRanksController : Controller
    {
        private readonly CinemaContext _context;

        public MembershipRanksController(CinemaContext context)
        {
            _context = context;
        }

        // GET: MembershipRanks
        public async Task<IActionResult> Index()
        {
            return View(await _context.MembershipRanks.ToListAsync());
        }

        // GET: MembershipRanks/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var membershipRank = await _context.MembershipRanks
                .FirstOrDefaultAsync(m => m.MembershipRankId == id);
            if (membershipRank == null)
            {
                return NotFound();
            }

            return View(membershipRank);
        }

        // GET: MembershipRanks/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MembershipRanks/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MembershipRankId,Name,RequirePoint,PointReturnTicket,PointReturnCombo,PriorityLevel,CreatedAt,UpdatedAt")] MembershipRank membershipRank)
        {
            if (ModelState.IsValid)
            {
                _context.Add(membershipRank);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(membershipRank);
        }

        // GET: MembershipRanks/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var membershipRank = await _context.MembershipRanks.FindAsync(id);
            if (membershipRank == null)
            {
                return NotFound();
            }
            return View(membershipRank);
        }

        // POST: MembershipRanks/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MembershipRankId,Name,RequirePoint,PointReturnTicket,PointReturnCombo,PriorityLevel,CreatedAt,UpdatedAt")] MembershipRank membershipRank)
        {
            if (id != membershipRank.MembershipRankId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(membershipRank);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MembershipRankExists(membershipRank.MembershipRankId))
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
            return View(membershipRank);
        }

        // GET: MembershipRanks/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var membershipRank = await _context.MembershipRanks
                .FirstOrDefaultAsync(m => m.MembershipRankId == id);
            if (membershipRank == null)
            {
                return NotFound();
            }

            return View(membershipRank);
        }

        // POST: MembershipRanks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var membershipRank = await _context.MembershipRanks.FindAsync(id);
            if (membershipRank != null)
            {
                _context.MembershipRanks.Remove(membershipRank);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MembershipRankExists(string id)
        {
            return _context.MembershipRanks.Any(e => e.MembershipRankId == id);
        }
    }
}
