using Mozaika.Api.Services;

namespace Mozaika.Api.Security;

public sealed class AuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AuthService authService)
    {
        var token = ReadToken(context.Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            var session = await authService.TryGetSessionAsync(token!);
            if (session is not null)
            {
                context.SetAuthSession(session);
            }
        }

        await next(context);
    }

    private static string? ReadToken(HttpRequest request)
    {
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        var tokenHeader = request.Headers["X-Mozaika-Token"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tokenHeader))
        {
            return tokenHeader.Trim();
        }

        return null;
    }
}
