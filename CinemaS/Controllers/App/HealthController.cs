using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaS.Models;

namespace CinemaS.Controllers.Api
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly CinemaContext _db;
        public HealthController(CinemaContext db) => _db = db;

        [HttpGet("db")]
        public async Task<IActionResult> Db()
        {
            try
            {
                // kiểm tra kết nối DB
                var canConnect = await _db.Database.CanConnectAsync();

                // kiểm tra có bảng Users (AspNetUsers) và có bản ghi nào không
                var usersCount = await _db.Users.CountAsync();

                return Ok(new
                {
                    ok = canConnect,
                    usersCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    ok = false,
                    error = ex.Message
                });
            }
        }
    }
}
