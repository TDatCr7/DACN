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
    public class PaymentTransactionsController : Controller
    {
        private readonly CinemaContext _context;

        public PaymentTransactionsController(CinemaContext context)
        {
            _context = context;
        }

        // GET: PaymentTransactions
        public async Task<IActionResult> Index()
        {
            return View(await _context.PaymentTransactions.ToListAsync());
        }

        // GET: PaymentTransactions/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentTransactions = await _context.PaymentTransactions
                .FirstOrDefaultAsync(m => m.PaymentTransactionId == id);
            if (paymentTransactions == null)
            {
                return NotFound();
            }

            return View(paymentTransactions);
        }

        // GET: PaymentTransactions/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: PaymentTransactions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PaymentTransactionId,InvoiceId,PaymentMethodId,Amount,Currency,Status,ProviderTxnId,ProviderOrderNo,Description,FailureReason,CreatedAt,UpdatedAt,PaidAt,RefundedAt")] PaymentTransactions paymentTransactions)
        {
            if (ModelState.IsValid)
            {
                _context.Add(paymentTransactions);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(paymentTransactions);
        }

        // GET: PaymentTransactions/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentTransactions = await _context.PaymentTransactions.FindAsync(id);
            if (paymentTransactions == null)
            {
                return NotFound();
            }
            return View(paymentTransactions);
        }

        // POST: PaymentTransactions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PaymentTransactionId,InvoiceId,PaymentMethodId,Amount,Currency,Status,ProviderTxnId,ProviderOrderNo,Description,FailureReason,CreatedAt,UpdatedAt,PaidAt,RefundedAt")] PaymentTransactions paymentTransactions)
        {
            if (id != paymentTransactions.PaymentTransactionId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(paymentTransactions);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentTransactionsExists(paymentTransactions.PaymentTransactionId))
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
            return View(paymentTransactions);
        }

        // GET: PaymentTransactions/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var paymentTransactions = await _context.PaymentTransactions
                .FirstOrDefaultAsync(m => m.PaymentTransactionId == id);
            if (paymentTransactions == null)
            {
                return NotFound();
            }

            return View(paymentTransactions);
        }

        // POST: PaymentTransactions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var paymentTransactions = await _context.PaymentTransactions.FindAsync(id);
            if (paymentTransactions != null)
            {
                _context.PaymentTransactions.Remove(paymentTransactions);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PaymentTransactionsExists(string id)
        {
            return _context.PaymentTransactions.Any(e => e.PaymentTransactionId == id);
        }
    }
}
