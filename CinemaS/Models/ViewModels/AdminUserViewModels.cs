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

        
        public int? Age { get; set; }
        public string? Address { get; set; }

        public bool EmailConfirmed { get; set; }

        
        public string? Password { get; set; }

        
        public DateTime? DateOfBirth { get; set; }
        public byte? Gender { get; set; }          
        public int SavePoint { get; set; }
        public int? AgeFromBirth { get; set; }     
        public string? AvatarPath { get; set; }
        public IList<string> SelectedRoles { get; set; } = new List<string>();
        public IList<string> AllRoles { get; set; } = new List<string>();

        // ✅ THÊM: Thông tin Rank hiện tại
        public string? MembershipRankId { get; set; }
        public string? RankName { get; set; }
    }
}
