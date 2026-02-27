using System.ComponentModel.DataAnnotations;

namespace Mozaika.Api.Contracts;

public sealed class AuthUserReadResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool RequiresPasswordChange { get; set; }
}

public sealed class AuthSessionReadResponse
{
    public bool IsAuthenticated { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool RequiresPasswordChange { get; set; }
    public AuthUserReadResponse? User { get; set; }
}

public sealed class AuthLoginRequest
{
    [Required(ErrorMessage = "Введите логин.")]
    [MaxLength(120, ErrorMessage = "Логин слишком длинный.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите пароль.")]
    [MaxLength(240, ErrorMessage = "Пароль слишком длинный.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class AuthLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool RequiresPasswordChange { get; set; }
    public AuthUserReadResponse User { get; set; } = new();
}

public sealed class AuthChangePasswordRequest
{
    [Required(ErrorMessage = "Введите текущий пароль.")]
    [MaxLength(240, ErrorMessage = "Текущий пароль слишком длинный.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите новый пароль.")]
    [MaxLength(240, ErrorMessage = "Новый пароль слишком длинный.")]
    public string NewPassword { get; set; } = string.Empty;
}
