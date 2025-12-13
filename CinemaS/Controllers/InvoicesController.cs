using CinemaS.Models;
using CinemaS.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class InvoicesController : Controller
    {
        private readonly CinemaContext _context;

        public InvoicesController(CinemaContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> TicketPopup(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return Content("Không có mã vé.", "text/html");

            var invoice = await _context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null)
                return Content("Hóa đơn không tồn tại.", "text/html");

            var tickets = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.InvoiceId == id)
                .ToListAsync();

            if (!tickets.Any())
                return Content("Hóa đơn này chưa có vé.", "text/html");

            var firstTicket = tickets.First();

            var st = await _context.ShowTimes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowTimeId == firstTicket.ShowTimeId);

            if (st == null)
                return Content("Không tìm thấy suất chiếu.", "text/html");

            var mv = await _context.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);

            var room = await _context.CinemaTheaters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CinemaTheaterId == st.CinemaTheaterId);

            MovieTheaters theater = null;
            if (room != null && !string.IsNullOrEmpty(room.MovieTheaterId))
            {
                theater = await _context.MovieTheaters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(mt => mt.MovieTheaterId == room.MovieTheaterId);
            }

            // Ghế
            var seatIds = tickets.Select(t => t.SeatId).ToList();

            var seatLabels = await _context.Seats
                .AsNoTracking()
                .Where(s => seatIds.Contains(s.SeatId))
                .Select(s => s.Label ?? s.SeatId)
                .OrderBy(x => x)
                .ToListAsync();

            string seatText = string.Join(" ", seatLabels);

            // Snack
            var snackLines = await _context.DetailBookingSnacks
                .AsNoTracking()
                .Where(d => d.InvoiceId == id)
                .ToListAsync();

            string snackLinesHtml;
            if (snackLines.Any())
            {
                var snackIds = snackLines.Select(x => x.SnackId).ToList();
                var snackMap = await _context.Snacks
                    .AsNoTracking()
                    .Where(s => snackIds.Contains(s.SnackId))
                    .ToDictionaryAsync(s => s.SnackId, s => s.Name);

                snackLinesHtml = "";
                foreach (var line in snackLines)
                {
                    snackMap.TryGetValue(line.SnackId, out var name);
                    int qty = line.TotalSnack ?? 0;
                    snackLinesHtml += $"{(string.IsNullOrEmpty(name) ? "Snack" : name)} × {qty}<br/>";
                }
            }
            else
            {
                snackLinesHtml = "Không có";
            }

            string movieTitle = mv?.Title ?? "N/A";
            string timeText = st.StartTime?.ToString("HH:mm") ?? "";
            string dateText = st.ShowDate?.ToString("dd/MM/yyyy") ?? "";
            string roomLabel = room?.Name ?? st.CinemaTheaterId ?? "N/A";
            string cinemaName = theater?.Name ?? "N/A";
            string totalText = (invoice.TotalPrice ?? 0m).ToString("#,0");

            string html = $@"
<div style='border-radius:16px; padding:24px 28px;
            color:#f9fafb; font-size:15px;
            background:#020617;
            border:1px solid rgba(148,163,255,.45);
            box-shadow:0 22px 55px rgba(15,23,42,.95);'>

    <div style='font-size:14px; font-weight:800; text-transform:uppercase; color:#fde047;'>TÊN PHIM</div>
    <div style='font-size:22px; font-weight:900; margin-bottom:20px;'>{movieTitle}</div>

    <div style='display:grid; grid-template-columns:1fr 1fr; gap:22px;'>

        <div>
            <div style='margin-bottom:14px;'>
                <div style='font-weight:700; color:#fde047;'>Mã đặt vé</div>
                <div>{invoice.InvoiceId}</div>
            </div>

            <div style='margin-bottom:14px;'>
                <div style='font-weight:700; color:#fde047;'>Phòng chiếu</div>
                <div>{roomLabel}</div>
            </div>

            <div>
                <div style='font-weight:700; color:#fde047;'>Số ghế</div>
                <div>{seatText}</div>
            </div>
        </div>

        <div>
            <div style='margin-bottom:14px;'>
                <div style='font-weight:700; color:#fde047;'>Thời gian</div>
                <div>{timeText} {dateText}</div>
            </div>

            <div style='margin-bottom:14px;'>
                <div style='font-weight:700; color:#fde047;'>Số vé</div>
                <div>{tickets.Count}</div>
            </div>

            <div>
                <div style='font-weight:700; color:#fde047;'>Bắp nước</div>
                <div>{snackLinesHtml}</div>
            </div>
        </div>
    </div>

    <div style='margin-top:22px;'>
        <div style='font-weight:700; color:#fde047;'>Rạp</div>
        <div style='font-size:16px; font-weight:700;'>{cinemaName}</div>
        <div style='font-size:13px; opacity:.9;'>{theater?.Address ?? ""}</div>
    </div>

    <hr style='margin:20px 0 14px; border:none; border-top:1px dashed rgba(255,255,255,.4);' />

    <div style='display:flex; justify-content:space-between; align-items:center; margin-bottom:14px;'>
        <span style='font-size:18px; font-weight:800; color:#fde047;'>TỔNG TIỀN</span>
        <span style='font-size:22px; font-weight:900;'>{totalText} VNĐ</span>
    </div>
</div>";

            return Content(html, "text/html");
        }

        // ========== INDEX ==========
        public async Task<IActionResult> Index(string? search, DateTime? fromDate, DateTime? toDate)
        {
            ViewBag.Search = search;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            var query =
                from inv in _context.Invoices
                join u in _context.Users on inv.CustomerId equals u.UserId into gj
                from u in gj.DefaultIfEmpty()
                select new
                {
                    inv.InvoiceId,
                    FullName = u.FullName ?? "Khách lẻ",
                    Email = inv.Email ?? u.Email,
                    Phone = inv.PhoneNumber ?? u.PhoneNumber,
                    inv.TotalPrice,
                    Status = inv.Status ?? 0,
                    inv.CreatedAt
                };

            // ===== FILTER: SEARCH =====
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(x =>
                    (x.FullName != null && x.FullName.ToLower().Contains(search)) ||
                    (x.Email != null && x.Email.ToLower().Contains(search)) ||
                    (x.Phone != null && x.Phone.Contains(search))
                );
            }

            // ===== FILTER: DATE =====
            if (fromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(x => x.CreatedAt <= toDate.Value.AddDays(1).AddSeconds(-1));

            // ===== RETURN DATA =====
            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new InvoiceIndexVM
                {
                    InvoiceId = x.InvoiceId,
                    CustomerName = x.FullName,
                    Email = x.Email,
                    PhoneNumber = x.Phone,
                    TotalPrice = x.TotalPrice ?? 0m,
                    Status = x.Status,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return View(data);
        }





        // ========== DETAILS ==========
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(m => m.InvoiceId == id);

            if (invoice == null) return NotFound();

            return View(invoice);
        }

        // ========== EDIT ==========
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();

            return View(invoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Invoices model)
        {
            if (id != model.InvoiceId) return NotFound();
            if (!ModelState.IsValid) return View(model);

            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();

            invoice.Email = model.Email;
            invoice.PhoneNumber = model.PhoneNumber;
            invoice.Status = model.Status;
            invoice.TotalTicket = model.TotalTicket;
            invoice.TotalPrice = model.TotalPrice;
            invoice.PaymentMethodId = model.PaymentMethodId;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = invoice.InvoiceId });
        }

        // ========== DELETE SINGLE ==========
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(m => m.InvoiceId == id);

            if (invoice == null) return NotFound();

            return View(invoice);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null)
            {
                _context.Invoices.Remove(invoice);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }


        // ========== DELETE NHIỀU (INDEX + 5 GIÂY UNDO) ==========
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelected(List<string> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "Không có hóa đơn nào được chọn để xóa.";
                return RedirectToAction(nameof(Index));
            }

            var invoices = await _context.Invoices
                .Where(i => selectedIds.Contains(i.InvoiceId) && i.Status != 1)
                .ToListAsync();

            if (!invoices.Any())
            {
                TempData["Error"] = "Các hóa đơn được chọn đều đã thanh toán, không thể xóa.";
                return RedirectToAction(nameof(Index));
            }

            _context.Invoices.RemoveRange(invoices);
            await _context.SaveChangesAsync();

            TempData["Message"] = $"Đã xóa {invoices.Count} hóa đơn chưa thanh toán.";
            return RedirectToAction(nameof(Index));
        }

        // ========== XÓA TẤT CẢ HÓA ĐƠN CHƯA THANH TOÁN ==========
        [HttpPost]
        public async Task<IActionResult> DeleteUnpaid()
        {
            var unpaid = await _context.Invoices
                .Where(i => i.Status != 1)
                .ToListAsync();

            if (!unpaid.Any())
            {
                return Json(new { success = false, message = "Không còn hóa đơn chưa thanh toán." });
            }

            _context.Invoices.RemoveRange(unpaid);
            await _context.SaveChangesAsync();

            return Json(new { success = true, count = unpaid.Count });
        }
    }
}
