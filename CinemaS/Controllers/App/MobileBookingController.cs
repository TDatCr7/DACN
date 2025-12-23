using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CinemaS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Controllers
{
    [ApiController]
    [Route("api")]
    public class MobileBookingController : ControllerBase
    {
        private readonly CinemaContext _context;

        public MobileBookingController(CinemaContext context)
        {
            _context = context;
        }

        // GET: /api/movies/{movieId}/showtimes
        [HttpGet("movies/{movieId}/showtimes")]
        public async Task<IActionResult> GetShowtimes(string movieId)
        {
            if (string.IsNullOrWhiteSpace(movieId)) return BadRequest();

            var now = DateTime.Now.Date;

            var data = await (from st in _context.ShowTimes.AsNoTracking()
                              join room in _context.CinemaTheaters.AsNoTracking()
                                  on st.CinemaTheaterId equals room.CinemaTheaterId
                              join theater in _context.MovieTheaters.AsNoTracking()
                                  on room.MovieTheaterId equals theater.MovieTheaterId into th
                              from theater in th.DefaultIfEmpty()
                              where st.MoviesId == movieId
                              orderby st.ShowDate, st.StartTime
                              select new
                              {
                                  showTimeId = st.ShowTimeId,
                                  showDate = st.ShowDate,
                                  startTime = st.StartTime,
                                  endTime = st.EndTime,
                                  cinemaName = theater != null ? theater.Name : "",
                                  screenName = room != null ? room.Name : ""
                              })
                              .ToListAsync();

            return Ok(data);
        }

        // GET: /api/showtimes/{showtimeId}/seats
        [HttpGet("showtimes/{showtimeId}/seats")]
        public async Task<IActionResult> GetSeats(string showtimeId)
        {
            if (string.IsNullOrWhiteSpace(showtimeId)) return BadRequest();

            var st = await _context.ShowTimes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShowTimeId == showtimeId);

            if (st == null) return NotFound();

            var now = DateTime.Now;

            // Ghế được xem là đã giữ/đã đặt nếu có ticket trạng thái pending(1) còn hạn hoặc paid(2)
            var blockedSeatIds = await _context.Tickets.AsNoTracking()
                .Where(t => t.ShowTimeId == showtimeId &&
                            (t.Status == 2 || (t.Status == 1 && t.Expire != null && t.Expire > now)))
                .Select(t => t.SeatId)
                .ToListAsync();

            var seats = await (from s in _context.Seats.AsNoTracking()
                               join stype in _context.SeatTypes.AsNoTracking()
                                   on s.SeatTypeId equals stype.SeatTypeId into stg
                               from stype in stg.DefaultIfEmpty()
                               where s.CinemaTheaterId == st.CinemaTheaterId
                               orderby s.RowIndex, s.ColumnIndex
                               select new
                               {
                                   seatId = s.SeatId,
                                   rowLabel = s.RowIndex,
                                   colIndex = s.ColumnIndex,
                                   label = s.Label,
                                   seatTypeId = s.SeatTypeId,
                                   basePrice = stype != null ? stype.Price : 0,
                                   isBooked = blockedSeatIds.Contains(s.SeatId),
                                   isAisle = s.IsAisle,
                                   pairId = s.PairId
                               })
                               .ToListAsync();

            return Ok(seats);
        }

        // GET: /api/snacks
        [HttpGet("snacks")]
        public async Task<IActionResult> GetSnacks()
        {
            var snacks = await _context.Snacks.AsNoTracking()
                .Where(x => x.IsActive == true)
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    snackId = x.SnackId,
                    name = x.Name,
                    price = x.Price,
                    image = x.Image,
                    description = x.Description
                })
                .ToListAsync();

            return Ok(snacks);
        }
    }
}
