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
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly CinemaContext _context;
        private readonly IWebHostEnvironment _env;

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

        public System.Collections.Generic.List<InvoiceHistoryVM> History { get; set; }
            = new System.Collections.Generic.List<InvoiceHistoryVM>();

        public class InputModel
        {
            public string FullName { get; set; }
            public string Email { get; set; }

            [Phone]
            public string PhoneNumber { get; set; }

            public DateTime? DateOfBirth { get; set; }
            public string Address { get; set; }
        }

        private async Task LoadAsync(AppUser user)
        {
            var email = await _userManager.GetEmailAsync(user);
            Username = await _userManager.GetUserNameAsync(user);

            var customer = await _context.Users
    .FirstOrDefaultAsync(u => u.Email == email);

            AvatarUrl = customer?.AvatarUrl;
            


            Input = new InputModel
            {
                FullName = customer?.FullName ?? user.FullName ?? Username,
                Email = email,
                PhoneNumber = customer?.PhoneNumber,
                DateOfBirth = customer?.DateOfBirth,
                Address = customer?.Address
            };

            History.Clear();

            if (customer != null)
            {
                var invoices = await _context.Invoices.AsNoTracking()
                    .Where(i => i.CustomerId == customer.UserId)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();

                // 1) Tính điểm từ các hóa đơn đã thanh toán
                var paidInvoices = invoices
                    .Where(i => i.Status == 1 && i.TotalPrice.HasValue)
                    .ToList();

                // Mỗi 1.000 đ = 1 điểm (giống phần view đang dùng)
                var computedPoint = paidInvoices.Sum(i => (int)((i.TotalPrice ?? 0m) / 1000m));

                CurrentPoint = computedPoint;
                IsGlFriend = computedPoint > 0;

                // 2) Lưu vào SavePoint nếu khác giá trị hiện tại
                if (customer.SavePoint != computedPoint)
                {
                    customer.SavePoint = computedPoint;
                    customer.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // 3) Build History như cũ
                foreach (var inv in invoices)
                {
                    var firstTicket = await _context.Tickets.AsNoTracking()
                        .Where(t => t.InvoiceId == inv.InvoiceId)
                        .OrderBy(t => t.TicketId)
                        .FirstOrDefaultAsync();

                    string movie = null, room = null;
                    DateTime? showDate = null, startTime = null;

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

                    History.Add(new InvoiceHistoryVM
                    {
                        InvoiceId = inv.InvoiceId,
                        TotalPrice = inv.TotalPrice ?? 0,
                        Status = inv.Status ?? 0,
                        CreatedAt = inv.CreatedAt,
                        MovieTitle = movie,
                        Room = room,
                        ShowDate = showDate,
                        StartTime = startTime
                    });
                }
            }
            else
            {
                AvatarUrl = null;
                CurrentPoint = 0;
                IsGlFriend = false;
            }

        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            await LoadAsync(user);
            return Page();
        }

        // ================== UPDATE PROFILE ==================
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

            // Cập nhật FullName và PhoneNumber vào AppUser
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

            // Cập nhật customer (Users table) với tất cả các trường
            var customer = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

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

        // ================== UPLOAD AVATAR ==================
        public async Task<IActionResult> OnPostAvatarAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var email = await _userManager.GetEmailAsync(user);
            var customer = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (customer == null) return NotFound("Customer not found.");
            if (AvatarFile == null || AvatarFile.Length == 0) return RedirectToPage();

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

        // ================== POPUP LOAD VÉ ==================
        public async Task<IActionResult> OnGetTicketDetailModalAsync(string invoiceId)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                return Content("Không có mã vé.", "text/html");

            var invoice = await _context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null)
                return Content("Hóa đơn không tồn tại.", "text/html");

            var tickets = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.InvoiceId == invoiceId)
                .ToListAsync();

            if (!tickets.Any())
                return Content("Không có vé.", "text/html");

            var firstTicket = tickets.First();

            var st = await _context.ShowTimes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowTimeId == firstTicket.ShowTimeId);

            if (st == null)
                return Content("Không tìm thấy suất chiếu.", "text/html");

            var mv = await _context.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MoviesId == st.MoviesId);

            // Phòng (CinemaTheaters) + Rạp (MovieTheaters)
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
                .Where(d => d.InvoiceId == invoiceId)
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

            // Text hiển thị
            string movieTitle = mv?.Title ?? "N/A";
            string timeText = st.StartTime?.ToString("HH:mm") ?? "";
            string dateText = st.ShowDate?.ToString("dd/MM/yyyy") ?? "";
            string roomLabel = room?.Name ?? room?.CinemaTheaterId ?? "N/A";   // Phòng
            string cinemaName = theater?.Name ?? "N/A";                         // Rạp (tên cụm rạp)
            string totalText = (invoice.TotalPrice ?? 0m).ToString("#,0");

            // HTML popup
            string html = $@"
        <div style='border-radius:16px; padding:24px 28px;
                    color:#f9fafb; font-size:15px;'>

            <!-- TÊN PHIM -->
            <div style='font-size:14px; font-weight:800; text-transform:uppercase; color:#fde047;'>TÊN PHIM</div>
            <div style='font-size:22px; font-weight:900; margin-bottom:20px;'>{movieTitle}</div>

            <!-- GRID THÔNG TIN 2 CỘT -->
            <div style='display:grid; grid-template-columns:1fr 1fr; gap:22px;'>

                <!-- CỘT TRÁI -->
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

                <!-- CỘT PHẢI -->
                <div>
                    <div style='margin-bottom:14px;'>
                        <div style='font-weight:700; color:#fde047;'>Thời gian</div>
                        <div>{timeText} {dateText}</div>
                    </div>

                    <div style='margin-bottom:14px;'>
                        <div style='font-weight:700; color:#fde047;'>Số vé</div>
                        <div>{tickets.Count}</div>
                    </div>

                    <!-- SNACK NGANG VỚI SỐ GHẾ -->
                    <div>
                        <div style='font-weight:700; color:#fde047;'>Bắp nước</div>
                        <div>{snackLinesHtml}</div>
                    </div>
                </div>
            </div>

            <!-- RẠP -->
            <div style='margin-top:22px;'>
                <div style='font-weight:700; color:#fde047;'>Rạp</div>
                <div style='font-size:16px; font-weight:700;'>{cinemaName}</div>
                <div style='font-size:13px; opacity:.9;'>{theater?.Address ?? ""}</div>
            </div>

            <!-- TOTAL -->
            <hr style='margin:20px 0 14px; border:none; border-top:1px dashed rgba(255,255,255,.4);' />

            <div style='display:flex; justify-content:space-between; align-items:center; margin-bottom:14px;'>
                <span style='font-size:18px; font-weight:800; color:#fde047;'>TỔNG TIỀN</span>
                <span style='font-size:22px; font-weight:900;'>{totalText} VNĐ</span>
            </div>

            <button style='margin-top:6px; width:100%; padding:12px;
                           background:#facc15; color:#111827; font-weight:800;
                           border-radius:10px; border:none; text-transform:uppercase;
                           letter-spacing:.06em;'>
                TẢI VÉ VỀ MÁY
            </button>
        </div>";

            return Content(html, "text/html");
        }
        


    }
}
