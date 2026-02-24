using Microsoft.AspNetCore.Mvc;
using Mozaika.Api.Contracts;
using Mozaika.Api.Services;

namespace Mozaika.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ApiControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthLoginResponse>> Login([FromBody] AuthLoginRequest payload)
    {
        try
        {
            var login = await authService.LoginAsync(payload.Username, payload.Password);
            return Ok(new AuthLoginResponse
            {
                Token = login.Token,
                ExpiresAt = login.Session.ExpiresAt,
                User = new AuthUserReadResponse
                {
                    Id = login.Session.UserId,
                    Username = login.Session.Username,
                    DisplayName = login.Session.DisplayName,
                    Role = login.Session.Role,
                },
            });
        }
        catch (AuthException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Message));
        }
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout()
    {
        var session = CurrentSession;
        if (session is null)
        {
            return Ok();
        }

        var token = ExtractCurrentToken();
        await authService.LogoutAsync(token);
        return Ok();
    }

    [HttpGet("me")]
    public ActionResult<AuthSessionReadResponse> Me()
    {
        return Ok(authService.BuildSessionResponse(CurrentSession));
    }

    private string? ExtractCurrentToken()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        var tokenHeader = Request.Headers["X-Mozaika-Token"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(tokenHeader) ? null : tokenHeader.Trim();
    }
}
