using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;

namespace Mozaika.Api.Controllers;

[ApiController]
[Route("api/bootstrap")]
public sealed class BootstrapController(MozaikaDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BootstrapResponse>> Get()
    {
        var settings = await DbHelpers.GetOrCreateSettingsAsync(dbContext);
        var colors = await dbContext.Colors
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync();
        var groutColors = await dbContext.GroutColors
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync();

        var payload = new BootstrapResponse
        {
            Settings = settings.ToRead(),
            Colors = colors.Select(item => item.ToRead()).ToList(),
            GroutColors = groutColors.Select(item => item.ToRead()).ToList(),
        };

        return Ok(payload);
    }
}
