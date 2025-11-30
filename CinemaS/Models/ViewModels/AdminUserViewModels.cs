// CinemaS/ViewModels/AdminUsers/AdminUserVms.cs
using System;
using System.Collections.Generic;

namespace CinemaS.ViewModels.AdminUsers
{
    public class AdminUserListItemVm
    {
        public string Id { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool LockoutEnabled { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
    }

    public class AdminUserEditVm
    {
        public string Id { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? PhoneNumber { get; set; }

        // Age lưu để edit nếu còn dùng trong AspNetUsers (string -> int?)
        public int? Age { get; set; }
        public string? Address { get; set; }

        public bool EmailConfirmed { get; set; }

        // Dùng cho tạo mới
        public string? Password { get; set; }

        // Dữ liệu đọc từ bảng Users
        public DateTime? DateOfBirth { get; set; }
        public byte? Gender { get; set; }          // 0/1/2...
        public int SavePoint { get; set; }
        public int? AgeFromBirth { get; set; }     // tuổi tính từ ngày sinh

        public IList<string> SelectedRoles { get; set; } = new List<string>();
        public IList<string> AllRoles { get; set; } = new List<string>();
    }
}
