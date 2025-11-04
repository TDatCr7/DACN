using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CinemaS.Models
{
    // AppUser lưu ở AspNetUsers (Identity)
    public class AppUser : IdentityUser
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = default!;
        [StringLength(300)]
        public string? Address { get; set; }
        [StringLength(300)]
        public string? Age { get; set; }   // giữ nguyên kiểu mẫu của bạn
    }
}
