using Mozaika.Api.Options;

namespace Mozaika.Api.Security;

public sealed record ResolvedBootstrapUser(
    string Username,
    string Password,
    string DisplayName,
    string Role
);

public static class AuthBootstrapUsers
{
    public const string AdminPasswordEnv = "MOZAIKA_AUTH_ADMIN_PASSWORD";
    public const string CustomerPasswordEnv = "MOZAIKA_AUTH_CUSTOMER_PASSWORD";
    public const string ViewerPasswordEnv = "MOZAIKA_AUTH_VIEWER_PASSWORD";

    public static IReadOnlyList<ResolvedBootstrapUser> Resolve(AuthOptions? options)
    {
        var resolved = ResolveConfiguredUsers(options);
        if (resolved.Count > 0)
        {
            return resolved;
        }

        if (options?.AllowInsecureFallbackPasswords != true)
        {
            return [];
        }

        return
        [
            new ResolvedBootstrapUser(
                "admin",
                ResolveEnvOrFallback(AdminPasswordEnv, "admin123"),
                "Administrator",
                AppRoles.Admin
            ),
            new ResolvedBootstrapUser(
                "customer",
                ResolveEnvOrFallback(CustomerPasswordEnv, "customer123"),
                "Customer",
                AppRoles.Customer
            ),
            new ResolvedBootstrapUser(
                "viewer",
                ResolveEnvOrFallback(ViewerPasswordEnv, "viewer123"),
                "Viewer",
                AppRoles.Viewer
            ),
        ];
    }

    public static ResolvedBootstrapUser? ResolveDefaultAdmin(AuthOptions? options)
    {
        return Resolve(options)
            .FirstOrDefault(item =>
                string.Equals(item.Role, AppRoles.Admin, StringComparison.Ordinal) &&
                string.Equals(item.Username, "admin", StringComparison.Ordinal));
    }

    private static List<ResolvedBootstrapUser> ResolveConfiguredUsers(AuthOptions? options)
    {
        var result = new List<ResolvedBootstrapUser>();
        var seenUsernames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in options?.Users ?? [])
        {
            if (!item.IsActive)
            {
                continue;
            }

            var username = NormalizeUsername(item.Username);
            if (string.IsNullOrWhiteSpace(username) || seenUsernames.Contains(username))
            {
                continue;
            }

            var role = NormalizeRole(item.Role);
            var password = ResolvePassword(item, username, role);
            if (string.IsNullOrWhiteSpace(password))
            {
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(item.DisplayName)
                ? username
                : item.DisplayName.Trim();

            result.Add(new ResolvedBootstrapUser(username, password, displayName, role));
            seenUsernames.Add(username);
        }

        return result;
    }

    private static string ResolvePassword(AuthUserOptions user, string username, string role)
    {
        if (!string.IsNullOrWhiteSpace(user.Password))
        {
            return user.Password.Trim();
        }

        var envName = string.IsNullOrWhiteSpace(user.PasswordEnv)
            ? ResolveDefaultPasswordEnv(username, role)
            : user.PasswordEnv.Trim();

        if (string.IsNullOrWhiteSpace(envName))
        {
            return string.Empty;
        }

        return Environment.GetEnvironmentVariable(envName)?.Trim() ?? string.Empty;
    }

    private static string ResolveDefaultPasswordEnv(string username, string role)
    {
        if (string.Equals(role, AppRoles.Admin, StringComparison.Ordinal) &&
            string.Equals(username, "admin", StringComparison.Ordinal))
        {
            return AdminPasswordEnv;
        }

        if (string.Equals(role, AppRoles.Customer, StringComparison.Ordinal) &&
            string.Equals(username, "customer", StringComparison.Ordinal))
        {
            return CustomerPasswordEnv;
        }

        if (string.Equals(role, AppRoles.Viewer, StringComparison.Ordinal) &&
            string.Equals(username, "viewer", StringComparison.Ordinal))
        {
            return ViewerPasswordEnv;
        }

        return string.Empty;
    }

    private static string ResolveEnvOrFallback(string envName, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(envName)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string NormalizeRole(string? role)
    {
        var normalized = role?.Trim().ToLowerInvariant() ?? string.Empty;
        return AppRoles.IsSupported(normalized) ? normalized : AppRoles.Customer;
    }

    private static string NormalizeUsername(string? username) =>
        username?.Trim().ToLowerInvariant() ?? string.Empty;
}
