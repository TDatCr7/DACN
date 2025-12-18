using CinemaS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaS.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterPasswordModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUserStore<AppUser> _userStore;
        private readonly IUserEmailStore<AppUser> _emailStore;
        private readonly ILogger<RegisterPasswordModel> _logger;
        private readonly RoleManager<IdentityRole> _roleManager;   // thêm
        private readonly CinemaContext _context;

        // các key session giống bên Register.cshtml.cs
        private const string SessionOtpCodeKey = "RegisterOtp_Code";
        private const string SessionOtpEmailKey = "RegisterOtp_Email";
        private const string SessionOtpExpireKey = "RegisterOtp_ExpireAt";
        private const string SessionOtpLastSentKey = "RegisterOtp_LastSent";
        private const string SessionOtpVerifiedKey = "RegisterOtp_Verified";
        private const string SessionOtpFullNameKey = "RegisterOtp_FullName";

        public RegisterPasswordModel(
            UserManager<AppUser> userManager,
            IUserStore<AppUser> userStore,
            SignInManager<AppUser> signInManager,
            RoleManager<IdentityRole> roleManager,    // thêm
            CinemaContext context,
            ILogger<RegisterPasswordModel> logger)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = (IUserEmailStore<AppUser>)_userStore;
            _signInManager = signInManager;
            _logger = logger;
            _roleManager = roleManager;              // thêm
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100)]
            public string FullName { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [StringLength(100, ErrorMessage = "Mật khẩu tối thiểu {2} và tối đa {1} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }
        }

        public IActionResult OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            var verified = HttpContext.Session.GetString(SessionOtpVerifiedKey);
            var email = HttpContext.Session.GetString(SessionOtpEmailKey);
            var fullName = HttpContext.Session.GetString(SessionOtpFullNameKey);

            if (verified != "true" || string.IsNullOrEmpty(email))
            {
                return RedirectToPage("Register");
            }

            Input = new InputModel
            {
                Email = email,
                FullName = fullName ?? string.Empty
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");

            var verified = HttpContext.Session.GetString(SessionOtpVerifiedKey);
            var emailSession = HttpContext.Session.GetString(SessionOtpEmailKey);
            var fullNameSession = HttpContext.Session.GetString(SessionOtpFullNameKey);

            if (verified != "true" || string.IsNullOrEmpty(emailSession))
            {
                return RedirectToPage("Register");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (!string.Equals(emailSession, Input.Email, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Input.Email", "Email không khớp với email đã xác thực OTP.");
                return Page();
            }

            var user = CreateUser();
            user.FullName = Input.FullName;
            user.EmailConfirmed = true; // coi OTP là bước xác thực email

            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password (via OTP).");
                const string defaultRoleName = "User";

                if (!await _roleManager.RoleExistsAsync(defaultRoleName))
                {
                    ModelState.AddModelError(string.Empty, "Role mặc định 'User' không tồn tại.");
                    return Page();
                }

                var roleResult = await SetSingleRoleAsync(user, defaultRoleName);
                if (!roleResult.Succeeded)
                {
                    foreach (var e in roleResult.Errors)
                        ModelState.AddModelError(string.Empty, e.Description);

                    return Page();
                }

                //    Users đã được tạo tự động trong CinemaContext.SaveChangesAsync khi AppUser được thêm.
                if (!string.IsNullOrEmpty(user.Email))
                {
                    var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
                    if (dbUser != null)
                    {
                        // xoá toàn bộ UserRole cũ (nếu có)
                        var oldUserRoles = _context.UserRoles.Where(ur => ur.UserId == dbUser.UserId);
                        _context.UserRoles.RemoveRange(oldUserRoles);

                        // tìm role domain tương ứng tên "User"
                        var dbRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == defaultRoleName);
                        if (dbRole != null)
                        {
                            _context.UserRoles.Add(new UserRole
                            {
                                UserId = dbUser.UserId,
                                RoleId = dbRole.RoleId
                                // các field khác (IsActive, CreatedAt, ...) nếu có sẽ dùng giá trị mặc định
                            });
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                // Xóa toàn bộ session OTP
                HttpContext.Session.Remove(SessionOtpCodeKey);
                HttpContext.Session.Remove(SessionOtpEmailKey);
                HttpContext.Session.Remove(SessionOtpExpireKey);
                HttpContext.Session.Remove(SessionOtpLastSentKey);
                HttpContext.Session.Remove(SessionOtpVerifiedKey);
                HttpContext.Session.Remove(SessionOtpFullNameKey);

                // Đăng nhập luôn
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Dùng lại trang RegisterConfirmation để báo thành công
                return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = ReturnUrl });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        private AppUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<AppUser>();
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Can't create an instance of '{nameof(AppUser)}'. " +
                    $"Ensure that '{nameof(AppUser)}' is not an abstract class and has a parameterless constructor.");
            }
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
