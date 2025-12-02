using Microsoft.AspNetCore.Mvc;

namespace Aspire_Full.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly TimeProvider _timeProvider;
    private readonly DateTimeOffset _startTime;

    public HealthController(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _startTime = timeProvider.GetUtcNow();
    }

    [HttpGet]
    public IActionResult GetHealth()
    {
        var now = _timeProvider.GetUtcNow();
        var uptime = now - _startTime;
        return Ok(new
        {
            status = "healthy",
            uptime = FormatUptime(uptime),
            timestamp = now.UtcDateTime
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
