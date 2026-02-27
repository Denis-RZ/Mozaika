using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Database.Entities;
using Mozaika.Api.Options;
using Mozaika.Api.Security;

namespace Mozaika.Api.Services;

public sealed class AuthException : Exception
{
    public int StatusCode { get; }

    public AuthException(string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed class AuthSessionInfo
{
    public required int UserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required bool RequiresPasswordChange { get; init; }
}

public sealed class AuthService
{
    private readonly MozaikaDbContext _dbContext;
    private readonly TimeSpan _sessionTtl;
    private readonly string? _defaultAdminUsername;
    private readonly string? _defaultAdminPassword;

    public AuthService(MozaikaDbContext dbContext, IOptions<AuthOptions> authOptionsAccessor)
    {
        _dbContext = dbContext;
        var authOptions = authOptionsAccessor.Value ?? new AuthOptions();
        _sessionTtl = TimeSpan.FromHours(Math.Clamp(authOptions.SessionHours, 1, 24 * 30));
        var defaultAdmin = AuthBootstrapUsers.ResolveDefaultAdmin(authOptions);
        _defaultAdminUsername = defaultAdmin?.Username;
        _defaultAdminPassword = defaultAdmin?.Password;
    }

    public async Task<(string Token, AuthSessionInfo Session)> LoginAsync(string username, string password)
    {
        var normalizedLogin = NormalizeLogin(username);
        var normalizedPassword = password.Trim();

        if (string.IsNullOrWhiteSpace(normalizedLogin) || string.IsNullOrWhiteSpace(normalizedPassword))
        {
            throw new AuthException("Введите логин и пароль.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.Username == normalizedLogin &&
                item.IsActive);

        if (user is null ||
            !PasswordHashing.Verify(
                normalizedPassword,
                user.PasswordHash,
                user.PasswordSalt,
                user.PasswordIterations))
        {
            throw new AuthException("Неверный логин или пароль.", StatusCodes.Status401Unauthorized);
        }

        var utcNow = DateTime.UtcNow;
        var expiresAt = utcNow.Add(_sessionTtl);
        var token = GenerateToken();
        var tokenHash = ComputeTokenHash(token);

        await CleanupExpiredSessionsAsync(utcNow);

        _dbContext.AuthSessions.Add(new AuthSessionEntity
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = utcNow,
            ExpiresAt = expiresAt,
            LastSeenAt = utcNow,
            IsRevoked = false,
        });

        await _dbContext.SaveChangesAsync();
        return (token, BuildSessionInfo(user, expiresAt));
    }

    public async Task<AuthSessionInfo?> TryGetSessionAsync(string token)
    {
        var normalizedToken = token.Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return null;
        }

        var utcNow = DateTime.UtcNow;
        var tokenHash = ComputeTokenHash(normalizedToken);

        var dbSession = await _dbContext.AuthSessions
            .Include(item => item.User)
            .FirstOrDefaultAsync(item =>
                item.TokenHash == tokenHash &&
                !item.IsRevoked &&
                item.ExpiresAt > utcNow &&
                item.User.IsActive);

        if (dbSession is null)
        {
            return null;
        }

        if ((utcNow - dbSession.LastSeenAt) >= TimeSpan.FromMinutes(1))
        {
            dbSession.LastSeenAt = utcNow;
            await _dbContext.SaveChangesAsync();
        }

        return BuildSessionInfo(dbSession.User, dbSession.ExpiresAt);
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var current = currentPassword.Trim();
        var next = newPassword.Trim();

        if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(next))
        {
            throw new AuthException("Укажите текущий и новый пароль.");
        }

        if (next.Length < 8)
        {
            throw new AuthException("Новый пароль должен содержать минимум 8 символов.");
        }

        if (string.Equals(current, next, StringComparison.Ordinal))
        {
            throw new AuthException("Новый пароль должен отличаться от текущего.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId && item.IsActive);
        if (user is null)
        {
            throw new AuthException("Пользователь не найден.", StatusCodes.Status401Unauthorized);
        }

        var currentIsValid = PasswordHashing.Verify(
            current,
            user.PasswordHash,
            user.PasswordSalt,
            user.PasswordIterations
        );

        if (!currentIsValid)
        {
            throw new AuthException("Текущий пароль указан неверно.", StatusCodes.Status401Unauthorized);
        }

        var nextPassword = PasswordHashing.CreateHash(next, PasswordHashing.DefaultIterations);
        user.PasswordHash = nextPassword.HashBase64;
        user.PasswordSalt = nextPassword.SaltBase64;
        user.PasswordIterations = nextPassword.Iterations;
        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    public async Task LogoutAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var tokenHash = ComputeTokenHash(token.Trim());
        var session = await _dbContext.AuthSessions.FirstOrDefaultAsync(item => item.TokenHash == tokenHash);
        if (session is null || session.IsRevoked)
        {
            return;
        }

        session.IsRevoked = true;
        session.LastSeenAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public AuthSessionReadResponse BuildSessionResponse(AuthSessionInfo? session) => new()
    {
        IsAuthenticated = session is not null,
        ExpiresAt = session?.ExpiresAt,
        RequiresPasswordChange = session?.RequiresPasswordChange ?? false,
        User = session is null ? null : new AuthUserReadResponse
        {
            Id = session.UserId,
            Username = session.Username,
            DisplayName = session.DisplayName,
            Role = session.Role,
            RequiresPasswordChange = session.RequiresPasswordChange,
        },
    };

    private async Task CleanupExpiredSessionsAsync(DateTime utcNow)
    {
        try
        {
            await _dbContext.AuthSessions
                .Where(item => item.ExpiresAt <= utcNow || item.IsRevoked)
                .ExecuteDeleteAsync();
        }
        catch
        {
            // Fallback for providers that do not support ExecuteDelete.
            var expiredSessions = await _dbContext.AuthSessions
                .Where(item => item.ExpiresAt <= utcNow || item.IsRevoked)
                .ToListAsync();
            if (expiredSessions.Count == 0)
            {
                return;
            }

            _dbContext.AuthSessions.RemoveRange(expiredSessions);
            await _dbContext.SaveChangesAsync();
        }
    }

    private AuthSessionInfo BuildSessionInfo(UserEntity user, DateTime expiresAt) => new()
    {
        UserId = user.Id,
        Username = user.Username,
        DisplayName = user.DisplayName,
        Role = user.Role,
        ExpiresAt = expiresAt,
        RequiresPasswordChange = RequiresPasswordChange(user),
    };

    private bool RequiresPasswordChange(UserEntity user)
    {
        if (!string.Equals(user.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_defaultAdminUsername) || string.IsNullOrWhiteSpace(_defaultAdminPassword))
        {
            return false;
        }

        if (!string.Equals(NormalizeLogin(user.Username), _defaultAdminUsername, StringComparison.Ordinal))
        {
            return false;
        }

        return PasswordHashing.Verify(
            _defaultAdminPassword,
            user.PasswordHash,
            user.PasswordSalt,
            user.PasswordIterations
        );
    }

    private static string NormalizeLogin(string value) => value.Trim().ToLowerInvariant();

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string ComputeTokenHash(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
