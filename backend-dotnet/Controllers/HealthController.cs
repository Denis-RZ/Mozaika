using Microsoft.AspNetCore.Mvc;
using Mozaika.Api.Database;

namespace Mozaika.Api.Controllers;

[ApiController]
public sealed class HealthController(MozaikaDbContext dbContext) : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Get()
    {
        try
        {
            var ok = dbContext.Database.CanConnect();
            return Ok(new { status = ok ? "ok" : "degraded" });
        }
        catch
        {
            return Ok(new { status = "degraded" });
        }
    }
}
