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

        private static readonly TimeSpan RoleOtpLifetime = TimeSpan.FromMinutes(5);   // OTP sống 5p (gửi/nhập)
        private static readonly TimeSpan RoleChangeWindow = TimeSpan.FromMinutes(3);  // cửa sổ đổi quyền 3p
        private static readonly TimeSpan RoleOtpCooldown = TimeSpan.FromSeconds(30);  // chống spam gửi lại

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
            const int pageSize = 8;

            // normalize input
            search = (search ?? "").Trim();
            role = (role ?? "").Trim();

            var query = _userManager.Users.AsQueryable();

            // search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    (u.FullName ?? "").Contains(search) ||
                    (u.Email ?? "").Contains(search) ||
                    (u.UserName ?? "").Contains(search));
            }

            // role filter (Admin/User)
            if (!string.IsNullOrWhiteSpace(role))
            {
                // chỉ cho phép 2 giá trị này để tránh nhập bậy
                var roleNormalized = role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "Admin"
                                   : role.Equals("User", StringComparison.OrdinalIgnoreCase) ? "User"
                                   : "";

                if (!string.IsNullOrWhiteSpace(roleNormalized))
                {
                    // lấy danh sách user thuộc role -> lọc theo Id
                    var usersInRole = await _userManager.GetUsersInRoleAsync(roleNormalized);
                    var roleUserIds = usersInRole.Select(u => u.Id).ToList();

                    // nếu role không có ai -> trả rỗng luôn
                    if (roleUserIds.Count == 0)
                    {
                        ViewBag.CurrentPage = 1;
                        ViewBag.TotalPages = 1;
                        ViewBag.Search = search;
                        ViewBag.Role = roleNormalized;
                        return View(new List<AdminUserListItemVm>());
                    }

                    query = query.Where(u => roleUserIds.Contains(u.Id));
                    role = roleNormalized;
                }
                else
                {
                    // role không hợp lệ -> coi như không lọc
                    role = "";
                }
            }

            // chống page vượt
            if (page < 1) page = 1;

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
                var roles = await _userManager.GetRolesAsync(user);

                vms.Add(new AdminUserListItemVm
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnabled = user.LockoutEnabled,
                    Roles = roles
                });
            }

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Search = search;
            ViewBag.Role = role ?? "";

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
                AvatarPath = profile?.AvatarUrl
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
                return View("Create", vm);
            }

            var selectedRole = vm.SelectedRoles?.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(selectedRole))
            {
                ModelState.AddModelError("SelectedRoles", "Vui lòng chọn 1 quyền.");
                ViewData["Title"] = "Thêm tài khoản";
                return View("Create", vm);
            }

            var rr = await SetSingleRoleAsync(user, selectedRole);
            if (!rr.Succeeded)
            {
                foreach (var e in rr.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                ViewData["Title"] = "Thêm tài khoản";
                return View("Create", vm);
            }


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
                    .ToListAsync()
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
            user.Email = vm.Email;
            user.UserName = vm.Email;
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

            var currentRoles = await _userManager.GetRolesAsync(user);
            var selectedRole = vm.SelectedRoles?.FirstOrDefault(); // chỉ lấy 1

            // bắt buộc phải chọn role
            if (string.IsNullOrWhiteSpace(selectedRole))
            {
                ModelState.AddModelError("SelectedRoles", "Vui lòng chọn 1 quyền.");
                ViewData["Title"] = "Chỉnh sửa tài khoản";
                return View(vm);
            }

            var isRoleChanging = currentRoles.Count != 1 || !string.Equals(currentRoles[0], selectedRole, StringComparison.OrdinalIgnoreCase);

            if (isRoleChanging)
            {
                var untilStr = HttpContext.Session.GetString(RoleOtpVerifiedUntilKey);
                if (!long.TryParse(untilStr, out var untilTicks))
                {
                    ModelState.AddModelError(string.Empty, "Cần xác minh Gmail (OTP) trước khi đổi quyền.");
                    ViewData["Title"] = "Chỉnh sửa tài khoản";
                    return View(vm);
                }

                var until = new DateTimeOffset(untilTicks, TimeSpan.Zero);
                if (DateTimeOffset.UtcNow > until)
                {
                    HttpContext.Session.Remove(RoleOtpVerifiedUntilKey);
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
        public async Task<IActionResult> SendRoleOtp()
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

            var code = new Random().Next(100000, 999999).ToString();

            session.SetString(RoleOtpCodeKey, code);
            session.SetString(RoleOtpExpireKey, now.Add(RoleOtpLifetime).UtcTicks.ToString());
            session.SetString(RoleOtpLastSentKey, now.UtcTicks.ToString());

            // reset cửa sổ đổi quyền
            session.Remove(RoleOtpVerifiedUntilKey);

            var subject = "OTP xác minh đổi quyền CinemaS";
            var body = $@"
<p>Mã OTP xác minh đổi quyền của bạn là: <strong>{code}</strong></p>
<p>Mã có hiệu lực trong {RoleOtpLifetime.TotalMinutes:N0} phút.</p>";

            try
            {
                await _emailSender.SendEmailAsync(admin.Email, subject, body);
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
                message = $"Đã gửi OTP tới {admin.Email}.",
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

            if (string.IsNullOrWhiteSpace(storedCode) || string.IsNullOrWhiteSpace(expireStr))
                return Json(new { ok = false, message = "Chưa gửi OTP hoặc OTP đã hết hạn." });

            if (!long.TryParse(expireStr, out var expireTicks))
                return Json(new { ok = false, message = "OTP không hợp lệ." });

            var expireAt = new DateTimeOffset(expireTicks, TimeSpan.Zero);
            if (now > expireAt)
                return Json(new { ok = false, message = "OTP đã hết hạn." });

            if (!string.Equals(storedCode, (otp ?? "").Trim(), StringComparison.Ordinal))
                return Json(new { ok = false, message = "OTP không đúng." });

            // OTP đúng -> mở cửa sổ đổi quyền 3 phút
            var verifiedUntil = now.Add(RoleChangeWindow).UtcTicks.ToString();
            session.SetString(RoleOtpVerifiedUntilKey, verifiedUntil);

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

    }
}
