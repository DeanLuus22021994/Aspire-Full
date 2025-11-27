using Microsoft.AspNetCore.Mvc;

namespace Aspire_Full.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    [HttpGet]
    public IActionResult GetHealth()
    {
        var uptime = DateTime.UtcNow - StartTime;
        return Ok(new
        {
            status = "healthy",
            uptime = FormatUptime(uptime),
            timestamp = DateTime.UtcNow
        });
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        if (uptime.TotalMinutes >= 1)
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        return $"{uptime.Seconds}s";
    }
}
