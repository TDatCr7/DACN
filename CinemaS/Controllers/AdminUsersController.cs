using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CinemaS.Models;
using CinemaS.ViewModels.AdminUsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly CinemaContext _context;

        public AdminUsersController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            CinemaContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: /AdminUsers
        public async Task<IActionResult> Index(string? search)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(u =>
                    (u.FullName ?? string.Empty).Contains(search) ||
                    (u.Email ?? string.Empty).Contains(search) ||
                    (u.UserName ?? string.Empty).Contains(search));
            }

            var users = await query
                .OrderBy(u => u.FullName)
                .ThenBy(u => u.Email)
                .ToListAsync();

            var vms = new List<AdminUserListItemVm>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                vms.Add(new AdminUserListItemVm
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnabled = user.LockoutEnabled,
                    Roles = roles
                });
            }

            ViewData["Title"] = "Quản lý tài khoản";
            return View(vms);
        }

        // GET: /AdminUsers/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            // đọc profile từ bảng Users (bảng riêng)
            Users? profile = null;
            if (!string.IsNullOrEmpty(user.Email))
            {
                profile = await _context.Set<Users>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == user.Email);
            }

            // tính tuổi từ ngày sinh (nếu có)
            int? ageFromBirth = null;
            var dob = profile?.DateOfBirth;

            if (dob.HasValue)
            {
                var today = DateTime.Today;
                var d = dob.Value.Date;
                var age = today.Year - d.Year;
                if (d > today.AddYears(-age)) age--;
                ageFromBirth = age;
            }

            int? ageField = null;
            if (!string.IsNullOrWhiteSpace(user.Age) &&
                int.TryParse(user.Age, out var parsedAge))
            {
                ageField = parsedAge;
            }

            var vm = new AdminUserEditVm
            {
                Id = user.Id,
                FullName = user.FullName,
                Address = user.Address,
                Age = ageField,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                EmailConfirmed = user.EmailConfirmed,
                SelectedRoles = roles.ToList(),

                DateOfBirth = profile?.DateOfBirth,
                Gender = profile?.Gender,
                SavePoint = profile?.SavePoint ?? 0,
                AgeFromBirth = ageFromBirth
            };

            ViewData["Title"] = "Thông tin tài khoản";
            return View(vm);
        }

        // GET: /AdminUsers/Create
        public async Task<IActionResult> Create()
        {
            var vm = new AdminUserEditVm
            {
                AllRoles = await _roleManager.Roles
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name!)
                    .ToListAsync()
            };

            ViewData["Title"] = "Thêm tài khoản";
            return View("Edit", vm);
        }

        // POST: /AdminUsers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminUserEditVm vm)
        {
            vm.AllRoles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Thêm tài khoản";
                return View("Edit", vm);
            }

            var user = new AppUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                FullName = vm.FullName,
                Address = vm.Address,
                PhoneNumber = vm.PhoneNumber,
                EmailConfirmed = true,
                Age = vm.Age?.ToString()
            };

            var password = string.IsNullOrWhiteSpace(vm.Password)
                ? "CinemaS@123"
                : vm.Password;

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                ViewData["Title"] = "Thêm tài khoản";
                return View("Edit", vm);
            }

            if (vm.SelectedRoles != null && vm.SelectedRoles.Any())
                await _userManager.AddToRolesAsync(user, vm.SelectedRoles);

            return RedirectToAction(nameof(Index));
        }

        // GET: /AdminUsers/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            int? age = null;
            if (!string.IsNullOrWhiteSpace(user.Age) &&
                int.TryParse(user.Age, out var parsedAge))
            {
                age = parsedAge;
            }

            var vm = new AdminUserEditVm
            {
                Id = user.Id,
                FullName = user.FullName,
                Address = user.Address,
                Age = age,
                Email = user.Email,
                UserName = user.UserName,
                PhoneNumber = user.PhoneNumber,
                EmailConfirmed = user.EmailConfirmed,
                SelectedRoles = roles.ToList(),
                AllRoles = await _roleManager.Roles
                    .OrderBy(r => r.Name)
                    .Select(r => r.Name!)
                    .ToListAsync()
            };

            ViewData["Title"] = "Chỉnh sửa tài khoản";
            return View(vm);
        }

        // POST: /AdminUsers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AdminUserEditVm vm)
        {
            if (id != vm.Id)
                return NotFound();

            vm.AllRoles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();

            if (!ModelState.IsValid)
            {
                ViewData["Title"] = "Chỉnh sửa tài khoản";
                return View(vm);
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            user.FullName = vm.FullName;
            user.Address = vm.Address;
            user.Email = vm.Email;
            user.UserName = vm.Email;
            user.PhoneNumber = vm.PhoneNumber;
            user.Age = vm.Age?.ToString();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                ViewData["Title"] = "Chỉnh sửa tài khoản";
                return View(vm);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var selectedRoles = vm.SelectedRoles ?? new List<string>();

            var rolesToAdd = selectedRoles.Except(currentRoles);
            var rolesToRemove = currentRoles.Except(selectedRoles);

            if (rolesToAdd.Any())
                await _userManager.AddToRolesAsync(user, rolesToAdd);

            if (rolesToRemove.Any())
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

            return RedirectToAction(nameof(Index));
        }

        // POST: /AdminUsers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            await _userManager.DeleteAsync(user);
            // CinemaContext.SaveChangesAsync sẽ tự xoá bản Users tương ứng nếu cấu hình cascade / xử lý thủ công
            return RedirectToAction(nameof(Index));
        }
    }
}
