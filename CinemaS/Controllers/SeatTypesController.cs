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
    public class SeatTypesController : Controller
    {
        private readonly CinemaContext _context;

        public SeatTypesController(CinemaContext context)
        {
            _context = context;
        }

        // GET: SeatTypes
        public async Task<IActionResult> Index(string searchString)
        {
            var seatTypes = from s in _context.SeatTypes
                            select s;

            // Nếu có chuỗi tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.Trim().ToLower();

                seatTypes = seatTypes.Where(s =>
                    s.Name.ToLower().Contains(searchString) ||
                    s.Price.ToString().Contains(searchString)
                );
            }

            seatTypes = seatTypes.OrderBy(s => s.SeatTypeId);
            return View(await seatTypes.ToListAsync());
        }

        // GET: SeatTypes/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seatTypes = await _context.SeatTypes
                .FirstOrDefaultAsync(m => m.SeatTypeId == id);
            if (seatTypes == null)
            {
                return NotFound();
            }

            return View(seatTypes);
        }

        // GET: SeatTypes/Create
        public async Task<IActionResult> Create()
        {
            // Generate next ID to show in the create form
            try
            {
                var nextId = await GenerateNewSeatTypeIdAsync();
                ViewBag.NextSeatTypeId = nextId;
            }
            catch
            {
                ViewBag.NextSeatTypeId = null;
            }

            return View();
        }

        // POST: SeatTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Price")] SeatTypes seatTypes)
        {
            // Bỏ qua validate ID vì tự sinh
            ModelState.Remove(nameof(seatTypes.SeatTypeId));

            if (!ModelState.IsValid)
            {
                return View(seatTypes);
            }

            try
            {
                // Kiểm tra tên trùng
                var nameExists = await _context.SeatTypes
                    .AnyAsync(st => st.Name == seatTypes.Name);

                if (nameExists)
                {
                    TempData["Error"] = "❌ Tên loại ghế đã tồn tại!";
                    return View(seatTypes);
                }

                // Auto-generate ID: ST001, ST002, ST003...
                seatTypes.SeatTypeId = await GenerateNewSeatTypeIdAsync();

                _context.Add(seatTypes);
                await _context.SaveChangesAsync();

                // Tự động tạo TicketType tương ứng
                await CreateCorrespondingTicketTypeAsync(seatTypes);

                TempData["Message"] = $"✅ Tạo loại ghế '{seatTypes.Name}' thành công! (Mã: {seatTypes.SeatTypeId})";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Lỗi: {ex.InnerException?.Message ?? ex.Message}";
                return View(seatTypes);
            }
        }

        // GET: SeatTypes/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seatTypes = await _context.SeatTypes.FindAsync(id);
            if (seatTypes == null)
            {
                return NotFound();
            }
            return View(seatTypes);
        }

        // POST: SeatTypes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("SeatTypeId,Name,Price")] SeatTypes seatTypes)
        {
            if (id != seatTypes.SeatTypeId)
            {
                TempData["Error"] = "❌ Mã loại ghế không khớp!";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(seatTypes);
            }

            try
            {
                // Đảm bảo giữ nguyên format STxxx
                if (!seatTypes.SeatTypeId.StartsWith("ST") || seatTypes.SeatTypeId.Length != 5)
                {
                    TempData["Error"] = "❌ Mã loại ghế phải theo định dạng STxxx (VD: ST001)!";
                    return View(seatTypes);
                }

                // Kiểm tra xem có đồ ăn nào đang sử dụng loại này không
                var inUse = await _context.Seats
                    .AnyAsync(s => s.SeatTypeId == id);

                if (inUse)
                {
                    TempData["Warning"] = "⚠️ Loại ghế này đang được sử dụng. Cập nhật sẽ ảnh hưởng đến các ghế hiện có.";
                }

                // Kiểm tra tên trùng (trừ chính nó)
                var nameExists = await _context.SeatTypes
                    .AnyAsync(st => st.Name == seatTypes.Name && st.SeatTypeId != id);

                if (nameExists)
                {
                    TempData["Error"] = "❌ Tên loại ghế đã tồn tại!";
                    return View(seatTypes);
                }

                _context.Update(seatTypes);
                await _context.SaveChangesAsync();

                // Cập nhật TicketType tương ứng nếu có
                await UpdateCorrespondingTicketTypeAsync(seatTypes);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SeatTypesExists(seatTypes.SeatTypeId))
                {
                    TempData["Error"] = "❌ Loại ghế không tồn tại!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ Lỗi: {ex.Message}";
                return View(seatTypes);
            }
        }

        // GET: SeatTypes/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var seatTypes = await _context.SeatTypes
                .FirstOrDefaultAsync(m => m.SeatTypeId == id);
            if (seatTypes == null)
            {
                return NotFound();
            }

            return View(seatTypes);
        }

        // POST: SeatTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var seatTypes = await _context.SeatTypes.FindAsync(id);
            if (seatTypes != null)
            {
                _context.SeatTypes.Remove(seatTypes);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ================== HELPER METHODS ==================

        private bool SeatTypesExists(string id)
        {
            return _context.SeatTypes.Any(e => e.SeatTypeId == id);
        }

        // Auto-generate SeatType ID: ST001, ST002, ST003...
        private async Task<string> GenerateNewSeatTypeIdAsync()
        {
            var last = await _context.SeatTypes
                .OrderByDescending(st => st.SeatTypeId)
                .FirstOrDefaultAsync();

            if (last == null) return "ST001";

            // Parse số từ ID cuối (VD: ST001 -> 1)
            var lastNumber = int.Parse(last.SeatTypeId.Substring(2));
            return $"ST{(lastNumber + 1):D3}";
        }

        // Tự động tạo TicketType tương ứng khi tạo SeatType
        private async Task CreateCorrespondingTicketTypeAsync(SeatTypes seatType)
        {
            // Tạo TicketType ID tương ứng: ST001 -> TT001
            var ticketTypeId = "TT" + seatType.SeatTypeId.Substring(2);

            // Kiểm tra xem đã tồn tại chưa
            var exists = await _context.TicketTypes
                .AnyAsync(tt => tt.TicketTypeId == ticketTypeId);

            if (!exists)
            {
                var ticketType = new TicketTypes
                {
                    TicketTypeId = ticketTypeId,
                    Name = seatType.Name,
                    Description = $"Loại vé cho ghế {seatType.Name}",
                    Price = seatType.Price
                };

                _context.TicketTypes.Add(ticketType);
                await _context.SaveChangesAsync();
            }
        }

        // Cập nhật TicketType tương ứng khi cập nhật SeatType
        private async Task UpdateCorrespondingTicketTypeAsync(SeatTypes seatType)
        {
            var ticketTypeId = "TT" + seatType.SeatTypeId.Substring(2);
            var ticketType = await _context.TicketTypes.FindAsync(ticketTypeId);

            if (ticketType != null)
            {
                ticketType.Name = seatType.Name;
                ticketType.Description = $"Loại vé cho ghế {seatType.Name}";
                ticketType.Price = seatType.Price;

                _context.Update(ticketType);
                await _context.SaveChangesAsync();
            }
        }
    }
}
