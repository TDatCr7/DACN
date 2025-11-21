// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using CinemaS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace CinemaS.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUserStore<AppUser> _userStore;
        private readonly IUserEmailStore<AppUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        private const string SessionOtpCodeKey = "RegisterOtp_Code";
        private const string SessionOtpEmailKey = "RegisterOtp_Email";
        private const string SessionOtpExpireKey = "RegisterOtp_ExpireAt";
        private const string SessionOtpLastSentKey = "RegisterOtp_LastSent";

        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan OtpCooldown = TimeSpan.FromSeconds(30);

        public RegisterModel(
            UserManager<AppUser> userManager,
            IUserStore<AppUser> userStore,
            SignInManager<AppUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        /// <summary>Thời gian còn lại (giây) trước khi được gửi lại mã OTP.</summary>
        public int OtpCountdownSeconds { get; set; }

        /// <summary>Thông báo trạng thái OTP hiển thị dưới ô nhập mã.</summary>
        public string OtpStatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập email.")]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [StringLength(100, ErrorMessage = "Mật khẩu tối thiểu {2} và tối đa {1} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
            [StringLength(100)]
            public string FullName { get; set; } = default!;

            [StringLength(300)]
            public string Address { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mã xác nhận đã gửi tới email.")]
            [Display(Name = "Mã xác nhận")]
            public string OtpCode { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ComputeOtpCountdown();
        }

        /// <summary>
        /// Xử lý nút "Gửi mã" – chỉ kiểm tra email, không bắt nhập các trường khác.
        /// </summary>
        public async Task<IActionResult> OnPostSendOtpAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            // Chỉ quan tâm tới email, bỏ qua validation các field khác để không báo lỗi mật khẩu.
            ModelState.Clear();

            if (Input == null || string.IsNullOrWhiteSpace(Input.Email))
            {
                ModelState.AddModelError("Input.Email", "Vui lòng nhập email để gửi mã xác nhận.");
                ComputeOtpCountdown();
                return Page();
            }

            // Cooldown 30 giây chống spam
            var now = DateTimeOffset.UtcNow;
            var lastSentTicks = HttpContext.Session.GetString(SessionOtpLastSentKey);
            if (long.TryParse(lastSentTicks, out var ticks))
            {
                var lastSent = new DateTimeOffset(ticks, TimeSpan.Zero);
                var diff = now - lastSent;
                if (diff < OtpCooldown)
                {
                    var remain = (int)Math.Ceiling((OtpCooldown - diff).TotalSeconds);
                    OtpCountdownSeconds = remain;
                    OtpStatusMessage = $"Bạn có thể gửi lại mã sau {remain}s.";
                    return Page();
                }
            }

            // Tạo mã OTP 6 chữ số
            var random = new Random();
            var code = random.Next(100000, 999999).ToString();

            HttpContext.Session.SetString(SessionOtpCodeKey, code);
            HttpContext.Session.SetString(SessionOtpEmailKey, Input.Email);
            HttpContext.Session.SetString(SessionOtpExpireKey, now.Add(OtpLifetime).UtcTicks.ToString());
            HttpContext.Session.SetString(SessionOtpLastSentKey, now.UtcTicks.ToString());

            OtpCountdownSeconds = (int)OtpCooldown.TotalSeconds;
            OtpStatusMessage = $"Đã gửi mã xác nhận tới {Input.Email}. Mã có hiệu lực trong {OtpLifetime.TotalMinutes:N0} phút.";

            // Gửi email
            var subject = "Mã xác nhận đăng ký CinemaS";
            var body = $@"
<p>Xin chào,</p>
<p>Mã xác nhận đăng ký tài khoản CinemaS của bạn là: <strong>{code}</strong></p>
<p>Mã có hiệu lực trong {OtpLifetime.TotalMinutes:N0} phút.</p>
<p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>";
            await _emailSender.SendEmailAsync(Input.Email, subject, body);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ComputeOtpCountdown();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Kiểm tra OTP
            var storedCode = HttpContext.Session.GetString(SessionOtpCodeKey);
            var storedEmail = HttpContext.Session.GetString(SessionOtpEmailKey);
            var expireTicks = HttpContext.Session.GetString(SessionOtpExpireKey);

            if (string.IsNullOrEmpty(storedCode) ||
                string.IsNullOrEmpty(storedEmail) ||
                string.IsNullOrEmpty(expireTicks))
            {
                ModelState.AddModelError("Input.OtpCode", "Bạn chưa yêu cầu mã xác nhận hoặc mã đã hết hạn, vui lòng bấm \"Gửi mã\".");
                return Page();
            }

            if (!long.TryParse(expireTicks, out var eTicks))
            {
                ModelState.AddModelError("Input.OtpCode", "Mã xác nhận không hợp lệ, vui lòng gửi lại mã mới.");
                return Page();
            }

            var expireAt = new DateTimeOffset(eTicks, TimeSpan.Zero);
            if (DateTimeOffset.UtcNow > expireAt)
            {
                ModelState.AddModelError("Input.OtpCode", "Mã xác nhận đã hết hạn, vui lòng gửi lại mã mới.");
                return Page();
            }

            if (!string.Equals(storedEmail, Input.Email, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Input.Email", "Email hiện tại không khớp với email đã nhận mã xác nhận.");
                return Page();
            }

            if (!string.Equals(storedCode, Input.OtpCode?.Trim(), StringComparison.Ordinal))
            {
                ModelState.AddModelError("Input.OtpCode", "Mã xác nhận không đúng, vui lòng kiểm tra lại.");
                return Page();
            }

            var user = CreateUser();

            user.FullName = Input.FullName;
            user.Address = Input.Address;

            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password.");

                var userId = await _userManager.GetUserIdAsync(user);
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                    $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                // Xóa OTP sau khi đăng ký thành công
                HttpContext.Session.Remove(SessionOtpCodeKey);
                HttpContext.Session.Remove(SessionOtpEmailKey);
                HttpContext.Session.Remove(SessionOtpExpireKey);
                HttpContext.Session.Remove(SessionOtpLastSentKey);

                if (_userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                }
                else
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        private void ComputeOtpCountdown()
        {
            var lastSentTicks = HttpContext.Session.GetString(SessionOtpLastSentKey);
            if (!long.TryParse(lastSentTicks, out var ticks))
            {
                OtpCountdownSeconds = 0;
                return;
            }

            var lastSent = new DateTimeOffset(ticks, TimeSpan.Zero);
            var diff = DateTimeOffset.UtcNow - lastSent;
            if (diff >= OtpCooldown)
            {
                OtpCountdownSeconds = 0;
            }
            else
            {
                OtpCountdownSeconds = (int)Math.Ceiling((OtpCooldown - diff).TotalSeconds);
            }
        }

        private AppUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<AppUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(AppUser)}'. " +
                    $"Ensure that '{nameof(AppUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<AppUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<AppUser>)_userStore;
        }
    }
}
