// ============================
// File: CinemaS/Areas/Identity/Pages/Account/Manage/Index.cshtml.cs
// (Fix lỗi tuple PaidTotal/BaseTotal; dùng đúng InvoiceHistoryVM ở ViewModels; update modal theo yêu cầu)
// ============================

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CinemaS.Models;
using CinemaS.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        // =========================
        // 1) DI services
        // =========================
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly CinemaContext _context;
        private readonly IWebHostEnvironment _env;

        // =========================
        // 2) Membership config
        // =========================
        private const int TargetPoint = 10000;

        public IndexModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            CinemaContext context,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _env = env;
        }

        // =========================
        // 3) Page state / bind models
        // =========================
        public string Username { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        [BindProperty]
        public IFormFile AvatarFile { get; set; }

        public string AvatarUrl { get; set; }
        public int CurrentPoint { get; set; }
        public int MaxPoint => TargetPoint;
        public bool IsGlFriend { get; set; }

        // =========================
        // 4) History list (DÙNG InvoiceHistoryVM trong CinemaS.Models.ViewModels)
        // =========================
        public System.Collections.Generic.List<InvoiceHistoryVM> History { get; set; }
            = new System.Collections.Generic.List<InvoiceHistoryVM>();

        // =========================
        // 5) InputModel: form update profile
        // =========================
        public class InputModel
        {
            public string FullName { get; set; }
            public string Email { get; set; }

            [Phone]
            public string PhoneNumber { get; set; }

            public DateTime? DateOfBirth { get; set; }
            public string Address { get; set; }
        }

        // =========================
        // 6) Helper: giờ VN (để kiểm tra promo theo thời gian VN)
        // =========================
        private static DateTime VnNow()
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }

        // =========================
        // 7) Helper: tính giảm giá theo rule
        //    - 0 < d <= 1    : tỉ lệ (0.1 = 10%)
        //    - 1 < d <= 100  : % (10 = 10%)
        //    - d > 100       : trừ thẳng tiền
        // =========================
        private static (decimal discountAmount, decimal totalAfterDiscount) CalcDiscount(decimal baseTotal, double discountValue)
        {
            if (baseTotal <= 0m || discountValue <= 0d) return (0m, baseTotal);

            decimal discount;

            if (discountValue <= 1d)
                discount = baseTotal * (decimal)discountValue;
            else if (discountValue <= 100d)
                discount = baseTotal * ((decimal)discountValue / 100m);
            else
                discount = (decimal)discountValue;

            discount = decimal.Round(discount, 0, MidpointRounding.AwayFromZero);

            var payable = baseTotal - discount;
            if (payable < 0m) payable = 0m;

            return (discount, payable);
        }

        // =========================
        // 8) LoadAsync: load profile + history + points
        // =========================
        private async Task LoadAsync(AppUser user)
        {
            var email = await _userManager.GetEmailAsync(user);
            Username = await _userManager.GetUserNameAsync(user);

            // ===== Customer record (Users table) =====
            var customer = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            AvatarUrl = customer?.AvatarUrl;

            // ===== Bind form fields =====
            Input = new InputModel
            {
                FullName = customer?.FullName ?? user.FullName ?? Username,
                Email = email,
                PhoneNumber = customer?.PhoneNumber,
                DateOfBirth = customer?.DateOfBirth,
                Address = customer?.Address
            };

            History.Clear();

            // ===== Không có customer: reset =====
            if (customer == null)
            {
                AvatarUrl = null;
                CurrentPoint = 0;
                IsGlFriend = false;
                return;
            }

            // ===== Load invoices =====
            var invoices = await _context.Invoices.AsNoTracking()
                .Where(i => i.CustomerId == customer.UserId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var invoiceIds = invoices.Select(i => i.InvoiceId).ToList();

            // ===== Map giao dịch SUCCESS theo InvoiceId: lấy amount mới nhất =====
            var paidTxnMap = await _context.PaymentTransactions.AsNoTracking()
                .Where(pt => invoiceIds.Contains(pt.InvoiceId) && pt.Status == 1)
                .GroupBy(pt => pt.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    Amount = g.OrderByDescending(x => (DateTime?)(x.PaidAt ?? x.UpdatedAt ?? x.CreatedAt))
                              .Select(x => x.Amount)
                              .FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.InvoiceId, x => (decimal)x.Amount);

            // ===== Local function: tách giá gốc / giảm / thực trả cho 1 invoice =====
            async Task<(decimal BaseTotal, decimal DiscountAmount, decimal PaidTotal)> GetPriceBreakdownAsync(Invoices inv)
            {
                // BaseTotal: ưu tiên inv.TotalPrice (giá gốc)
                var baseTotal = inv.TotalPrice ?? 0m;
                if (baseTotal <= 0m) return (0m, 0m, 0m);

                // (1) Có txn success => PaidTotal = amount; Discount = base - paid
                if (!string.IsNullOrWhiteSpace(inv.InvoiceId)
                    && paidTxnMap.TryGetValue(inv.InvoiceId, out var paidAmount)
                    && paidAmount > 0m)
                {
                    var discount = baseTotal - paidAmount;
                    if (discount < 0m) discount = 0m;

                    return (BaseTotal: baseTotal, DiscountAmount: discount, PaidTotal: paidAmount);
                }

                // (2) Không có txn success nhưng có PromotionId => tính theo promo (để hiển thị khi pending)
                if (!string.IsNullOrWhiteSpace(inv.PromotionId))
                {
                    var promo = await _context.Promotion.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.PromotionId == inv.PromotionId);

                    if (promo != null && promo.Status == true && promo.Discount.HasValue)
                    {
                        var now = VnNow();
                        if ((!promo.StartDay.HasValue || now >= promo.StartDay.Value) &&
                            (!promo.EndDay.HasValue || now <= promo.EndDay.Value))
                        {
                            var (discount, payable) = CalcDiscount(baseTotal, promo.Discount.Value);
                            return (BaseTotal: baseTotal, DiscountAmount: discount, PaidTotal: payable);
                        }
                    }
                }

                // (3) Không có giảm => paid = base
                return (BaseTotal: baseTotal, DiscountAmount: 0m, PaidTotal: baseTotal);
            }

            // ===== priceMap: key invoiceId -> (BaseTotal, DiscountAmount, PaidTotal) =====
            var priceMap = new System.Collections.Generic.Dictionary<string, (decimal BaseTotal, decimal DiscountAmount, decimal PaidTotal)>();
            foreach (var inv in invoices)
            {
                // inv.InvoiceId có thể null/empty: nếu hệ thống đảm bảo có thì ok; nếu không, vẫn guard
                if (!string.IsNullOrWhiteSpace(inv.InvoiceId))
                    priceMap[inv.InvoiceId] = await GetPriceBreakdownAsync(inv);
            }

            // ===== Điểm: tính theo GIÁ GỐC (BaseTotal) của invoice đã thanh toán (Status == 1) =====
            var paidInvoices = invoices
                .Where(i => i.Status == 1 && i.TotalPrice.HasValue)
                .ToList();

            // NOTE: BaseTotal đang lấy từ i.TotalPrice (giá gốc)
            var computedPoint = paidInvoices.Sum(i => (int)((i.TotalPrice ?? 0m) / 1000m));

            CurrentPoint = computedPoint;
            IsGlFriend = computedPoint > 0;

            // ===== Đồng bộ SavePoint vào Users table nếu lệch =====
            if (customer.SavePoint != computedPoint)
            {
                customer.SavePoint = computedPoint;
                customer.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // ===== Build History =====
            foreach (var inv in invoices)
            {
                // ---- Lấy thông tin movie/room/showtime từ ticket đầu tiên (nếu có) ----
                var firstTicket = await _context.Tickets.AsNoTracking()
                    .Where(t => t.InvoiceId == inv.InvoiceId)
                    .OrderBy(t => t.TicketId)
                    .FirstOrDefaultAsync();

                string movie = null;
                string room = null;
                DateTime? showDate = null;
                DateTime? startTime = null;

                if (firstTicket != null)
                {
                    var st = await _context.ShowTimes.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.ShowTimeId == firstTicket.ShowTimeId);

                    if (st != null)
                    {
                        showDate = st.ShowDate;
                        startTime = st.StartTime;

                        var mv = await _context.Movies.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);

                        var ct = await _context.CinemaTheaters.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CinemaTheaterId == st.CinemaTheaterId);

                        movie = mv?.Title;
                        room = ct?.Name;
                    }
                }

                // ---- Lấy breakdown giá từ priceMap ----
                (decimal BaseTotal, decimal DiscountAmount, decimal PaidTotal) p;

                if (!string.IsNullOrWhiteSpace(inv.InvoiceId) && priceMap.TryGetValue(inv.InvoiceId, out var v))
                {
                    p = v;
                }
                else
                {
                    // FIX lỗi tuple mất tên: PHẢI gán named tuple
                    var baseTotalFallback = inv.TotalPrice ?? 0m;
                    p = (BaseTotal: baseTotalFallback, DiscountAmount: 0m, PaidTotal: baseTotalFallback);
                }

                // ---- Add history item (TotalPrice = PaidTotal; BaseTotalPrice = BaseTotal) ----
                History.Add(new InvoiceHistoryVM
                {
                    InvoiceId = inv.InvoiceId,
                    CreatedAt = inv.CreatedAt,
                    Status = inv.Status ?? 0,

                    // Tổng cộng (hiển thị): số đã thanh toán (sau giảm)
                    TotalPrice = p.PaidTotal,

                    // Giá gốc: dùng cho tính điểm ở view
                    BaseTotalPrice = p.BaseTotal,

                    MovieTitle = movie,
                    Room = room,
                    ShowDate = showDate,
                    StartTime = startTime
                });
            }
        }

        // =========================
        // 9) GET: load page
        // =========================
        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            await LoadAsync(user);
            return Page();
        }

        // =========================
        // 10) POST: update profile
        // =========================
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var email = await _userManager.GetEmailAsync(user);

            // ---- Update AppUser ----
            user.FullName = Input.FullName;
            user.PhoneNumber = Input.PhoneNumber;
            user.Address = Input.Address;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                await LoadAsync(user);
                return Page();
            }

            // ---- Update Users table (customer) ----
            var customer = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (customer != null)
            {
                customer.FullName = Input.FullName;
                customer.PhoneNumber = Input.PhoneNumber;
                customer.Address = Input.Address;
                customer.DateOfBirth = Input.DateOfBirth;
                customer.UpdatedAt = DateTime.UtcNow;

                _context.Users.Update(customer);
                await _context.SaveChangesAsync();
            }

            await _signInManager.RefreshSignInAsync(user);

            StatusMessage = "Cập nhật thành công!";
            return RedirectToPage();
        }

        // =========================
        // 11) POST: upload avatar
        // =========================
        public async Task<IActionResult> OnPostAvatarAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var email = await _userManager.GetEmailAsync(user);
            var customer = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (customer == null) return NotFound("Customer not found.");
            if (AvatarFile == null || AvatarFile.Length == 0) return RedirectToPage();

            // ---- Save file to wwwroot/images/avatars ----
            var folder = Path.Combine(_env.WebRootPath, "images", "avatars");
            Directory.CreateDirectory(folder);

            var fileName = $"avatar_{customer.UserId}_{Guid.NewGuid():N}{Path.GetExtension(AvatarFile.FileName)}";
            var path = Path.Combine(folder, fileName);

            using (var fs = System.IO.File.Create(path))
            {
                await AvatarFile.CopyToAsync(fs);
            }

            customer.AvatarUrl = "/images/avatars/" + fileName;
            customer.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            StatusMessage = "Đổi ảnh đại diện thành công!";
            return RedirectToPage();
        }

        // =========================
        // 12) GET: Modal chi tiết hóa đơn (VÉ / SNACK)
      
        public async Task<IActionResult> OnGetTicketDetailModalAsync(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                return Content("Không có mã hóa đơn.", "text/html");

            var invoice = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null)
                return Content("Hóa đơn không tồn tại.", "text/html");

            // ---- Load vé + snack ----
            var tickets = await _context.Tickets.AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .ToListAsync();

            var snackLines = await _context.DetailBookingSnacks.AsNoTracking()
                .Where(d => d.InvoiceId == invoiceId)
                .ToListAsync();

            var hasTickets = tickets.Any();
            var hasSnacks = snackLines.Any();

            if (!hasTickets && !hasSnacks)
                return Content("Hóa đơn này chưa có vé hoặc bắp nước.", "text/html");

            // =========================
            // A) TÍNH GIÁ: base / discount / paid
            //    Ưu tiên PaymentTransactions SUCCESS; nếu không có, fallback promo; nếu không có => base
            // =========================
            var baseTotal = invoice.TotalPrice ?? 0m;

            var paidTxn = await _context.PaymentTransactions.AsNoTracking()
                .Where(pt => pt.InvoiceId == invoiceId && pt.Status == 1)
                .OrderByDescending(x => (DateTime?)(x.PaidAt ?? x.UpdatedAt ?? x.CreatedAt))
                .FirstOrDefaultAsync();

            decimal paidTotal;
            decimal discountAmount;

            if (paidTxn != null && paidTxn.Amount > 0m)
            {
                // ---- SUCCESS txn ----
                paidTotal = (decimal)paidTxn.Amount;
                discountAmount = baseTotal - paidTotal;
                if (discountAmount < 0m) discountAmount = 0m;
            }
            else if (!string.IsNullOrWhiteSpace(invoice.PromotionId))
            {
                // ---- Promo fallback ----
                var promo = await _context.Promotion.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PromotionId == invoice.PromotionId);

                if (promo != null && promo.Status == true && promo.Discount.HasValue)
                {
                    var now = VnNow();
                    if ((!promo.StartDay.HasValue || now >= promo.StartDay.Value) &&
                        (!promo.EndDay.HasValue || now <= promo.EndDay.Value))
                    {
                        var calc = CalcDiscount(baseTotal, promo.Discount.Value);
                        discountAmount = calc.discountAmount;
                        paidTotal = calc.totalAfterDiscount;
                    }
                    else
                    {
                        discountAmount = 0m;
                        paidTotal = baseTotal;
                    }
                }
                else
                {
                    discountAmount = 0m;
                    paidTotal = baseTotal;
                }
            }
            else
            {
                // ---- No discount ----
                discountAmount = 0m;
                paidTotal = baseTotal;
            }

            // ---- Format text ----
            string baseText = baseTotal.ToString("#,0");
            string discountText = discountAmount.ToString("#,0");
            string paidText = paidTotal.ToString("#,0");

            // =========================
            // B) HÓA ĐƠN VÉ (có thể kèm snack)
            // =========================
            if (hasTickets)
            {
                var firstTicket = tickets.First();

                var st = await _context.ShowTimes.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ShowTimeId == firstTicket.ShowTimeId);

                if (st == null)
                    return Content("Không tìm thấy suất chiếu.", "text/html");

                var mv = await _context.Movies.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);

                var room = await _context.CinemaTheaters.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CinemaTheaterId == st.CinemaTheaterId);

                MovieTheaters theater = null;
                if (room != null && !string.IsNullOrEmpty(room.MovieTheaterId))
                {
                    theater = await _context.MovieTheaters.AsNoTracking()
                        .FirstOrDefaultAsync(mt => mt.MovieTheaterId == room.MovieTheaterId);
                }

                // ---- Ghế ----
                var seatIds = tickets.Select(t => t.SeatId).ToList();
                var seatLabels = await _context.Seats.AsNoTracking()
                    .Where(s => seatIds.Contains(s.SeatId))
                    .Select(s => s.Label ?? s.SeatId)
                    .OrderBy(x => x)
                    .ToListAsync();

                string seatText = string.Join(" ", seatLabels);

                // ---- Snack lines ----
                string snackLinesHtml = "Không có";
                if (hasSnacks)
                {
                    var snackIds = snackLines.Select(x => x.SnackId).ToList();
                    var snackMap = await _context.Snacks.AsNoTracking()
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

                // ---- Display text ----
                string movieTitle = mv?.Title ?? "N/A";
                string timeText = st.StartTime?.ToString("HH:mm") ?? "";
                string dateText = st.ShowDate?.ToString("dd/MM/yyyy") ?? "";
                string roomLabel = room?.Name ?? room?.CinemaTheaterId ?? "N/A";
                string cinemaName = theater?.Name ?? "N/A";
                string cinemaAddr = theater?.Address ?? "";
                string typeLabel = hasSnacks ? "HÓA ĐƠN VÉ + BẮP NƯỚC" : "HÓA ĐƠN VÉ";

                // ---- HTML content (giữ bố cục cũ + thêm giá gốc/giảm/đã thanh toán) ----
                string html = $@"
<div style='border-radius:16px; padding:24px 28px; color:#f9fafb; font-size:15px;'>

  <div style='font-size:12px; font-weight:800; letter-spacing:.18em; text-transform:uppercase; color:#93c5fd; margin-bottom:4px;'>
    {typeLabel}
  </div>

  <div style='font-size:14px; font-weight:800; text-transform:uppercase; color:#fde047;'>TÊN PHIM</div>
  <div style='font-size:22px; font-weight:900; margin-bottom:20px;'>{movieTitle}</div>

  <div style='display:grid; grid-template-columns:1fr 1fr; gap:22px;'>
    <div>
      <div style='margin-bottom:14px;'>
        <div style='font-weight:700; color:#fde047;'>Mã hóa đơn</div>
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
        <div style='font-weight:700; color:#fde047;'>Thời gian chiếu</div>
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
    <div style='font-size:13px; opacity:.9;'>{cinemaAddr}</div>
  </div>

  <hr style='margin:20px 0 14px; border:none; border-top:1px dashed rgba(255,255,255,.4);' />

  <div style='display:flex; justify-content:space-between; margin-bottom:8px;'>
    <span style='font-weight:700; color:#e5e7eb;'>Giá gốc</span>
    <span style='font-weight:800;'>{baseText} VNĐ</span>
  </div>
  <div style='display:flex; justify-content:space-between; margin-bottom:8px;'>
    <span style='font-weight:700; color:#e5e7eb;'>Tiền giảm</span>
    <span style='font-weight:800;'>{discountText} VNĐ</span>
  </div>

  <div style='display:flex; justify-content:space-between; align-items:center; margin-top:10px;'>
    <span style='font-size:18px; font-weight:800; color:#fde047;'>ĐÃ THANH TOÁN</span>
    <span style='font-size:22px; font-weight:900;'>{paidText} VNĐ</span>
  </div>

</div>";

                return Content(html, "text/html");
            }

           
            {
                // ---- Map snackId -> snackName ----
                var snackIds = snackLines.Select(x => x.SnackId).ToList();
                var snackMap = await _context.Snacks.AsNoTracking()
                    .Where(s => snackIds.Contains(s.SnackId))
                    .ToDictionaryAsync(s => s.SnackId, s => s.Name);

                // ---- Build snack lines html ----
                string snackLinesHtml = "";
                foreach (var line in snackLines)
                {
                    snackMap.TryGetValue(line.SnackId, out var name);
                    int qty = line.TotalSnack ?? 0;
                    snackLinesHtml += $"{(string.IsNullOrEmpty(name) ? "Snack" : name)} × {qty}<br/>";
                }

                // ---- Thời gian mua ----
                string createdText = invoice.CreatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "N/A";

           
                string cinemaName = null;
                string cinemaAddr = null;

                try
                {
                    // (1) MovieTheaterId direct
                    var mtProp = invoice.GetType().GetProperty("MovieTheaterId");
                    var mtId = mtProp?.GetValue(invoice) as string;

                    if (!string.IsNullOrWhiteSpace(mtId))
                    {
                        var mt = await _context.MovieTheaters.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.MovieTheaterId == mtId);

                        cinemaName = mt?.Name;
                        cinemaAddr = mt?.Address;
                    }
                    else
                    {
                        // (2) CinemaTheaterId -> CinemaTheaters -> MovieTheaterId
                        var ctProp = invoice.GetType().GetProperty("CinemaTheaterId");
                        var ctId = ctProp?.GetValue(invoice) as string;

                        if (!string.IsNullOrWhiteSpace(ctId))
                        {
                            var ct = await _context.CinemaTheaters.AsNoTracking()
                                .FirstOrDefaultAsync(x => x.CinemaTheaterId == ctId);

                            if (ct != null && !string.IsNullOrWhiteSpace(ct.MovieTheaterId))
                            {
                                var mt = await _context.MovieTheaters.AsNoTracking()
                                    .FirstOrDefaultAsync(x => x.MovieTheaterId == ct.MovieTheaterId);

                                cinemaName = mt?.Name ?? ct.Name;
                                cinemaAddr = mt?.Address;
                            }
                            else
                            {
                                cinemaName = ct?.Name;
                            }
                        }
                    }
                }
                catch
                {
                    // Không thêm giả định khác
                }

                string cinemaTextName = !string.IsNullOrWhiteSpace(cinemaName) ? cinemaName : "Không đủ dữ liệu để xác minh";
                string cinemaTextAddr = !string.IsNullOrWhiteSpace(cinemaAddr) ? cinemaAddr : "";

                // ---- HTML content ----
                string snackHtml = $@"
<div style='border-radius:16px; padding:24px 28px; color:#f9fafb; font-size:15px;'>

  <div style='font-size:12px; font-weight:800; letter-spacing:.18em; text-transform:uppercase; color:#93c5fd; margin-bottom:4px;'>
    HÓA ĐƠN BẮP NƯỚC
  </div>

  <div style='font-size:20px; font-weight:900; margin-bottom:16px;'>
    Mã hóa đơn: {invoice.InvoiceId}
  </div>

  <div style='margin-bottom:12px;'>
    <div style='font-weight:700; color:#fde047;'>Thời gian mua</div>
    <div>{createdText}</div>
  </div>

  

  <div style='margin-bottom:14px;'>
    <div style='font-weight:700; color:#fde047;'>Danh sách bắp nước</div>
    <div>{snackLinesHtml}</div>
  </div>

  <hr style='margin:20px 0 14px; border:none; border-top:1px dashed rgba(255,255,255,.4);' />

  <div style='display:flex; justify-content:space-between; margin-bottom:8px;'>
    <span style='font-weight:700; color:#e5e7eb;'>Giá gốc</span>
    <span style='font-weight:800;'>{baseText} VNĐ</span>
  </div>
  <div style='display:flex; justify-content:space-between; margin-bottom:8px;'>
    <span style='font-weight:700; color:#e5e7eb;'>Tiền giảm</span>
    <span style='font-weight:800;'>{discountText} VNĐ</span>
  </div>

  <div style='display:flex; justify-content:space-between; align-items:center; margin-top:10px;'>
    <span style='font-size:18px; font-weight:800; color:#fde047;'>ĐÃ THANH TOÁN</span>
    <span style='font-size:22px; font-weight:900;'>{paidText} VNĐ</span>
  </div>

</div>";

                return Content(snackHtml, "text/html");
            }
        }
    }
}
