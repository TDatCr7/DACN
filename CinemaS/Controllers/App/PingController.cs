// Backend/Controllers/PingController.cs
using Microsoft.AspNetCore.Mvc;

namespace CinemaBookingApi.Controllers;

[ApiController]
[Route("api/ping")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { ok = true, utc = DateTime.UtcNow });
}
