using Microsoft.AspNetCore.Mvc;
using Mozaika.Api.Contracts;
using Mozaika.Api.Security;
using Mozaika.Api.Services;

namespace Mozaika.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(
    ProjectStorageService projectStorageService,
    ProjectWorkflowService projectWorkflowService
) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProjectListItemResponse>>> List()
    {
        var authError = RequireAuthenticated();
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var payload = await projectStorageService.ListProjectsAsync();
            return Ok(payload);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ProjectReadResponse>> Create([FromBody] ProjectCreateRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectStorageService.CreateProjectAsync(payload);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpGet("{projectId:int}")]
    public async Task<ActionResult<ProjectReadResponse>> Get(int projectId)
    {
        var authError = RequireAuthenticated();
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectStorageService.GetProjectAsync(projectId);
            return Ok(response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpPatch("{projectId:int}")]
    public async Task<ActionResult<ProjectReadResponse>> Update(int projectId, [FromBody] ProjectUpdateRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

        if (!payload.HasAnyValue())
        {
            return BadRequest(new ApiError("Передайте хотя бы одно поле для обновления проекта."));
        }

        try
        {
            var response = await projectStorageService.UpdateProjectAsync(projectId, payload);
            return Ok(response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpPost("{projectId:int}/generations")]
    public async Task<ActionResult<ProjectGenerationReadResponse>> SaveGeneration(
        int projectId,
        [FromBody] ProjectSaveGenerationRequest payload
    )
    {
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectStorageService.SaveGenerationAsync(projectId, payload, setAsActive: true);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpGet("{projectId:int}/generations/{generationId:int}")]
    public async Task<ActionResult<ProjectGenerationReadResponse>> GetGeneration(int projectId, int generationId)
    {
        var authError = RequireAuthenticated();
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectStorageService.GetGenerationAsync(projectId, generationId);
            return Ok(response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpPost("{projectId:int}/generations/{generationId:int}/activate")]
    public async Task<ActionResult<ProjectReadResponse>> ActivateGeneration(int projectId, int generationId)
    {
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectStorageService.ActivateGenerationAsync(projectId, generationId);
            return Ok(response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpGet("{projectId:int}/shares")]
    public async Task<ActionResult<List<ProjectShareReadResponse>>> ListShares(int projectId)
    {
        var authError = RequireAuthenticated();
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectWorkflowService.ListSharesAsync(projectId);
            return Ok(response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpPost("{projectId:int}/shares")]
    public async Task<ActionResult<ProjectShareReadResponse>> CreateShare(
        int projectId,
        [FromBody] ProjectShareCreateRequest payload
    )
    {
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectWorkflowService.CreateShareAsync(
                projectId,
                payload,
                CurrentSession?.Username ?? "unknown"
            );
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpPost("{projectId:int}/shares/{shareId:int}/revoke")]
    public async Task<ActionResult<ProjectShareReadResponse>> RevokeShare(int projectId, int shareId)
    {
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectWorkflowService.RevokeShareAsync(projectId, shareId);
            return Ok(response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpGet("{projectId:int}/orders")]
    public async Task<ActionResult<List<ProjectOrderReadResponse>>> ListOrders(int projectId)
    {
        var authError = RequireAuthenticated();
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectWorkflowService.ListOrdersAsync(projectId);
            return Ok(response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpPost("{projectId:int}/orders")]
    public async Task<ActionResult<ProjectOrderReadResponse>> CreateOrder(
        int projectId,
        [FromBody] ProjectOrderCreateRequest payload
    )
    {
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectWorkflowService.CreateOrderAsync(
                projectId,
                payload,
                CurrentSession?.Username ?? "unknown"
            );
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpPatch("{projectId:int}/orders/{orderId:int}/status")]
    public async Task<ActionResult<ProjectOrderReadResponse>> UpdateOrderStatus(
        int projectId,
        int orderId,
        [FromBody] ProjectOrderStatusUpdateRequest payload
    )
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        try
        {
            var response = await projectWorkflowService.UpdateOrderStatusAsync(
                projectId,
                orderId,
                payload,
                CurrentSession?.Username ?? "admin"
            );
            return Ok(response);
        }
        catch (ProjectStorageException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }
}
