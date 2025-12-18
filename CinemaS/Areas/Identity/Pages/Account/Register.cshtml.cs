// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using CinemaS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

        // dùng cho bước 2
        private const string SessionOtpVerifiedKey = "RegisterOtp_Verified";
        private const string SessionOtpFullNameKey = "RegisterOtp_FullName";

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

        public int OtpCountdownSeconds { get; set; }
        public string OtpStatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập email.")]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
            [StringLength(100)]
            [Display(Name = "Họ và tên")]
            public string FullName { get; set; } = default!;

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
        /// Xử lý nút GỬI MÃ / GỬI LẠI – chỉ dùng email.
        /// </summary>
        public async Task<IActionResult> OnPostSendOtpAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            // chỉ validate email, bỏ các field khác
            ModelState.Clear();

            if (Input == null || string.IsNullOrWhiteSpace(Input.Email))
            {
                ModelState.AddModelError("Input.Email", "Vui lòng nhập email để gửi mã xác nhận.");
                ComputeOtpCountdown();
                return Page();
            }

            var now = DateTimeOffset.UtcNow;
            var lastSentTicks = HttpContext.Session.GetString(SessionOtpLastSentKey);
            var lastEmail = HttpContext.Session.GetString(SessionOtpEmailKey);

            // chỉ áp cooldown nếu cùng email
            if (!string.IsNullOrEmpty(lastEmail) &&
                lastEmail.Equals(Input.Email, StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(lastSentTicks, out var ticks))
            {
                var lastSent = new DateTimeOffset(ticks, TimeSpan.Zero);
                var diff = now - lastSent;
                if (diff < OtpCooldown)
                {
                    var remain = (int)Math.Ceiling((OtpCooldown - diff).TotalSeconds);
                    OtpCountdownSeconds = remain;

                    // chỉ message chung, không chứa số giây
                    OtpStatusMessage = "Bạn vừa yêu cầu mã, vui lòng chờ một lúc trước khi gửi lại.";
                    return Page();
                }
            }

            // tạo mã OTP
            var random = new Random();
            var code = random.Next(100000, 999999).ToString();

            HttpContext.Session.SetString(SessionOtpCodeKey, code);
            HttpContext.Session.SetString(SessionOtpEmailKey, Input.Email);
            HttpContext.Session.SetString(SessionOtpExpireKey, now.Add(OtpLifetime).UtcTicks.ToString());
            HttpContext.Session.SetString(SessionOtpLastSentKey, now.UtcTicks.ToString());

            // reset cờ bước 2
            HttpContext.Session.Remove(SessionOtpVerifiedKey);
            HttpContext.Session.Remove(SessionOtpFullNameKey);

            // luôn 30 giây cho UI đếm
            OtpCountdownSeconds = (int)OtpCooldown.TotalSeconds;
            OtpStatusMessage = $"Đã gửi mã xác nhận tới {Input.Email}. Mã có hiệu lực trong {OtpLifetime.TotalMinutes:N0} phút.";

            var subject = "Mã xác nhận đăng ký CinemaS";
            var body = $@"
<p>Xin chào,</p>
<p>Mã xác nhận đăng ký tài khoản CinemaS của bạn là: <strong>{code}</strong></p>
<p>Mã có hiệu lực trong {OtpLifetime.TotalMinutes:N0} phút.</p>
<p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>";
            try
            {
                await _emailSender.SendEmailAsync(Input.Email, subject, body);
            }
            catch
            {
                // Không suy đoán nguyên nhân; chỉ báo chung
                OtpStatusMessage = "Không gửi được email OTP. Vui lòng thử lại sau.";
                ComputeOtpCountdown();
                return Page();
            }


            return Page();
        }


        /// <summary>
        /// Xử lý nút TIẾP TỤC – kiểm tra OTP, đúng thì chuyển sang trang tạo mật khẩu.
        /// </summary>
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ComputeOtpCountdown();

            if (!ModelState.IsValid)
            {
                return Page();
            }

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

            // OTP đúng → lưu vào session cho bước 2
            HttpContext.Session.SetString(SessionOtpVerifiedKey, "true");
            HttpContext.Session.SetString(SessionOtpFullNameKey, Input.FullName ?? string.Empty);

            // chuyển sang trang tạo mật khẩu
            return RedirectToPage("RegisterPassword", new { returnUrl });
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
