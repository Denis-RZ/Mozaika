using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Database.Entities;
using Mozaika.Api.Options;
using Mozaika.Api.Security;
using Mozaika.Api.Services;
using System.Data;

namespace Mozaika.Api.Database;

public static class DbInitializer
{
    public static async Task SeedAsync(
        MozaikaDbContext dbContext,
        AuthOptions? authOptions = null,
        bool seedDefaults = true
    )
    {
        await dbContext.Database.EnsureCreatedAsync();
        await EnsureIncrementalSchemaAsync(dbContext);
        if (!seedDefaults)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        var settings = await dbContext.AppSettings.FindAsync(1);
        if (settings is null)
        {
            settings = new AppSettingEntity
            {
                Id = 1,
                DefaultFieldWidthMm = 1200,
                DefaultFieldHeightMm = 800,
                DefaultCellSizeMm = 10,
                DefaultGapMm = 2,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            };
            dbContext.AppSettings.Add(settings);
        }

        if (!await dbContext.GroutColors.AnyAsync())
        {
            dbContext.GroutColors.AddRange(
                new GroutColorEntity
                {
                    Name = "Warm White",
                    RgbHex = "#F2EFEA",
                    IsActive = true,
                    CreatedAt = utcNow,
                },
                new GroutColorEntity
                {
                    Name = "Graphite",
                    RgbHex = "#353535",
                    IsActive = true,
                    CreatedAt = utcNow,
                },
                new GroutColorEntity
                {
                    Name = "Sand",
                    RgbHex = "#CDBB9C",
                    IsActive = true,
                    CreatedAt = utcNow,
                }
            );
        }

        await SeedUsersAsync(dbContext, authOptions, utcNow);
        await dbContext.SaveChangesAsync();
    }

    private static async Task EnsureIncrementalSchemaAsync(MozaikaDbContext dbContext)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        var commands = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL,
                display_name TEXT NOT NULL,
                password_hash TEXT NOT NULL,
                password_salt TEXT NOT NULL,
                password_iterations INTEGER NOT NULL,
                role TEXT NOT NULL,
                is_active INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_users_username ON users(username);",
            "CREATE INDEX IF NOT EXISTS IX_users_role ON users(role);",
            """
            CREATE TABLE IF NOT EXISTS auth_sessions (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                token_hash TEXT NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                last_seen_at TEXT NOT NULL,
                is_revoked INTEGER NOT NULL,
                FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_auth_sessions_token_hash ON auth_sessions(token_hash);",
            "CREATE INDEX IF NOT EXISTS IX_auth_sessions_expires_at ON auth_sessions(expires_at);",
            """
            CREATE TABLE IF NOT EXISTS projects (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                description TEXT NULL,
                source_image_mime_type TEXT NULL,
                source_image_bytes BLOB NULL,
                active_generation_id INTEGER NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS IX_projects_updated_at ON projects(updated_at);",
            "CREATE INDEX IF NOT EXISTS IX_projects_active_generation_id ON projects(active_generation_id);",
            """
            CREATE TABLE IF NOT EXISTS project_generations (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                project_id INTEGER NOT NULL,
                version INTEGER NOT NULL,
                name TEXT NOT NULL,
                note TEXT NULL,
                snapshot_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_project_generations_project_id_version ON project_generations(project_id, version);",
            "CREATE INDEX IF NOT EXISTS IX_project_generations_created_at ON project_generations(created_at);",
            """
            CREATE TABLE IF NOT EXISTS project_shares (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                project_id INTEGER NOT NULL,
                generation_id INTEGER NOT NULL,
                generation_version INTEGER NOT NULL,
                generation_name TEXT NOT NULL,
                token TEXT NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT NULL,
                is_revoked INTEGER NOT NULL,
                created_by TEXT NULL,
                FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE,
                FOREIGN KEY(generation_id) REFERENCES project_generations(id) ON DELETE RESTRICT
            );
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_project_shares_token ON project_shares(token);",
            "CREATE INDEX IF NOT EXISTS IX_project_shares_project_id_created_at ON project_shares(project_id, created_at);",
            """
            CREATE TABLE IF NOT EXISTS project_orders (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                project_id INTEGER NOT NULL,
                generation_id INTEGER NOT NULL,
                generation_version INTEGER NOT NULL,
                generation_name TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                status TEXT NOT NULL,
                status_comment TEXT NULL,
                customer_name TEXT NOT NULL,
                customer_email TEXT NULL,
                customer_phone TEXT NULL,
                comment TEXT NULL,
                submitted_by TEXT NULL,
                total_price REAL NOT NULL,
                currency TEXT NOT NULL,
                total_chips INTEGER NOT NULL,
                colors_used INTEGER NOT NULL,
                FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE,
                FOREIGN KEY(generation_id) REFERENCES project_generations(id) ON DELETE RESTRICT
            );
            """,
            "CREATE INDEX IF NOT EXISTS IX_project_orders_project_id_created_at ON project_orders(project_id, created_at);",
            "CREATE INDEX IF NOT EXISTS IX_project_orders_status ON project_orders(status);",
            """
            CREATE TABLE IF NOT EXISTS project_order_status_history (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                order_id INTEGER NOT NULL,
                status TEXT NOT NULL,
                status_comment TEXT NULL,
                changed_by TEXT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY(order_id) REFERENCES project_orders(id) ON DELETE CASCADE
            );
            """,
            "CREATE INDEX IF NOT EXISTS IX_project_order_status_history_order_id_created_at ON project_order_status_history(order_id, created_at);",
            // Active generation cleanup trigger avoids stale pointers in sqlite migration mode.
            """
            CREATE TRIGGER IF NOT EXISTS trg_projects_active_generation_cleanup
            AFTER DELETE ON project_generations
            BEGIN
                UPDATE projects
                SET active_generation_id = NULL
                WHERE active_generation_id = OLD.id;
            END;
            """,
        };

        foreach (var command in commands)
        {
            await dbContext.Database.ExecuteSqlRawAsync(command);
        }

        // Legacy sqlite databases may miss newer columns if table already existed before updates.
        await EnsureSqliteColumnAsync(
            dbContext,
            tableName: "projects",
            columnName: "owner_username",
            columnDefinition: "TEXT NULL"
        );
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_projects_owner_username ON projects(owner_username);"
        );
    }

    private static async Task EnsureSqliteColumnAsync(
        MozaikaDbContext dbContext,
        string tableName,
        string columnName,
        string columnDefinition
    )
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"PRAGMA table_info({tableName});";

        var hasColumn = false;
        await using (var reader = await checkCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var existingName = reader["name"]?.ToString();
                if (string.Equals(existingName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (hasColumn)
        {
            return;
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync();
    }

    private static async Task SeedUsersAsync(MozaikaDbContext dbContext, AuthOptions? authOptions, DateTime utcNow)
    {
        var configuredUsers = (authOptions?.Users ?? [])
            .Where(item => item.IsActive)
            .Select(item => new
            {
                Username = item.Username.Trim().ToLowerInvariant(),
                Password = item.Password.Trim(),
                DisplayName = item.DisplayName.Trim(),
                Role = NormalizeRole(item.Role),
            })
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Username) &&
                !string.IsNullOrWhiteSpace(item.Password) &&
                !string.IsNullOrWhiteSpace(item.DisplayName))
            .ToList();

        if (configuredUsers.Count == 0)
        {
            configuredUsers =
            [
                new
                {
                    Username = "admin",
                    Password = "admin123",
                    DisplayName = "Administrator",
                    Role = AppRoles.Admin,
                },
                new
                {
                    Username = "customer",
                    Password = "customer123",
                    DisplayName = "Customer",
                    Role = AppRoles.Customer,
                },
                new
                {
                    Username = "viewer",
                    Password = "viewer123",
                    DisplayName = "Viewer",
                    Role = AppRoles.Viewer,
                },
            ];
        }

        foreach (var user in configuredUsers)
        {
            var existing = await dbContext.Users.FirstOrDefaultAsync(item => item.Username == user.Username);
            var password = PasswordHashing.CreateHash(user.Password, PasswordHashing.DefaultIterations);

            if (existing is null)
            {
                dbContext.Users.Add(new UserEntity
                {
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    PasswordHash = password.HashBase64,
                    PasswordSalt = password.SaltBase64,
                    PasswordIterations = password.Iterations,
                    Role = user.Role,
                    IsActive = true,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow,
                });
                continue;
            }

            var changed = false;
            if (!string.Equals(existing.DisplayName, user.DisplayName, StringComparison.Ordinal))
            {
                existing.DisplayName = user.DisplayName;
                changed = true;
            }

            if (!string.Equals(existing.Role, user.Role, StringComparison.Ordinal))
            {
                existing.Role = user.Role;
                changed = true;
            }

            if (!existing.IsActive)
            {
                existing.IsActive = true;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existing.PasswordHash) ||
                string.IsNullOrWhiteSpace(existing.PasswordSalt) ||
                !PasswordHashing.Verify(user.Password, existing.PasswordHash, existing.PasswordSalt, existing.PasswordIterations))
            {
                existing.PasswordHash = password.HashBase64;
                existing.PasswordSalt = password.SaltBase64;
                existing.PasswordIterations = password.Iterations;
                changed = true;
            }

            if (changed)
            {
                existing.UpdatedAt = utcNow;
            }
        }
    }

    private static string NormalizeRole(string role)
    {
        var normalized = role.Trim().ToLowerInvariant();
        return AppRoles.IsSupported(normalized) ? normalized : AppRoles.Customer;
    }
}
