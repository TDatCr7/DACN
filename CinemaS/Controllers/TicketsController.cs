using CinemaS.Models;
using CinemaS.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CinemaS.Controllers
{
    public class TicketsController : Controller
    {
        private readonly CinemaContext _context;

        public TicketsController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Tickets/Management
        public IActionResult Management()
        {
            return View();
        }

        // GET: Tickets
        public async Task<IActionResult> Index(string searchString, DateTime? fromDate, DateTime? toDate, byte? statusFilter, int page = 1)
        {
            const int PageSize = 8;
            if (page < 1) page = 1;

            ViewData["CurrentFilter"] = searchString;
            ViewData["FromDate"] = fromDate?.ToString("yyyy-MM-dd");
            ViewData["ToDate"] = toDate?.ToString("yyyy-MM-dd");
            ViewData["StatusFilter"] = statusFilter;

            var ticketsQuery = from ticket in _context.Tickets
                               join invoice in _context.Invoices on ticket.InvoiceId equals invoice.InvoiceId into invGroup
                               from invoice in invGroup.DefaultIfEmpty()

                               join showTime in _context.ShowTimes on ticket.ShowTimeId equals showTime.ShowTimeId into stGroup
                               from showTime in stGroup.DefaultIfEmpty()

                               join movie in _context.Movies on showTime.MoviesId equals movie.MoviesId into mGroup
                               from movie in mGroup.DefaultIfEmpty()

                               join seat in _context.Seats on ticket.SeatId equals seat.SeatId into seatGroup
                               from seat in seatGroup.DefaultIfEmpty()

                               join cinemaTheater in _context.CinemaTheaters on showTime.CinemaTheaterId equals cinemaTheater.CinemaTheaterId into ctGroup
                               from cinemaTheater in ctGroup.DefaultIfEmpty()

                               join user in _context.Users on invoice.CustomerId equals user.UserId into userGroup
                               from user in userGroup.DefaultIfEmpty()

                               join ticketType in _context.TicketTypes on ticket.TicketTypeId equals ticketType.TicketTypeId into ttGroup
                               from ticketType in ttGroup.DefaultIfEmpty()

                               join seatType in _context.SeatTypes on seat.SeatTypeId equals seatType.SeatTypeId into stypeGroup
                               from seatType in stypeGroup.DefaultIfEmpty()

                               select new
                               {
                                   Ticket = ticket,
                                   Invoice = invoice,
                                   MovieTitle = movie != null ? movie.Title : "",
                                   MoviePoster = movie != null ? movie.PosterImage : null,
                                   ShowDate = showTime != null ? showTime.ShowDate : (DateTime?)null,
                                   StartTime = showTime != null ? showTime.StartTime : (DateTime?)null,
                                   CinemaTheaterName = cinemaTheater != null ? cinemaTheater.Name : "",
                                   SeatLabel = seat != null ? (seat.RowIndex + seat.ColumnIndex.ToString()) : "",
                                   SeatTypeName = seatType != null ? seatType.Name : null,
                                   CustomerName = user != null ? user.FullName : null,
                                   TicketTypeName = ticketType != null ? ticketType.Name : null
                               };

            // ===== FILTER: SEARCH =====
            if (!string.IsNullOrEmpty(searchString))
            {
                ticketsQuery = ticketsQuery.Where(t =>
                    (t.MovieTitle ?? "").Contains(searchString) ||
                    t.Ticket.TicketId.Contains(searchString) ||
                    (t.Invoice != null && t.Invoice.InvoiceId.Contains(searchString)) ||
                    (t.CustomerName != null && t.CustomerName.Contains(searchString)) ||
                    (t.Invoice != null && t.Invoice.Email != null && t.Invoice.Email.Contains(searchString))
                );
            }

            // ===== FILTER: DATE =====
            if (fromDate.HasValue)
                ticketsQuery = ticketsQuery.Where(t => t.Ticket.CreatedBooking >= fromDate.Value);

            if (toDate.HasValue)
            {
                var endOfDay = toDate.Value.AddDays(1).AddSeconds(-1);
                ticketsQuery = ticketsQuery.Where(t => t.Ticket.CreatedBooking <= endOfDay);
            }

            // ===== FILTER: STATUS =====
            if (statusFilter.HasValue)
                ticketsQuery = ticketsQuery.Where(t => t.Ticket.Status == statusFilter.Value);

            // ===== COUNT for paging =====
            var totalItems = await ticketsQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            // ===== PAGE DATA =====
            var results = await ticketsQuery
                .OrderByDescending(t => t.Ticket.CreatedBooking)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // paging info for view
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = PageSize;
            ViewBag.TotalItems = totalItems;

            // (nếu view có dùng TicketData thì vẫn giữ)
            ViewBag.TicketData = results;

            return View(results.Select(r => r.Ticket).ToList());
        }


        // GET: Tickets/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tickets = await _context.Tickets
                .FirstOrDefaultAsync(m => m.TicketId == id);
            if (tickets == null)
            {
                return NotFound();
            }

            return View(tickets);
        }

        // GET: Tickets/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Tickets/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TicketId,InvoiceId,TicketTypeId,ShowTimeId,SeatId,Status,Price,CreatedBooking,Expire")] Tickets tickets)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tickets);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Tạo vé mới thành công!";
                return RedirectToAction(nameof(Index));
            }
            TempData["Error"] = "Không thể tạo vé. Vui lòng kiểm tra lại thông tin.";
            return View(tickets);
        }

        // GET: Tickets/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tickets = await _context.Tickets.FindAsync(id);
            if (tickets == null)
            {
                return NotFound();
            }
            return View(tickets);
        }

        // POST: Tickets/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("TicketId,InvoiceId,TicketTypeId,ShowTimeId,SeatId,Status,Price,CreatedBooking,Expire")] Tickets tickets)
        {
            if (id != tickets.TicketId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tickets);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "Cập nhật vé thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TicketsExists(tickets.TicketId))
                    {
                        TempData["Error"] = "Vé không tồn tại.";
                        return NotFound();
                    }
                    else
                    {
                        TempData["Error"] = "Có lỗi xảy ra khi cập nhật vé.";
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            TempData["Error"] = "Không thể cập nhật vé. Vui lòng kiểm tra lại thông tin.";
            return View(tickets);
        }

        // GET: Tickets/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tickets = await _context.Tickets
                .FirstOrDefaultAsync(m => m.TicketId == id);
            if (tickets == null)
            {
                return NotFound();
            }

            return View(tickets);
        }

        // POST: Tickets/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
            {
                TempData["Error"] = "Không tìm thấy vé cần xóa.";
                return RedirectToAction(nameof(Index));
            }

            var st = await _context.ShowTimes.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.ShowTimeId == ticket.ShowTimeId);

            if (st != null)
            {
                var end = (st.EndTime ?? st.StartTime)?.ToLocalTime() ?? DateTime.MinValue;
                var date = st.ShowDate?.Date ?? DateTime.MinValue.Date;
                var showEnd = date.Add(end.TimeOfDay);

                if (DateTime.Now <= showEnd)
                {
                    TempData["Error"] = "Chỉ xóa vé khi suất chiếu đã kết thúc.";
                    return RedirectToAction(nameof(Index));
                }
            }

            _context.Tickets.Remove(ticket);
            await _context.SaveChangesAsync();
            TempData["Message"] = "Xóa vé thành công!";
            return RedirectToAction(nameof(Index));
        }


        private bool TicketsExists(string id)
        {
            return _context.Tickets.Any(e => e.TicketId == id);
        }

        [Authorize]
        public async Task<IActionResult> MyTickets()
        {
            var email = User.Identity?.Name;
            var user = await _context.Users.AsNoTracking()
                            .FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return View(new List<Tickets>());

            var query = from t in _context.Tickets
                        join i in _context.Invoices on t.InvoiceId equals i.InvoiceId
                        join st in _context.ShowTimes on t.ShowTimeId equals st.ShowTimeId
                        where i.CustomerId == user.UserId
                        orderby st.ShowDate descending, st.StartTime descending
                        select new { Ticket = t, ShowTime = st };

            var data = await query.ToListAsync();
            ViewBag.ShowTimes = data.ToDictionary(x => x.Ticket.TicketId, x => x.ShowTime);

            return View(data.Select(x => x.Ticket).ToList());
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteForUser(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var email = User.Identity?.Name;
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return Forbid();

            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.TicketId == id);
            if (ticket == null) return NotFound();

            var invoice = await _context.Invoices.AsNoTracking()
                                .FirstOrDefaultAsync(i => i.InvoiceId == ticket.InvoiceId);
            if (invoice == null || invoice.CustomerId != user.UserId) return Forbid();

            var st = await _context.ShowTimes.AsNoTracking()
                            .FirstOrDefaultAsync(s => s.ShowTimeId == ticket.ShowTimeId);
            if (st == null) return NotFound();

            var end = (st.EndTime ?? st.StartTime)?.ToLocalTime() ?? DateTime.MinValue;
            var date = st.ShowDate?.Date ?? DateTime.MinValue.Date;
            var showEnd = date.Add(end.TimeOfDay);

            if (DateTime.Now <= showEnd)
            {
                TempData["Error"] = "Chỉ được xóa vé sau khi suất chiếu đã kết thúc.";
                return RedirectToAction(nameof(MyTickets));
            }

            _context.Tickets.Remove(ticket);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Đã xóa vé khỏi lịch sử.";
            return RedirectToAction(nameof(MyTickets));
        }
        
    }
}
