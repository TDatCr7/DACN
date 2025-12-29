// Backend - thêm mới controller UsersController.cs
// MỤC TIÊU: resolve Users.UserId từ email để Flutter dùng cho /vnpay/create
// Route: GET /api/users/resolve?email=...

using System;
using System.Linq;
using System.Threading.Tasks;
using CinemaS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaS.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly CinemaContext _context;
        public UsersController(CinemaContext context)
        {
            _context = context;
        }

        [HttpGet("resolve")]
        public async Task<IActionResult> Resolve([FromQuery] string email)
        {
            email = (email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { ok = false, message = "Missing email" });

            var user = await _context.Users.AsNoTracking()
                .Where(u => u.Email != null && u.Email.Trim() == email)
                .Select(u => new { u.UserId, u.Email })
                .FirstOrDefaultAsync();

            if (user == null || string.IsNullOrWhiteSpace(user.UserId))
                return NotFound(new { ok = false, message = "User not found" });

            return Ok(new { ok = true, userId = user.UserId });
        }
    }
}
