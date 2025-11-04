using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;
using CinemaS.Models.ViewModels;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CinemaTheatersController : Controller
    {
        private readonly CinemaContext _context;

        public CinemaTheatersController(CinemaContext context)
        {
            _context = context;
        }

        // ================== LIST ==================
        // Trả về danh sách phòng chiếu kèm tên loại phòng, tên rạp và tổng số ghế
        public async Task<IActionResult> Index()
        {
            var theaters = await _context.CinemaTheaters
                .Select(ct => new CinemaTheaterVM
                {
                    CinemaTheaterId = ct.CinemaTheaterId,
                    Name = ct.Name,
                    CinemaTypeId = ct.CinemaTypeId,
                    MovieTheaterId = ct.MovieTheaterId,
                    NumOfRows = ct.NumOfRows,
                    NumOfColumns = ct.NumOfColumns,
                    Status = ct.Status
                })
                .ToListAsync();

            foreach (var t in theaters)
            {
                var cinemaType = await _context.CinemaTypes
                    .FirstOrDefaultAsync(x => x.CinemaTypeId == t.CinemaTypeId);
                var movieTheater = await _context.MovieTheaters
                    .FirstOrDefaultAsync(x => x.MovieTheaterId == t.MovieTheaterId);

                t.CinemaTypeName = cinemaType?.Name;
                t.MovieTheaterName = movieTheater?.Name;
                t.TotalSeats = await _context.Seats.CountAsync(s => s.CinemaTheaterId == t.CinemaTheaterId);
            }

            return View(theaters);
        }

        // ================== DETAILS ==================
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var cinemaTheater = await _context.CinemaTheaters
                .FirstOrDefaultAsync(m => m.CinemaTheaterId == id);

            if (cinemaTheater == null) return NotFound();

            return View(cinemaTheater);
        }

        // ================== CREATE ==================
        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CinemaTheaters cinemaTheater)
        {
            // Bỏ qua validate cho khóa chính vì sẽ tự sinh
            ModelState.Remove(nameof(cinemaTheater.CinemaTheaterId));

            // LOG nhanh để debug khi cần
            Console.WriteLine("=== CREATE CINEMATHEATER ===");
            Console.WriteLine($"Name: {cinemaTheater?.Name}");
            Console.WriteLine($"CinemaTypeId: {cinemaTheater?.CinemaTypeId}");
            Console.WriteLine($"MovieTheaterId: {cinemaTheater?.MovieTheaterId}");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("❌ ModelState INVALID");
                LoadDropdowns();
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin đã nhập!";
                return View(cinemaTheater);
            }

            try
            {
                // Kiểm tra đã có đủ loại ghế (NORMAL, VIP, COUPLE)
                var seatTypesCount = await _context.SeatTypes.CountAsync();
                if (seatTypesCount < 3)
                {
                    TempData["Error"] = "Vui lòng tạo đủ 3 loại ghế NORMAL, VIP, COUPLE trong database trước!";
                    LoadDropdowns();
                    return View(cinemaTheater);
                }

                // Sinh mã phòng CTxxx
                cinemaTheater.CinemaTheaterId = await GenerateNewTheaterIdAsync();
                cinemaTheater.Status = 1; // Active

                // Thiết lập sơ đồ ghế mặc định (6 hàng A-F, 14 cột)
                cinemaTheater.NumOfRows = 6;      // A..F
                cinemaTheater.NumOfColumns = 14;  // 14 cột
                cinemaTheater.RegularSeatRow = 3; // A-C
                cinemaTheater.VIPSeatRow = 2;     // D-E (trung tâm là VIP)
                cinemaTheater.DoubleSeatRow = 1;  // F (ghế đôi)

                _context.Add(cinemaTheater);
                await _context.SaveChangesAsync();

                // Tạo ghế tự động theo bố cục trên
                await CreateSeatsForTheater(cinemaTheater);

                TempData["Message"] = $"✅ Tạo phòng chiếu '{cinemaTheater.Name}' và 72 ghế thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EXCEPTION: {ex.Message}");
                TempData["Error"] = ex.InnerException == null
                    ? $"Lỗi: {ex.Message}"
                    : $"Lỗi: {ex.Message} | Chi tiết: {ex.InnerException.Message}";
                LoadDropdowns();
                return View(cinemaTheater);
            }
        }

        // ================== EDIT ==================
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var cinemaTheaters = await _context.CinemaTheaters.FindAsync(id);
            if (cinemaTheaters == null) return NotFound();

            LoadDropdowns();
            return View(cinemaTheaters);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, CinemaTheaters cinemaTheaters)
        {
            if (id != cinemaTheaters.CinemaTheaterId) return NotFound();

            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(cinemaTheaters);
            }

            try
            {
                _context.Update(cinemaTheaters);
                await _context.SaveChangesAsync();
                TempData["Message"] = "✅ Cập nhật phòng chiếu thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CinemaTheatersExists(cinemaTheaters.CinemaTheaterId))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // ================== DELETE ==================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var cinemaTheaters = await _context.CinemaTheaters
                .FirstOrDefaultAsync(m => m.CinemaTheaterId == id);

            if (cinemaTheaters == null) return NotFound();

            return View(cinemaTheaters);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var cinemaTheaters = await _context.CinemaTheaters.FindAsync(id);
            if (cinemaTheaters != null)
            {
                _context.CinemaTheaters.Remove(cinemaTheaters);

                // Xóa ghế liên quan (nếu muốn dọn sạch)
                var seats = _context.Seats.Where(s => s.CinemaTheaterId == id);
                _context.Seats.RemoveRange(seats);
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = "🗑️ Đã xóa phòng chiếu (và ghế) thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ================== HELPERS ==================
        private bool CinemaTheatersExists(string id)
        {
            return _context.CinemaTheaters.Any(e => e.CinemaTheaterId == id);
        }

        private void LoadDropdowns()
        {
            ViewBag.CinemaTypes = new SelectList(
                _context.CinemaTypes.OrderBy(ct => ct.Name),
                "CinemaTypeId", "Name");

            ViewBag.MovieTheaters = new SelectList(
                _context.MovieTheaters.OrderBy(mt => mt.Name),
                "MovieTheaterId", "Name");
        }

        // Sinh ID CTxxx
        private async Task<string> GenerateNewTheaterIdAsync()
        {
            var last = await _context.CinemaTheaters
                .OrderByDescending(ct => ct.CinemaTheaterId)
                .FirstOrDefaultAsync();

            if (last == null) return "CT001";
            var num = int.Parse(last.CinemaTheaterId.Substring(2));
            return $"CT{(num + 1):D3}";
        }

        // Tạo ghế theo bố cục mặc định:
        // A-C: NORMAL; D-E: VIP ở cột 4..11, ngoài ra NORMAL; F: COUPLE
        private async Task CreateSeatsForTheater(CinemaTheaters theater)
        {
            var seats = new List<Seats>();
            var seatTypes = await _context.SeatTypes.ToListAsync();

            var normal = seatTypes.FirstOrDefault(st => st.Name == "NORMAL");
            var vip = seatTypes.FirstOrDefault(st => st.Name == "VIP");
            var couple = seatTypes.FirstOrDefault(st => st.Name == "COUPLE");

            if (normal == null || vip == null || couple == null)
                throw new Exception("Thiếu SeatType: cần NORMAL, VIP, COUPLE.");

            // Lấy seatId lớn nhất hiện tại để không trùng
            var lastSeat = await _context.Seats
                .OrderByDescending(s => s.SeatId)
                .FirstOrDefaultAsync();

            int counter = 1;
            if (lastSeat != null && lastSeat.SeatId.StartsWith("S"))
            {
                counter = int.Parse(lastSeat.SeatId.Substring(1)) + 1;
            }

            // A..C: NORMAL (3*14 = 42)
            for (char row = 'A'; row <= 'C'; row++)
            {
                for (int col = 1; col <= 14; col++)
                {
                    seats.Add(new Seats
                    {
                        SeatId = $"S{counter:D6}",
                        SeatTypeId = normal.SeatTypeId,
                        CinemaTheaterId = theater.CinemaTheaterId,
                        RowIndex = row.ToString(),
                        ColumnIndex = col,
                        Label = $"{row}{col}"
                    });
                    counter++;
                }
            }

            // D..E: VIP ở giữa (cột 4..11), còn lại NORMAL
            for (char row = 'D'; row <= 'E'; row++)
            {
                for (int col = 1; col <= 14; col++)
                {
                    var type = (col >= 4 && col <= 11) ? vip.SeatTypeId : normal.SeatTypeId;
                    seats.Add(new Seats
                    {
                        SeatId = $"S{counter:D6}",
                        SeatTypeId = type,
                        CinemaTheaterId = theater.CinemaTheaterId,
                        RowIndex = row.ToString(),
                        ColumnIndex = col,
                        Label = $"{row}{col}"
                    });
                    counter++;
                }
            }

            // F: COUPLE (14)
            for (int col = 1; col <= 14; col++)
            {
                seats.Add(new Seats
                {
                    SeatId = $"S{counter:D6}",
                    SeatTypeId = couple.SeatTypeId,
                    CinemaTheaterId = theater.CinemaTheaterId,
                    RowIndex = "F",
                    ColumnIndex = col,
                    Label = $"F{col}"
                });
                counter++;
            }

            _context.Seats.AddRange(seats);
            await _context.SaveChangesAsync();
        }
    }
}
