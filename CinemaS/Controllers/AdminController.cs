using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using CinemaS.Models;
using CinemaS.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly CinemaContext _db;
        private readonly UserManager<AppUser> _userManager;

        public AdminController(CinemaContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            if (!User.Identity.IsAuthenticated || !User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // ============== ADMIN BOOKING - TRANG ĐẶT VÉ HỘ KHÁCH HÀNG ==============

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminBooking(string showTimeId)
        {
            if (string.IsNullOrWhiteSpace(showTimeId)) 
                return BadRequest("Vui lòng chọn suất chiếu!");

            var showTime = await _db.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShowTimeId == showTimeId);
            
            if (showTime == null) 
                return NotFound("Suất chiếu không tồn tại!");

            var movie = await _db.Movies.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == showTime.MoviesId);
            
            var theater = await _db.CinemaTheaters.AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.CinemaTheaterId == showTime.CinemaTheaterId);
            
            if (movie == null || theater == null) 
                return NotFound();

            // Chỉ khoá ghế đã thanh toán (Status = 2)
            var paidSeatIds = await _db.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == showTimeId && t.Status == 2)
                .Select(t => t.SeatId)
                .ToListAsync();

            // Filter ra ghế IsDeleted = true - chỉ load ghế chưa bị xóa
            var seats = await _db.Seats.AsNoTracking()
                .Where(s => s.CinemaTheaterId == theater.CinemaTheaterId && !s.IsDeleted)
                .OrderBy(s => s.RowIndex)
                .ThenBy(s => s.ColumnIndex)
                .ToListAsync();
            
            var seatTypes = await _db.SeatTypes.AsNoTracking().ToListAsync();

            var seatVMs = seats.Select(s =>
            {
                var st = seatTypes.FirstOrDefault(x => x.SeatTypeId == s.SeatTypeId);
                return new SeatVM
                {
                    SeatId = s.SeatId,
                    SeatTypeId = s.SeatTypeId,
                    SeatTypeName = st?.Name,
                    SeatTypePrice = st?.Price,
                    RowIndex = s.RowIndex,
                    ColumnIndex = s.ColumnIndex,
                    Label = s.Label,
                    Status = paidSeatIds.Contains(s.SeatId) ? "Booked" : "Available",
                    IsCouple = string.Equals(st?.Name, "COUPLE", StringComparison.OrdinalIgnoreCase),
                    IsVIP = string.Equals(st?.Name, "VIP", StringComparison.OrdinalIgnoreCase),
                    IsActive = s.IsActive,
                    PairId = s.PairId
                };
            }).ToList();

            ViewBag.Snacks = await _db.Snacks.AsNoTracking()
                .Where(s => s.IsActive == true)
                .ToListAsync();

            var vm = new AdminBookingVM
            {
                ShowTimeId = showTime.ShowTimeId,
                MoviesId = movie.MoviesId,
                MovieTitle = movie.Title,
                MoviePoster = movie.PosterImage,
                CinemaTheaterName = theater.Name,
                ShowDate = showTime.ShowDate,
                StartTime = showTime.StartTime,
                EndTime = showTime.EndTime,
                Seats = seatVMs,
                NumOfRows = theater.NumOfRows ?? 6,
                NumOfColumns = theater.NumOfColumns ?? 14
            };

            return View("~/Views/Admin/AdminBooking.cshtml", vm);
        }
    }
}
