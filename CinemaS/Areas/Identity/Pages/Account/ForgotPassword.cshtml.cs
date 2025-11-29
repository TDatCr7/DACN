// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using CinemaS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace CinemaS.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailSender _emailSender;

        private const string SessionOtpCodeKey = "ForgotPassword_OtpCode";
        private const string SessionOtpEmailKey = "ForgotPassword_OtpEmail";
        private const string SessionOtpExpireKey = "ForgotPassword_OtpExpireAt";
        private const string SessionOtpLastSentKey = "ForgotPassword_OtpLastSent";

        // Session keys cho ResetPassword page
        private const string ResetPasswordOtpCodeKey = "ResetPassword_OtpCode";
        private const string ResetPasswordOtpEmailKey = "ResetPassword_OtpEmail";

        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan OtpCooldown = TimeSpan.FromSeconds(30);

        public ForgotPasswordModel(UserManager<AppUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required(ErrorMessage = "Vui lòng nhập email.")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mã xác nhận.")]
            [Display(Name = "Mã xác nhận")]
            public string OtpCode { get; set; }
        }

        public void OnGet()
        {
        }

        /// <summary>
        /// AJAX handler: Gửi mã OTP đến email
        /// </summary>
        public async Task<IActionResult> OnPostSendOtpAsync([FromBody] SendOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Email))
            {
                return new JsonResult(new { success = false, message = "Vui lòng nhập email." });
            }

            // Kiểm tra email có tồn tại không
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                // Không tiết lộ user không tồn tại, nhưng vẫn báo thành công
                return new JsonResult(new { success = false, message = "Email không tồn tại hoặc chưa được xác nhận." });
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
                    return new JsonResult(new { success = false, message = $"Bạn có thể gửi lại mã sau {remain}s.", countdown = remain });
                }
            }

            // Tạo mã OTP 6 chữ số
            var random = new Random();
            var code = random.Next(100000, 999999).ToString();

            HttpContext.Session.SetString(SessionOtpCodeKey, code);
            HttpContext.Session.SetString(SessionOtpEmailKey, request.Email);
            HttpContext.Session.SetString(SessionOtpExpireKey, now.Add(OtpLifetime).UtcTicks.ToString());
            HttpContext.Session.SetString(SessionOtpLastSentKey, now.UtcTicks.ToString());

            // Gửi email
            var subject = "Mã xác nhận đặt lại mật khẩu CinemaS";
            var body = $@"
<p>Xin chào,</p>
<p>Mã xác nhận đặt lại mật khẩu CinemaS của bạn là: <strong>{code}</strong></p>
<p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>";
            
            await _emailSender.SendEmailAsync(request.Email, subject, body);

            return new JsonResult(new 
            { 
                success = true, 
                message = $"Đã gửi mã xác nhận tới {request.Email}",
                countdown = (int)OtpCooldown.TotalSeconds
            });
        }

        /// <summary>
        /// AJAX handler: Xác thực mã OTP và chuyển đến trang ResetPassword
        /// </summary>
        public async Task<IActionResult> OnPostVerifyOtpAsync([FromBody] VerifyOtpRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Email))
            {
                return new JsonResult(new { success = false, message = "Vui lòng nhập email." });
            }

            if (string.IsNullOrWhiteSpace(request?.OtpCode))
            {
                return new JsonResult(new { success = false, message = "Vui lòng nhập mã xác nhận." });
            }

            // Kiểm tra OTP
            var storedCode = HttpContext.Session.GetString(SessionOtpCodeKey);
            var storedEmail = HttpContext.Session.GetString(SessionOtpEmailKey);
            var expireTicks = HttpContext.Session.GetString(SessionOtpExpireKey);

            if (string.IsNullOrEmpty(storedCode) ||
                string.IsNullOrEmpty(storedEmail) ||
                string.IsNullOrEmpty(expireTicks))
            {
                return new JsonResult(new { success = false, message = "Bạn chưa yêu cầu mã xác nhận hoặc mã đã hết hạn, vui lòng bấm \"Gửi mã\"." });
            }

            if (!long.TryParse(expireTicks, out var eTicks))
            {
                return new JsonResult(new { success = false, message = "Mã xác nhận không hợp lệ, vui lòng gửi lại mã mới." });
            }

            var expireAt = new DateTimeOffset(eTicks, TimeSpan.Zero);
            if (DateTimeOffset.UtcNow > expireAt)
            {
                return new JsonResult(new { success = false, message = "Mã xác nhận đã hết hạn, vui lòng gửi lại mã mới." });
            }

            if (!string.Equals(storedEmail, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                return new JsonResult(new { success = false, message = "Email hiện tại không khớp với email đã nhận mã xác nhận." });
            }

            if (!string.Equals(storedCode, request.OtpCode.Trim(), StringComparison.Ordinal))
            {
                return new JsonResult(new { success = false, message = "Mã xác nhận không đúng, vui lòng kiểm tra lại." });
            }

            // Xác thực thành công → tạo token reset password
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return new JsonResult(new { success = false, message = "Email không tồn tại trong hệ thống." });
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            // Lưu mã OTP cho ResetPassword page để xác thực lại
            HttpContext.Session.SetString(ResetPasswordOtpCodeKey, storedCode);
            HttpContext.Session.SetString(ResetPasswordOtpEmailKey, storedEmail);

            // Xóa OTP của ForgotPassword sau khi xác thực thành công
            HttpContext.Session.Remove(SessionOtpCodeKey);
            HttpContext.Session.Remove(SessionOtpEmailKey);
            HttpContext.Session.Remove(SessionOtpExpireKey);
            HttpContext.Session.Remove(SessionOtpLastSentKey);

            return new JsonResult(new { success = true, redirectUrl = callbackUrl });
        }

        public class SendOtpRequest
        {
            public string Email { get; set; }
        }

        public class VerifyOtpRequest
        {
            public string Email { get; set; }
            public string OtpCode { get; set; }
        }
    }
}
