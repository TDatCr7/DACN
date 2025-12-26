using CinemaS.Models;
using CinemaS.ViewModels.AdminUsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly CinemaContext _context;
        private readonly IEmailSender _emailSender;

        private const string RoleOtpCodeKey = "AdminRoleOtp_Code";
        private const string RoleOtpExpireKey = "AdminRoleOtp_ExpireAtUtcTicks";
        private const string RoleOtpVerifiedUntilKey = "AdminRoleOtp_VerifiedUntilUtcTicks";
        private const string RoleOtpLastSentKey = "AdminRoleOtp_LastSentUtcTicks";
        // thêm: target user id/email và verified-for-user id
        private const string RoleOtpTargetUserIdKey = "AdminRoleOtp_TargetUserId";
        private const string RoleOtpTargetEmailKey = "AdminRoleOtp_TargetEmail";
        private const string RoleOtpVerifiedUserIdKey = "AdminRoleOtp_VerifiedForUserId";

        private static readonly TimeSpan RoleOtpLifetime = TimeSpan.FromMinutes(5);   // OTP sống 5p (gửi/nhập)
        private static readonly TimeSpan RoleChangeWindow = TimeSpan.FromMinutes(3);  // cửa sổ đổi quyền 3p
        private static readonly TimeSpan RoleOtpCooldown = TimeSpan.FromSeconds(30);  // chống spam gửi lại
        private const int pageSize = 8;
        public AdminUsersController(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        CinemaContext context,
        IEmailSender emailSender)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _emailSender = emailSender;
        }


        public async Task<IActionResult> Index(string? search, string? role, int page = 1)
        {
            if (page < 1) page = 1;

            var query = _userManager.Users.AsQueryable();

            // search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    (u.FullName ?? "").Contains(search) ||
                    (u.Email ?? "").Contains(search) ||
                    (u.UserName ?? "").Contains(search));
            }

            // ✅ role filter (lọc theo quyền)
            if (!string.IsNullOrWhiteSpace(role))
            {
                var userIdsInRole = await (from ur in _context.UserRoles
                                           join r in _context.Roles on ur.RoleId equals r.RoleId
                                           where r.Name == role
                                           select ur.UserId)
                                         .Distinct()
                                         .ToListAsync();

                query = query.Where(u => userIdsInRole.Contains(u.Id));
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var users = await query
                .OrderBy(u => u.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vms = new List<AdminUserListItemVm>();

            foreach (var user in users)
            {
                var rolesOfUser = await _userManager.GetRolesAsync(user);

                vms.Add(new AdminUserListItemVm
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnabled = user.LockoutEnabled,
                    Roles = rolesOfUser
                });
            }

            // ✅ ViewBag cho Index.cshtml
            ViewBag.Role = role ?? "";
            ViewBag.Search = search ?? "";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            ViewData["Title"] = "Quản lý tài khoản";
            return View(vms);
        }

        // GET: /AdminUsers/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            // đọc profile từ bảng Users (bảng riêng)
            Users? profile = null;
            if (!string.IsNullOrEmpty(user.Email))
            {
                profile = await _context.Set<Users>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == user.Email);
            }

            // tính tuổi từ ngày sinh (nếu có)
            int? ageFromBirth = null;
            var dob = profile?.DateOfBirth;

            if (dob.HasValue)
            {
                var today = DateTime.Today;
                var d = dob.Value.Date;
                var age = today.Year - d.Year;
                if (d > today.AddYears(-age)) age--;
                ageFromBirth = age;
            }

            int? ageField = null;
            if (!string.IsNullOrWhiteSpace(user.Age) &&
                int.TryParse(user.Age, out var parsedAge))
            {
                ageField = parsedAge;
            }

            var vm = new AdminUserEditVm
            {
                Id = user.Id,
                FullName = user.FullName,
                Address = user.Address,
                Age = ageField,
                Email = user.Email,
                UserName = profile?.UserId ?? user.UserName,
                PhoneNumber = user.PhoneNumber,
                EmailConfirmed = user.EmailConfirmed,
                SelectedRoles = roles.ToList(),

                DateOfBirth = profile?.DateOfBirth,
                Gender = profile?.Gender,
                SavePoint = profile?.SavePoint ?? 0,
                AgeFromBirth = ageFromBirth,
                AvatarPath = profile?.AvatarUrl,

                // thêm MembershipRankId và RankName
                MembershipRankId = profile?.MembershipRankId,
                RankName = profile != null
                    ? (await _context.MembershipRanks.AsNoTracking()
                        .FirstOrDefaultAsync(r => r.MembershipRankId == profile.MembershipRankId))
                        ?.Name ?? "Chưa xác định"
                    : "Chưa xác định"
            };

            ViewData["Title"] = "Thông tin tài khoản";
            return View(vm);
        }

        // GET: /AdminUsers/Create
        public async Task<IActionResult> Create()
        {
            var vm = new AdminUserEditVm
            {
                AllRoles = await _roleManager.Roles
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name!)
                    .ToListAsync()
            };

            ViewData["Title"] = "Thêm tài khoản";
            return View("Create", vm);
        }

        // POST: /AdminUsers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminUserEditVm vm)
        {
            vm.AllRoles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Thêm tài khoản";
                return View("Create", vm);
            }

            var user = new AppUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                FullName = vm.FullName,
                Address = vm.Address,
                PhoneNumber = vm.PhoneNumber,
                EmailConfirmed = true,
                Age = vm.Age?.ToString()
            };

            var password = string.IsNullOrWhiteSpace(vm.Password)
                ? "CinemaS@123"
                : vm.Password;

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                ViewData["Title"] = "Thêm tài khoản";
                return View("Edit", vm);
            }

            var selectedRole = vm.SelectedRoles?.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(selectedRole))
            {
                ModelState.AddModelError("SelectedRoles", "Vui lòng chọn 1 quyền.");
                ViewData["Title"] = "Thêm tài khoản";
                return View("Create", vm);
            }

            if (vm.SelectedRoles != null && vm.SelectedRoles.Any())
                await _userManager.AddToRolesAsync(user, vm.SelectedRoles);

            return RedirectToAction(nameof(Index));
        }

        // GET: /AdminUsers/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            int? age = null;
            if (!string.IsNullOrWhiteSpace(user.Age) &&
                int.TryParse(user.Age, out var parsedAge))
            {
                age = parsedAge;
            }

            // ✅ Lấy Users profile từ bảng Users
            Users? profile = null;
            if (!string.IsNullOrEmpty(user.Email))
            {
                profile = await _context.Set<Users>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == user.Email);
            }

            // ✅ Lấy thông tin Rank
            string? membershipRankId = null;
            string? rankName = null;

            if (profile != null)
            {
                membershipRankId = profile.MembershipRankId;

                // Load tên hạng từ MembershipRanks
                var rank = await _context.MembershipRanks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.MembershipRankId == membershipRankId);

                if (rank != null)
                {
                    rankName = rank.Name ?? "Chưa xác định";
                }
                else
                {
                    // Nếu MembershipRankId null hoặc không tìm thấy, fallback: xác định rank theo SavePoint
                    var sp = profile.SavePoint ?? 0;
                    var fallbackRank = await _context.MembershipRanks
                        .AsNoTracking()
                        .Where(r => (r.RequirePoint ?? 0) <= sp && (r.MaxPoint == null || sp <= r.MaxPoint))
                        .OrderByDescending(r => (r.RequirePoint ?? 0))
                        .FirstOrDefaultAsync();

                    if (fallbackRank != null)
                    {
                        membershipRankId = fallbackRank.MembershipRankId;
                        rankName = fallbackRank.Name ?? "Chưa xác định";
                    }
                    else
                    {
                        rankName = "Chưa xác định";
                    }
                }
            }

            var vm = new AdminUserEditVm
            {
                Id = user.Id,
                FullName = user.FullName,
                Address = user.Address,
                Age = age,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                EmailConfirmed = user.EmailConfirmed,
                SelectedRoles = roles.ToList(),
                AllRoles = await _roleManager.Roles
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name!)
                    .ToListAsync(),
                // ✅ Gán thông tin Rank
                MembershipRankId = membershipRankId,
                RankName = rankName ?? "Chưa xác định",
                SavePoint = profile?.SavePoint ?? 0
            };

            ViewData["Title"] = "Chỉnh sửa tài khoản";
            return View(vm);
        }

        // POST: /AdminUsers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AdminUserEditVm vm)
        {
            if (id != vm.Id)
                return NotFound();

            // ✅ THÊM: Validation để giới hạn SavePoint max = 99,999
            if (vm.SavePoint > 99999)
            {
                vm.SavePoint = 99999;
            }
            if (vm.SavePoint < 0)
            {
                vm.SavePoint = 0;
            }

            vm.AllRoles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Chỉnh sửa tài khoản";
                return View(vm);
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            user.FullName = vm.FullName;
            user.Address = vm.Address;
            // only update email/username if provided to avoid setting to null
            if (!string.IsNullOrWhiteSpace(vm.Email))
            {
                user.Email = vm.Email;
                user.UserName = vm.Email;
            }
            user.PhoneNumber = vm.PhoneNumber;
            user.Age = vm.Age?.ToString();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                ViewData["Title"] = "Chỉnh sửa tài khoản";
                return View(vm);
            }

            // ✅ THÊM: Cập nhật SavePoint và tự động tính MembershipRankId dựa trên điểm mới
            if (!string.IsNullOrEmpty(user.Email))
            {
                var profile = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == user.Email);

                if (profile != null)
                {
                    // Cập nhật SavePoint (đã được validate ở trên)
                    profile.SavePoint = vm.SavePoint;
                    profile.UpdatedAt = DateTime.UtcNow;

                    // Tìm hạng phù hợp dựa trên điểm mới
                    var newRank = await _context.MembershipRanks
                        .AsNoTracking()
                        .Where(r => (r.RequirePoint ?? 0) <= vm.SavePoint && (r.MaxPoint == null || vm.SavePoint <= r.MaxPoint))
                        .OrderByDescending(r => (r.RequirePoint ?? 0))
                        .FirstOrDefaultAsync();

                    if (newRank != null)
                    {
                        profile.MembershipRankId = newRank.MembershipRankId;
                    }

                    await _context.SaveChangesAsync();
                }
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var selectedRole = vm.SelectedRoles?.FirstOrDefault();

            // ✅ THÊM: Kiểm tra xem admin có chọn role hay không
            bool adminExplicitlySelected = !string.IsNullOrWhiteSpace(selectedRole);

            // ✅ THÊM: Nếu không chọn role, mặc định gán "User"
            if (string.IsNullOrWhiteSpace(selectedRole))
            {
                selectedRole = "User";
            }

            // ✅ THAY ĐỔI: Chỉ yêu cầu OTP nếu admin CHỦ ĐỘNG chọn role (không phải mặc định)
            var isRoleChanging = adminExplicitlySelected && (currentRoles.Count != 1 || !string.Equals(currentRoles.FirstOrDefault() ?? "", selectedRole ?? "", StringComparison.OrdinalIgnoreCase));

            if (isRoleChanging)
            {
                var untilStr = HttpContext.Session.GetString(RoleOtpVerifiedUntilKey);
                var verifiedFor = HttpContext.Session.GetString(RoleOtpVerifiedUserIdKey);
                if (!long.TryParse(untilStr, out var untilTicks) || string.IsNullOrWhiteSpace(verifiedFor) || !string.Equals(verifiedFor, id, StringComparison.Ordinal))
                {
                    ModelState.AddModelError(string.Empty, "Cần xác minh Gmail (OTP) cho tài khoản này trước khi đổi quyền.");
                    ViewData["Title"] = "Chỉnh sửa tài khoản";
                    return View(vm);
                }

                var until = new DateTimeOffset(untilTicks, TimeSpan.Zero);
                if (DateTimeOffset.UtcNow > until)
                {
                    HttpContext.Session.Remove(RoleOtpVerifiedUntilKey);
                    HttpContext.Session.Remove(RoleOtpVerifiedUserIdKey);
                    ModelState.AddModelError(string.Empty, "Hết thời gian đổi quyền (3 phút). Vui lòng xác minh Gmail lại.");
                    ViewData["Title"] = "Chỉnh sửa tài khoản";
                    return View(vm);
                }

                var rr = await SetSingleRoleAsync(user, selectedRole);
                if (!rr.Succeeded)
                {
                    foreach (var e in rr.Errors)
                        ModelState.AddModelError(string.Empty, e.Description);

                    ViewData["Title"] = "Chỉnh sửa tài khoản";
                    return View(vm);
                }

                HttpContext.Session.Remove(RoleOtpVerifiedUntilKey);
                HttpContext.Session.Remove(RoleOtpVerifiedUserIdKey);
            }
            else if (!adminExplicitlySelected && currentRoles.FirstOrDefault() != selectedRole)
            {
                // ✅ THÊM: Nếu admin để trống (mặc định "User"), cập nhật role mà không cần OTP
                var rr = await SetSingleRoleAsync(user, selectedRole);
                if (!rr.Succeeded)
                {
                    foreach (var e in rr.Errors)
                        ModelState.AddModelError(string.Empty, e.Description);

                    ViewData["Title"] = "Chỉnh sửa tài khoản";
                    return View(vm);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /AdminUsers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            Users? profile = null;
            if (!string.IsNullOrEmpty(user.Email))
            {
                profile = await _context.Set<Users>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == user.Email);
            }

            if (profile != null)
            {
                var hasInvoices = await _context.Set<Invoices>()
                    .AsNoTracking()
                    .AnyAsync(i => i.CustomerId == profile.UserId);

                if (hasInvoices)
                {
                    TempData["Error"] = "Không thể xoá tài khoản vì tài khoản đã có hoá đơn.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            await _userManager.DeleteAsync(user);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRoleOtp(string? targetUserId)
        {
            // admin đang thao tác
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null || string.IsNullOrWhiteSpace(admin.Email))
                return Json(new { ok = false, message = "Không có email admin để gửi OTP." });

            var session = HttpContext.Session;
            var now = DateTimeOffset.UtcNow;

            // cooldown 30s
            var lastSentStr = session.GetString(RoleOtpLastSentKey);
            if (long.TryParse(lastSentStr, out var lastSentTicks))
            {
                var lastSent = new DateTimeOffset(lastSentTicks, TimeSpan.Zero);
                var diff = now - lastSent;
                if (diff < RoleOtpCooldown)
                {
                    var remain = (int)Math.Ceiling((RoleOtpCooldown - diff).TotalSeconds);
                    return Json(new { ok = false, message = "Vui lòng chờ trước khi gửi lại OTP.", cooldown = remain });
                }
            }

            // quyết định gửi cho ai: nếu có targetUserId thì gửi cho email của tài khoản đó, còn không thì gửi cho admin
            string sendToEmail = admin.Email;
            string targetToStore = string.Empty;
            if (!string.IsNullOrWhiteSpace(targetUserId))
            {
                var targetUser = await _userManager.FindByIdAsync(targetUserId);
                if (targetUser != null && !string.IsNullOrWhiteSpace(targetUser.Email))
                {
                    sendToEmail = targetUser.Email;
                    targetToStore = targetUserId;
                }
            }

            var code = new Random().Next(100000, 999999).ToString();

            session.SetString(RoleOtpCodeKey, code);
            session.SetString(RoleOtpExpireKey, now.Add(RoleOtpLifetime).UtcTicks.ToString());
            session.SetString(RoleOtpLastSentKey, now.UtcTicks.ToString());

            // lưu target thông tin
            session.SetString(RoleOtpTargetUserIdKey, targetToStore ?? string.Empty);
            session.SetString(RoleOtpTargetEmailKey, sendToEmail ?? string.Empty);

            // reset cửa sổ đổi quyền
            session.Remove(RoleOtpVerifiedUntilKey);
            session.Remove(RoleOtpVerifiedUserIdKey);

            var subject = "OTP xác minh đổi quyền CinemaS";
            var body = $@"
<p>Mã OTP xác minh đổi quyền của bạn là: <strong>{code}</strong></p>
<p>Mã có hiệu lực trong {RoleOtpLifetime.TotalMinutes:N0} phút.</p>";

            try
            {
                await _emailSender.SendEmailAsync(sendToEmail, subject, body);
            }
            catch
            {
                return Json(new
                {
                    ok = false,
                    message = "Không gửi được email OTP. Kiểm tra cấu hình SMTP/Gmail và App Password."
                });
            }

            return Json(new
            {
                ok = true,
                message = $"Đã gửi OTP tới {sendToEmail}.",
                otpLifetimeSeconds = (int)RoleOtpLifetime.TotalSeconds
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyRoleOtp([FromForm] string otp)
        {
            var session = HttpContext.Session;
            var now = DateTimeOffset.UtcNow;

            var storedCode = session.GetString(RoleOtpCodeKey);
            var expireStr = session.GetString(RoleOtpExpireKey);
            var targetUserId = session.GetString(RoleOtpTargetUserIdKey) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(storedCode) || string.IsNullOrWhiteSpace(expireStr))
                return Json(new { ok = false, message = "Chưa gửi OTP hoặc OTP đã hết hạn." });

            if (!long.TryParse(expireStr, out var expireTicks))
                return Json(new { ok = false, message = "OTP không hợp lệ." });

            var expireAt = new DateTimeOffset(expireTicks, TimeSpan.Zero);
            if (now > expireAt)
                return Json(new { ok = false, message = "OTP đã hết hạn." });

            if (!string.Equals(storedCode, (otp ?? "").Trim(), StringComparison.Ordinal))
                return Json(new { ok = false, message = "OTP không đúng." });

            // OTP đúng -> mở cửa sổ đổi quyền 3 phút và ghi nhận tài khoản được verify
            var verifiedUntil = now.Add(RoleChangeWindow).UtcTicks.ToString();
            session.SetString(RoleOtpVerifiedUntilKey, verifiedUntil);
            session.SetString(RoleOtpVerifiedUserIdKey, targetUserId ?? string.Empty);

            return Json(new
            {
                ok = true,
                message = "Xác minh thành công. Có thể đổi quyền trong 3 phút.",
                roleWindowSeconds = (int)RoleChangeWindow.TotalSeconds
            });
        }
        private async Task<IdentityResult> SetSingleRoleAsync(AppUser user, string role)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);

            if (currentRoles.Count > 0)
            {
                var rm = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!rm.Succeeded) return rm;
            }

            if (string.IsNullOrWhiteSpace(role))
                return IdentityResult.Success;

            return await _userManager.AddToRoleAsync(user, role);
        }

        // ✅ THÊM MỚI: API để load danh sách các hạng thành viên có sẵn
        [HttpPost("LoadAvailableRanks")]
        [Authorize(Roles = "Admin")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> LoadAvailableRanks(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Json(new { success = false, message = "Thiếu User ID." });

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy người dùng." });

            // Lấy danh sách tất cả các hạng
            var ranks = await _context.MembershipRanks
                .AsNoTracking()
                .OrderBy(r => (r.RequirePoint ?? 0))
                .Select(r => new
                {
                    id = r.MembershipRankId,
                    name = r.Name ?? "Chưa xác định",
                    minPoints = (r.RequirePoint ?? 0),
                    maxPoints = (r.MaxPoint ?? 0),
                    description = r.Description
                })
                .ToListAsync();

            return Json(new { success = true, ranks = ranks });
        }

        // ✅ THÊM MỚI: API để admin cập nhật Membership_Rank_ID của user
        [HttpPost("UpdateUserRank")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRank(string userId, string rankId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(rankId))
                return Json(new { ok = false, message = "Thiếu User ID hoặc Rank ID." });

            // Kiểm tra rank tồn tại
            var rankExists = await _context.MembershipRanks
                .AsNoTracking()
                .AnyAsync(r => r.MembershipRankId == rankId);

            if (!rankExists)
                return Json(new { ok = false, message = "Hạng không tồn tại." });

            // Lấy user từ bảng Users
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return Json(new { ok = false, message = "Không tìm thấy người dùng." });

            var oldRankId = user.MembershipRankId;

            // Cập nhật rank
            user.MembershipRankId = rankId;
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();

                // Lấy tên rank mới
                var newRank = await _context.MembershipRanks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.MembershipRankId == rankId);

                var rankName = newRank?.Name ?? "Chưa xác định";

                return Json(new
                {
                    ok = true,
                    message = $"Đã cập nhật hạng thành viên thành {rankName}",
                    rankId = rankId,
                    rankName = rankName,
                    savePoint = user.SavePoint ?? 0
                });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Lỗi cập nhật: " + ex.Message });
            }
        }

        // ✅ THÊM MỚI: API để admin cập nhật Membership_Rank_ID dựa trên điểm hiện tại
        [HttpPost("RecalculateUserRank")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculateUserRank(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Json(new { ok = false, message = "Thiếu User ID." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return Json(new { ok = false, message = "Không tìm thấy người dùng." });

            var totalPoints = user.SavePoint ?? 0;

            // Tìm hạng phù hợp dựa trên điểm
            var newRank = await _context.MembershipRanks
                .AsNoTracking()
                .Where(r => (r.RequirePoint ?? 0) <= totalPoints && (r.MaxPoint == null || totalPoints <= r.MaxPoint))
                .OrderByDescending(r => (r.RequirePoint ?? 0))
                .FirstOrDefaultAsync();

            if (newRank == null)
                return Json(new { ok = false, message = "Không tìm thấy hạng phù hợp với điểm hiện tại." });

            var oldRankId = user.MembershipRankId;

            if (string.Equals(oldRankId, newRank.MembershipRankId, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    ok = true,
                    message = $"Hạng hiện tại đã đúng: {newRank.Name}",
                    rankId = newRank.MembershipRankId,
                    rankName = newRank.Name,
                    totalPoints = totalPoints
                });
            }

            // Cập nhật rank
            user.MembershipRankId = newRank.MembershipRankId;
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();

                return Json(new
                {
                    ok = true,
                    message = $"Đã cập nhật hạng từ {oldRankId} thành {newRank.MembershipRankId} ({newRank.Name})",
                    rankId = newRank.MembershipRankId,
                    rankName = newRank.Name,
                    totalPoints = totalPoints
                });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Lỗi cập nhật: " + ex.Message });
            }
        }
    }
}
