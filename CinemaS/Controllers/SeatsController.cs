using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;
using CinemaS.Models.ViewModels;

namespace CinemaS.Controllers
{
    public class SeatsController : Controller
    {
        private readonly CinemaContext _context;

        public SeatsController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Seats
        public async Task<IActionResult> Index(string cinemaTheaterId)
        {
            // Lấy danh sách phòng chiếu để hiển thị dropdown
            ViewBag.CinemaTheaters = await _context.CinemaTheaters
                .Where(ct => ct.Status == 1)
                .OrderBy(ct => ct.Name)
                .ToListAsync();

            if (string.IsNullOrEmpty(cinemaTheaterId))
            {
                // Nếu chưa chọn phòng, lấy phòng đầu tiên
                var firstTheater = await _context.CinemaTheaters
                    .Where(ct => ct.Status == 1)
                    .OrderBy(ct => ct.Name)
                    .FirstOrDefaultAsync();

                if (firstTheater != null)
                {
                    cinemaTheaterId = firstTheater.CinemaTheaterId;
                }
            }

            ViewBag.SelectedTheaterId = cinemaTheaterId;

            if (!string.IsNullOrEmpty(cinemaTheaterId))
            {
                // Lấy ghế và thông tin loại ghế
                var seats = await _context.Seats
                    .Where(s => s.CinemaTheaterId == cinemaTheaterId)
                    .OrderBy(s => s.RowIndex)
                    .ThenBy(s => s.ColumnIndex)
                    .ToListAsync();

                var theater = await _context.CinemaTheaters
                    .FirstOrDefaultAsync(ct => ct.CinemaTheaterId == cinemaTheaterId);

                ViewBag.TheaterName = theater?.Name;
                ViewBag.NumOfColumns = theater?.NumOfColumns ?? 14;

                // Lấy thông tin loại ghế
                var seatTypes = await _context.SeatTypes.ToListAsync();
                ViewBag.SeatTypes = seatTypes;

                // Lấy danh sách suất chiếu với thông tin phim
                var showTimes = await _context.ShowTimes
                    .Where(st => st.CinemaTheaterId == cinemaTheaterId
            && st.ShowDate >= DateTime.Today)
                    .OrderBy(st => st.ShowDate)
                    .ThenBy(st => st.StartTime)
                    .ToListAsync();

                var showTimeVMs = new List<ShowTimeVM>();
                foreach (var st in showTimes)
                {
                    var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);
                    showTimeVMs.Add(new ShowTimeVM
                    {
                        ShowTimeId = st.ShowTimeId,
                        MoviesId = st.MoviesId,
                        MovieTitle = movie?.Title,
                        CinemaTheaterId = st.CinemaTheaterId,
                        ShowDate = st.ShowDate,
                        StartTime = st.StartTime,
                        EndTime = st.EndTime,
                        OriginPrice = st.OriginPrice
                    });
                }

                ViewBag.ShowTimes = showTimeVMs;

                return View(seats);
            }

            return View(new List<Seats>());
        }

        // POST: Seats/ToggleActive - API để bật/tắt trạng thái ghế
        [HttpPost]
        public async Task<IActionResult> ToggleActive([FromBody] string seatId)
        {
            try
            {
                var seat = await _context.Seats.FindAsync(seatId);
                if (seat == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ghế" });
                }

                // Kiểm tra xem ghế có đang được đặt không
                var isBooked = await _context.Tickets
                    .AnyAsync(t => t.SeatId == seatId && t.Status == 1);

                if (isBooked)
                {
                    return Json(new { success = false, message = "Ghế đã được đặt, không thể báo hỏng" });
                }

                // Đổi trạng thái
                seat.IsActive = !seat.IsActive;
                _context.Update(seat);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    isActive = seat.IsActive,
                    message = seat.IsActive ? "Đã phục hồi ghế" : "Đã báo hỏng ghế"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // GET: Seats/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seats = await _context.Seats
                .FirstOrDefaultAsync(m => m.SeatId == id);
            if (seats == null)
            {
                return NotFound();
            }

            return View(seats);
        }

        // GET: Seats/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Seats/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SeatId,SeatTypeId,CinemaTheaterId,RowIndex,ColumnIndex,Label")] Seats seats)
        {
            if (ModelState.IsValid)
            {
                _context.Add(seats);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(seats);
        }

        // GET: Seats/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seats = await _context.Seats.FindAsync(id);
            if (seats == null)
            {
                return NotFound();
            }
            return View(seats);
        }

        // POST: Seats/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("SeatId,SeatTypeId,CinemaTheaterId,RowIndex,ColumnIndex,Label")] Seats seats)
        {
            if (id != seats.SeatId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(seats);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SeatsExists(seats.SeatId))
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
            return View(seats);
        }

        // GET: Seats/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seats = await _context.Seats
                .FirstOrDefaultAsync(m => m.SeatId == id);
            if (seats == null)
            {
                return NotFound();
            }

            return View(seats);
        }

        // POST: Seats/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var seats = await _context.Seats.FindAsync(id);
            if (seats != null)
            {
                _context.Seats.Remove(seats);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SeatsExists(string id)
        {
            return _context.Seats.Any(e => e.SeatId == id);
        }
    }
}
