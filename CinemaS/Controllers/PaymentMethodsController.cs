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
    public class PaymentMethodsController : Controller
    {
        private readonly CinemaContext _context;

        public PaymentMethodsController(CinemaContext context)
        {
            _context = context;
        }

        // GET: PaymentMethods
        public async Task<IActionResult> Index()
        {
            return View(await _context.PaymentMethods.ToListAsync());
        }

        // GET: PaymentMethods/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentMethods = await _context.PaymentMethods
                .FirstOrDefaultAsync(m => m.PaymentMethodId == id);
            if (paymentMethods == null)
            {
                return NotFound();
            }

            return View(paymentMethods);
        }

        // GET: PaymentMethods/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: PaymentMethods/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PaymentMethodId,Code,Name,Type,Provider,IsActive,CreatedAt,UpdatedAt")] PaymentMethods paymentMethods)
        {
            if (ModelState.IsValid)
            {
                _context.Add(paymentMethods);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(paymentMethods);
        }

        // GET: PaymentMethods/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentMethods = await _context.PaymentMethods.FindAsync(id);
            if (paymentMethods == null)
            {
                return NotFound();
            }
            return View(paymentMethods);
        }

        // POST: PaymentMethods/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PaymentMethodId,Code,Name,Type,Provider,IsActive,CreatedAt,UpdatedAt")] PaymentMethods paymentMethods)
        {
            if (id != paymentMethods.PaymentMethodId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(paymentMethods);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentMethodsExists(paymentMethods.PaymentMethodId))
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
            return View(paymentMethods);
        }

        // GET: PaymentMethods/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentMethods = await _context.PaymentMethods
                .FirstOrDefaultAsync(m => m.PaymentMethodId == id);
            if (paymentMethods == null)
            {
                return NotFound();
            }

            return View(paymentMethods);
        }

        // POST: PaymentMethods/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var paymentMethods = await _context.PaymentMethods.FindAsync(id);
            if (paymentMethods != null)
            {
                _context.PaymentMethods.Remove(paymentMethods);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PaymentMethodsExists(string id)
        {
            return _context.PaymentMethods.Any(e => e.PaymentMethodId == id);
        }
    }
}
