using Mozaika.Api.Services;

namespace Mozaika.Api.Security;

public static class HttpContextAuthExtensions
{
    private const string AuthSessionKey = "__auth_session";

    public static void SetAuthSession(this HttpContext context, AuthSessionInfo session)
    {
        context.Items[AuthSessionKey] = session;
    }

    public static AuthSessionInfo? GetAuthSession(this HttpContext context)
    {
        if (context.Items.TryGetValue(AuthSessionKey, out var value) &&
            value is AuthSessionInfo session)
        {
            return session;
        }

        return null;
    }
}
