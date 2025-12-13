using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CinemaS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PromotionsController : Controller
    {
        private readonly CinemaContext _context;
        private readonly UserManager<AppUser> _userManager;

        public PromotionsController(CinemaContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // TẠO MÃ KM TỰ ĐỘNG: PR001, PR002,...
        private string GenerateNextPromotionId()
        {
            var lastId = _context.Promotion
                                 .OrderByDescending(p => p.PromotionId)
                                 .Select(p => p.PromotionId)
                                 .FirstOrDefault();

            if (string.IsNullOrEmpty(lastId))
                return "PR001";

            var prefix = new string(lastId.TakeWhile(c => !char.IsDigit(c)).ToArray());
            var numberPart = new string(lastId.Skip(prefix.Length).ToArray());

            if (!int.TryParse(numberPart, out var num))
                return lastId;

            num++;
            var formatted = num.ToString(new string('0', numberPart.Length));
            return prefix + formatted;
        }

        /// <summary>
        /// Load danh sách user có quyền Admin (Identity) + map sang bảng Users (User_ID) để bind dropdown.
        /// </summary>
        private async Task LoadAdminUsersAsync(string? selectedUserId = null)
        {
            // Lấy account Identity có role Admin
            var identityAdmins = await _userManager.GetUsersInRoleAsync("Admin");

            var adminEmails = identityAdmins
                .Select(a => a.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToList();

            if (!adminEmails.Any())
            {
                ViewBag.AdminUsers = new List<SelectListItem>();
                return;
            }

            // Map sang bảng Users theo Email để lấy User_ID (10 ký tự) + Full_Name
            var cinemaAdmins = await _context.Users
                .Where(u => adminEmails.Contains(u.Email))
                .ToListAsync();

            var items = cinemaAdmins
                .Select(u => new SelectListItem
                {
                    Value = u.UserId,
                    Text = $"{u.UserId} - {u.FullName ?? u.Email}",
                    Selected = !string.IsNullOrEmpty(selectedUserId) && u.UserId == selectedUserId
                })
                .OrderBy(x => x.Text)
                .ToList();

            ViewBag.AdminUsers = items;
        }

        // GET: Promotions
        public async Task<IActionResult> Index(string? search, DateTime? fromDate, DateTime? toDate, bool? onlyActive)
        {
            var query = _context.Promotion.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(p =>
                    (p.Name != null && p.Name.Contains(s)) ||
                    (p.Code != null && p.Code.Contains(s)) ||
                    (p.Content != null && p.Content.Contains(s)));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.StartDay >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(p => p.EndDay <= end);
            }

            if (onlyActive.HasValue)
            {
                var now = DateTime.Now;

                if (onlyActive.Value)
                {
                    // Đang áp dụng = Status = true và trong khoảng thời gian hiệu lực
                    query = query.Where(p =>
                        p.Status == true &&
                        (!p.StartDay.HasValue || p.StartDay.Value <= now) &&
                        (!p.EndDay.HasValue || p.EndDay.Value >= now));
                }
                else
                {
                    // Không áp dụng = Status = false/null hoặc đã hết hạn theo ngày
                    query = query.Where(p =>
                        p.Status == false ||
                        p.Status == null ||
                        (p.EndDay.HasValue && p.EndDay.Value < now));
                }
            }

            var list = await query
                .OrderByDescending(p => p.StartDay ?? DateTime.MinValue)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.OnlyActive = onlyActive;

            return View(list);
        }

        // GET: Promotions/Create
        public async Task<IActionResult> Create()
        {
            var model = new Promotion
            {
                PromotionId = GenerateNextPromotionId(),
                StartDay = DateTime.Today,
                EndDay = DateTime.Today.AddDays(7),
                Status = true,
                Discount = 10
            };

            await LoadAdminUsersAsync(model.UserId);

            return View(model);
        }

        // POST: Promotions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Promotion promotion)
        {
            if (string.IsNullOrWhiteSpace(promotion.PromotionId))
                promotion.PromotionId = GenerateNextPromotionId();

            if (promotion.StartDay.HasValue && promotion.EndDay.HasValue &&
                promotion.EndDay.Value < promotion.StartDay.Value)
            {
                ModelState.AddModelError(nameof(Promotion.EndDay),
                    "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            if (promotion.Discount.HasValue &&
                (promotion.Discount.Value < 0 || promotion.Discount.Value > 100))
            {
                ModelState.AddModelError(nameof(Promotion.Discount),
                    "Chiết khấu phải từ 0 đến 100 (%).");
            }

            // UserId bắt buộc chọn (nhân viên admin tạo)
            if (string.IsNullOrWhiteSpace(promotion.UserId))
            {
                ModelState.AddModelError(nameof(Promotion.UserId),
                    "Vui lòng chọn nhân viên tạo khuyến mãi (Admin).");
            }

            if (!ModelState.IsValid)
            {
                await LoadAdminUsersAsync(promotion.UserId);
                return View(promotion);
            }

            _context.Add(promotion);
            await _context.SaveChangesAsync();
            TempData["Message"] = "Đã tạo mã khuyến mãi mới.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Promotions/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var promotion = await _context.Promotion.FindAsync(id);
            if (promotion == null) return NotFound();

            await LoadAdminUsersAsync(promotion.UserId);

            return View(promotion);
        }

        // POST: Promotions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Promotion promotion)
        {
            if (id != promotion.PromotionId) return NotFound();

            if (promotion.StartDay.HasValue && promotion.EndDay.HasValue &&
                promotion.EndDay.Value < promotion.StartDay.Value)
            {
                ModelState.AddModelError(nameof(Promotion.EndDay),
                    "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            if (promotion.Discount.HasValue &&
                (promotion.Discount.Value < 0 || promotion.Discount.Value > 100))
            {
                ModelState.AddModelError(nameof(Promotion.Discount),
                    "Chiết khấu phải từ 0 đến 100 (%).");
            }

            if (string.IsNullOrWhiteSpace(promotion.UserId))
            {
                ModelState.AddModelError(nameof(Promotion.UserId),
                    "Vui lòng chọn nhân viên tạo/quản lý khuyến mãi (Admin).");
            }

            if (!ModelState.IsValid)
            {
                await LoadAdminUsersAsync(promotion.UserId);
                return View(promotion);
            }

            try
            {
                _context.Update(promotion);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PromotionExists(promotion.PromotionId))
                    return NotFound();

                throw;
            }

            TempData["Message"] = "Đã cập nhật mã khuyến mãi.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Promotions/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var promotion = await _context.Promotion
                .FirstOrDefaultAsync(m => m.PromotionId == id);
            if (promotion == null) return NotFound();

            return View(promotion);
        }

        // POST: Promotions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var promotion = await _context.Promotion.FindAsync(id);
            if (promotion != null)
            {
                _context.Promotion.Remove(promotion);
                await _context.SaveChangesAsync();
            }

            TempData["Message"] = "Đã xóa mã khuyến mãi.";
            return RedirectToAction(nameof(Index));
        }

        private bool PromotionExists(string id)
        {
            return _context.Promotion.Any(e => e.PromotionId == id);
        }
    }
}
