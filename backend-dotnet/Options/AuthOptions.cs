using Mozaika.Api.Security;

namespace Mozaika.Api.Options;

public sealed class AuthOptions
{
    public int SessionHours { get; set; } = 24;
    public List<AuthUserOptions> Users { get; set; } = [];
}

public sealed class AuthUserOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = AppRoles.Customer;
    public bool IsActive { get; set; } = true;
}
