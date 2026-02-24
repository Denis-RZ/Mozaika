using Microsoft.AspNetCore.Mvc;
using Mozaika.Api.Contracts;
using Mozaika.Api.Security;
using Mozaika.Api.Services;

namespace Mozaika.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected AuthSessionInfo? CurrentSession => HttpContext.GetAuthSession();

    protected ActionResult? RequireAuthenticated()
    {
        if (CurrentSession is null)
        {
            return Unauthorized(new ApiError("Требуется авторизация."));
        }

        return null;
    }

    protected ActionResult? RequireAnyRole(params string[] roles)
    {
        var session = CurrentSession;
        if (session is null)
        {
            return Unauthorized(new ApiError("Требуется авторизация."));
        }

        if (!AppRoles.HasAccess(session.Role, roles))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("Недостаточно прав."));
        }

        return null;
    }
}
