using Microsoft.AspNetCore.Mvc;
using Mozaika.Api.Contracts;
using Mozaika.Api.Services;

namespace Mozaika.Api.Controllers;

[ApiController]
[Route("api/project-shares")]
public sealed class ProjectSharesController(ProjectWorkflowService projectWorkflowService) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<ActionResult<ProjectShareResolveResponse>> Resolve(string token)
    {
        try
        {
            var response = await projectWorkflowService.ResolveShareAsync(token);
            return Ok(response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }
}
