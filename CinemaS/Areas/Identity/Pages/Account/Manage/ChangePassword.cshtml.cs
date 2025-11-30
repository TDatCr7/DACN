// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using CinemaS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace CinemaS.Areas.Identity.Pages.Account.Manage
{
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ILogger<ChangePasswordModel> _logger;
        private readonly IEmailSender _emailSender;

        private const string SessionOtpCodeKey = "ChangePassword_OtpCode";
        private const string SessionOtpExpireKey = "ChangePassword_OtpExpireAt";
        private const string SessionOtpLastSentKey = "ChangePassword_OtpLastSent";

        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan OtpCooldown = TimeSpan.FromSeconds(30);

        public ChangePasswordModel(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ILogger<ChangePasswordModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu hiện tại")]
            public string OldPassword { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
            [StringLength(100, ErrorMessage = "Mật khẩu tối thiểu {2} và tối đa {1} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu mới")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu mới")]
            [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mã xác nhận.")]
            [Display(Name = "Mã xác nhận")]
            public string OtpCode { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (!hasPassword)
            {
                return RedirectToPage("./SetPassword");
            }

            return Page();
        }

        /// <summary>
        /// AJAX handler: Gửi mã OTP đến email người dùng
        /// </summary>
        public async Task<IActionResult> OnPostSendOtpAsync([FromBody] SendOtpRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return new JsonResult(new { success = false, message = "Không tìm thấy người dùng." });
            }

            if (string.IsNullOrWhiteSpace(request?.OldPassword))
            {
                return new JsonResult(new { success = false, message = "Vui lòng nhập mật khẩu hiện tại." });
            }

            if (string.IsNullOrWhiteSpace(request?.NewPassword))
            {
                return new JsonResult(new { success = false, message = "Vui lòng nhập mật khẩu mới." });
            }

            if (string.IsNullOrWhiteSpace(request?.ConfirmPassword))
            {
                return new JsonResult(new { success = false, message = "Vui lòng nhập xác nhận mật khẩu." });
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                return new JsonResult(new { success = false, message = "Mật khẩu xác nhận không khớp." });
            }

            // Kiểm tra mật khẩu cũ
            var passwordCheck = await _userManager.CheckPasswordAsync(user, request.OldPassword);
            if (!passwordCheck)
            {
                return new JsonResult(new { success = false, message = "Mật khẩu cũ không đúng." });
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
            HttpContext.Session.SetString(SessionOtpExpireKey, now.Add(OtpLifetime).UtcTicks.ToString());
            HttpContext.Session.SetString(SessionOtpLastSentKey, now.UtcTicks.ToString());

            // Gửi email
            var subject = "Mã xác nhận đổi mật khẩu CinemaS";
            var body = $@"
<p>Xin chào {user.FullName},</p>
<p>Mã xác nhận đổi mật khẩu của bạn là: <strong>{code}</strong></p>
<p>Mã có hiệu lực trong {OtpLifetime.TotalMinutes:N0} phút.</p>
<p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>";
            
            await _emailSender.SendEmailAsync(user.Email, subject, body);

            return new JsonResult(new 
            { 
                success = true, 
                message = $"Đã gửi mã xác nhận tới {user.Email}",
                countdown = (int)OtpCooldown.TotalSeconds
            });
        }

        /// <summary>
        /// AJAX handler: Xác thực mã OTP và tự động đổi mật khẩu
        /// </summary>
        public async Task<IActionResult> OnPostVerifyOtpAndChangePasswordAsync([FromBody] VerifyOtpRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return new JsonResult(new { success = false, message = "Không tìm thấy người dùng." });
            }

            if (string.IsNullOrWhiteSpace(request?.OtpCode))
            {
                return new JsonResult(new { success = false, message = "Vui lòng nhập mã xác nhận." });
            }

            // Kiểm tra OTP
            var storedCode = HttpContext.Session.GetString(SessionOtpCodeKey);
            var expireTicks = HttpContext.Session.GetString(SessionOtpExpireKey);

            if (string.IsNullOrEmpty(storedCode) || string.IsNullOrEmpty(expireTicks))
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

            if (!string.Equals(storedCode, request.OtpCode.Trim(), StringComparison.Ordinal))
            {
                return new JsonResult(new { success = false, message = "Mã xác nhận không đúng, vui lòng kiểm tra lại." });
            }

            // Kiểm tra lại mật khẩu cũ
            var passwordCheck = await _userManager.CheckPasswordAsync(user, request.OldPassword);
            if (!passwordCheck)
            {
                return new JsonResult(new { success = false, message = "Mật khẩu cũ không đúng." });
            }

            // Kiểm tra mật khẩu mới và xác nhận
            if (request.NewPassword != request.ConfirmPassword)
            {
                return new JsonResult(new { success = false, message = "Mật khẩu xác nhận không khớp." });
            }

            // Đổi mật khẩu
            var changePasswordResult = await _userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                var errors = string.Join(", ", changePasswordResult.Errors.Select(e => e.Description));
                return new JsonResult(new { success = false, message = $"Không thể đổi mật khẩu: {errors}" });
            }

            // Xóa OTP sau khi đổi mật khẩu thành công
            HttpContext.Session.Remove(SessionOtpCodeKey);
            HttpContext.Session.Remove(SessionOtpExpireKey);
            HttpContext.Session.Remove(SessionOtpLastSentKey);

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("User changed their password successfully with OTP verification.");

            return new JsonResult(new { success = true, message = "Mật khẩu đã được thay đổi thành công." });
        }

        public class SendOtpRequest
        {
            public string OldPassword { get; set; }
            public string NewPassword { get; set; }
            public string ConfirmPassword { get; set; }
        }

        public class VerifyOtpRequest
        {
            public string OldPassword { get; set; }
            public string NewPassword { get; set; }
            public string ConfirmPassword { get; set; }
            public string OtpCode { get; set; }
        }
    }
}
