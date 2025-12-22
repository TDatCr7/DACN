using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using CinemaS.Models;
using CinemaS.Services;

namespace CinemaS.Controllers.Api
{
    [ApiController]
    [Route("api")]
    public class MobileAuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly IRegisterOtpStore _otpStore;

        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

        public MobileAuthController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IEmailSender emailSender,
            IRegisterOtpStore otpStore)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _otpStore = otpStore;
        }

        public record SendOtpReq(string Email);
        public record VerifyOtpReq(string Email, string Otp);
        public record RegisterReq(string Email, string Password, string FullName);
        public record LoginReq(string Email, string Password);

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpReq req)
        {
            var email = (req.Email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email không hợp lệ" });

            // tạo OTP 6 số
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var expireAt = DateTimeOffset.UtcNow.Add(OtpLifetime);

            _otpStore.SaveOtp(email, code, expireAt);

            var subject = "Mã xác nhận đăng ký CinemaS";
            var body = $@"
<p>Xin chào,</p>
<p>Mã xác nhận đăng ký tài khoản CinemaS của bạn là: <strong>{code}</strong></p>
<p>Mã có hiệu lực trong {OtpLifetime.TotalMinutes:N0} phút.</p>";

            await _emailSender.SendEmailAsync(email, subject, body);

            return Ok(new { message = "Đã gửi mã OTP" });
        }

        [HttpPost("verify-otp")]
        public IActionResult VerifyOtp([FromBody] VerifyOtpReq req)
        {
            var email = (req.Email ?? "").Trim();
            var otp = (req.Otp ?? "").Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                return BadRequest(new { message = "Thiếu email hoặc OTP" });

            if (!_otpStore.TryGet(email, out var state))
                return BadRequest(new { message = "Bạn chưa yêu cầu mã hoặc mã đã hết hạn" });

            if (DateTimeOffset.UtcNow > state.ExpireAt)
            {
                _otpStore.Remove(email);
                return BadRequest(new { message = "OTP đã hết hạn" });
            }

            if (!string.Equals(state.Code, otp, StringComparison.Ordinal))
                return BadRequest(new { message = "OTP không đúng" });

            // Chưa set FullName ở bước này (Flutter sẽ gửi FullName khi register)
            _otpStore.MarkVerified(email, state.FullName);

            return Ok(new { message = "OTP hợp lệ" }); // Flutter đang check chuỗi "otp hợp lệ"
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterReq req)
        {
            var email = (req.Email ?? "").Trim();
            var password = (req.Password ?? "").Trim();
            var fullName = (req.FullName ?? "").Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
                return BadRequest(new { message = "Thiếu email/mật khẩu/họ tên" });

            if (!_otpStore.TryGet(email, out var state))
                return BadRequest(new { message = "Bạn chưa yêu cầu OTP" });

            if (!state.Verified)
                return BadRequest(new { message = "Bạn chưa xác thực OTP" });

            if (DateTimeOffset.UtcNow > state.ExpireAt)
            {
                _otpStore.Remove(email);
                return BadRequest(new { message = "OTP đã hết hạn" });
            }

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null) return BadRequest(new { message = "Email đã tồn tại" });

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };


            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                return BadRequest(new
                {
                    message = "Đăng ký thất bại",
                    errors = result.Errors.Select(e => e.Description).ToList()
                });

            _otpStore.Remove(email);

            return Ok(new { message = "Đăng ký thành công" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginReq req)
        {
            var email = (req.Email ?? "").Trim();
            var password = (req.Password ?? "");

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return BadRequest(new { message = "Thiếu email hoặc mật khẩu" });

            // giống web: tìm theo Email
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return Unauthorized(new { message = "Sai email hoặc mật khẩu" });

            // giống web: check password trước để debug đúng lỗi
            var pwdValid = await _userManager.CheckPasswordAsync(user, password);
            if (!pwdValid)
                return Unauthorized(new { message = "Sai email hoặc mật khẩu" });

            // sign-in cookie (không dùng được cho Flutter nếu không giữ cookie, nhưng để xác nhận credentials đúng)
            var result = await _signInManager.PasswordSignInAsync(
                user, password, isPersistent: false, lockoutOnFailure: false);

            if (!result.Succeeded)
                return Unauthorized(new { message = "Không thể đăng nhập (SignIn thất bại)" });

            // trả thêm userId để app lưu
            return Ok(new
            {
                message = "Đăng nhập thành công",
                userId = user.Id,
                email = user.Email,
                fullName = user.FullName
            });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> Profile([FromQuery] string? email)
        {
            // Vì app bạn chưa dùng JWT thật, cho phép lấy profile bằng email query tạm thời
            email = (email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Thiếu email" });

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return NotFound(new { message = "Không tìm thấy user" });

            return Ok(new
            {
                name = user.FullName,
                email = user.Email,
                picture = "" 
            });
        }

    }
}
