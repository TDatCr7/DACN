// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using CinemaS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace CinemaS.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;

        private const string ResetPasswordOtpCodeKey = "ResetPassword_OtpCode";
        private const string ResetPasswordOtpEmailKey = "ResetPassword_OtpEmail";

        public ResetPasswordModel(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
            [StringLength(100, ErrorMessage = "Mật khẩu phải có ít nhất {2} và tối đa {1} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu mới")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }

            public string Code { get; set; }

            public string Email { get; set; }
        }

        public IActionResult OnGet(string code = null)
        {
            if (code == null)
            {
                TempData["AuthMessage"] = "Thiếu mã xác thực. Vui lòng thử lại từ trang quên mật khẩu.";
                TempData["AuthMessageType"] = "error";
                return RedirectToPage("./ForgotPassword");
            }

            // Lấy email từ session
            var email = HttpContext.Session.GetString(ResetPasswordOtpEmailKey);
            if (string.IsNullOrEmpty(email))
            {
                TempData["AuthMessage"] = "Phiên làm việc đã hết hạn. Vui lòng thử lại.";
                TempData["AuthMessageType"] = "error";
                return RedirectToPage("./ForgotPassword");
            }

            Input = new InputModel
            {
                Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
                Email = email
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Lấy email từ session nếu không có trong form
            if (string.IsNullOrEmpty(Input.Email))
            {
                Input.Email = HttpContext.Session.GetString(ResetPasswordOtpEmailKey);
            }

            if (string.IsNullOrEmpty(Input.Email))
            {
                ModelState.AddModelError(string.Empty, "Phiên làm việc đã hết hạn. Vui lòng thử lại từ trang quên mật khẩu.");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                HttpContext.Session.Remove(ResetPasswordOtpCodeKey);
                HttpContext.Session.Remove(ResetPasswordOtpEmailKey);
                TempData["AuthMessageType"] = "success";
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}
