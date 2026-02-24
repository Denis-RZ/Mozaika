namespace Mozaika.Api.Security;

public static class AppRoles
{
    public const string Admin = "admin";
    public const string Customer = "customer";
    public const string Viewer = "viewer";

    public static readonly IReadOnlyList<string> All = [Admin, Customer, Viewer];

    public static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        All.Contains(value.Trim().ToLowerInvariant());

    public static bool HasAccess(string? userRole, params string[] allowedRoles)
    {
        if (string.IsNullOrWhiteSpace(userRole) || allowedRoles.Length == 0)
        {
            return false;
        }

        var normalizedRole = userRole.Trim().ToLowerInvariant();
        return allowedRoles.Any(role =>
            string.Equals(normalizedRole, role.Trim().ToLowerInvariant(), StringComparison.Ordinal));
    }
}
