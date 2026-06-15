using Npgsql;
using Task4.Models;
using Task4.Services;

namespace Task4.Data;

public class AppDb
{
    private readonly string _connectionString;
    private readonly PasswordService _passwordService;

    public AppDb(IConfiguration config, PasswordService passwordService)
    {
        _connectionString = GetConnectionString(config);
        _passwordService = passwordService;
    }

    public async Task InitAsync()
    {
        await using var connection = await OpenConnectionAsync();

        var sql = await File.ReadAllTextAsync("db/init.sql");
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<User> RegisterAsync(RegisterViewModel model)
    {
        await using var connection = await OpenConnectionAsync();

        var user = new User
        {
            Name = model.Name.Trim(),
            Email = model.Email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordService.Hash(model.Password),
            Status = "Unverified",
            ConfirmationToken = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow
        };

        const string sql = """
            INSERT INTO users (name, email, password_hash, status, confirmation_token, created_at)
            VALUES (@name, @email, @password_hash, @status, @confirmation_token, @created_at)
            RETURNING id
            """;

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("name", user.Name);
            command.Parameters.AddWithValue("email", user.Email);
            command.Parameters.AddWithValue("password_hash", user.PasswordHash);
            command.Parameters.AddWithValue("status", user.Status);
            command.Parameters.AddWithValue("confirmation_token", user.ConfirmationToken);
            command.Parameters.AddWithValue("created_at", user.CreatedAt);

            user.Id = (int)(await command.ExecuteScalarAsync() ?? 0);
            return user;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505" && ex.ConstraintName == "ux_users_email")
        {
            throw new DuplicateEmailException();
        }
    }

    public async Task<User?> LoginAsync(LoginViewModel model)
    {
        var email = model.Email.Trim().ToLowerInvariant();
        var user = await GetUserByEmailAsync(email);

        if (user == null || user.Status == "Blocked" || !_passwordService.Verify(model.Password, user.PasswordHash))
        {
            return null;
        }

        await using var connection = await OpenConnectionAsync();

        await using var command = new NpgsqlCommand("UPDATE users SET last_login_at = @now WHERE id = @id", connection);
        command.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("id", user.Id);
        await command.ExecuteNonQueryAsync();

        return user;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = new NpgsqlCommand("SELECT * FROM users WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync() ? ReadUser(reader) : null;
    }

    public async Task<List<User>> GetUsersAsync()
    {
        var users = new List<User>();

        await using var connection = await OpenConnectionAsync();

        await using var command = new NpgsqlCommand("""
            SELECT *
            FROM users
            ORDER BY last_login_at DESC NULLS LAST, created_at DESC
            """, connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            users.Add(ReadUser(reader));
        }

        return users;
    }

    public async Task<bool> HasUnverifiedUsersAsync()
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM users WHERE status = 'Unverified')", connection);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    public async Task<int> BlockAsync(int[] ids)
    {
        return await ExecuteByIdsAsync("UPDATE users SET status = 'Blocked' WHERE id = ANY(@ids)", ids);
    }

    public async Task<int> UnblockAsync(int[] ids)
    {
        return await ExecuteByIdsAsync("UPDATE users SET status = 'Active' WHERE id = ANY(@ids)", ids);
    }

    public async Task<int> DeleteAsync(int[] ids)
    {
        return await ExecuteByIdsAsync("DELETE FROM users WHERE id = ANY(@ids)", ids);
    }

    public async Task<bool> IsUnverifiedAsync(int id)
    {
        var user = await GetUserByIdAsync(id);
        return user?.Status == "Unverified";
    }

    public async Task<int> DeleteUnverifiedAsync()
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = new NpgsqlCommand("DELETE FROM users WHERE status = 'Unverified'", connection);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> ConfirmAsync(string token)
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = new NpgsqlCommand("""
            UPDATE users
            SET status = CASE WHEN status = 'Blocked' THEN 'Blocked' ELSE 'Active' END
            WHERE confirmation_token = @token
            """, connection);

        command.Parameters.AddWithValue("token", token);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private async Task<User?> GetUserByEmailAsync(string email)
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = new NpgsqlCommand("SELECT * FROM users WHERE LOWER(email) = @email", connection);
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync() ? ReadUser(reader) : null;
    }

    private async Task<int> ExecuteByIdsAsync(string sql, int[] ids)
    {
        if (ids.Length == 0)
        {
            return 0;
        }

        await using var connection = await OpenConnectionAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ids", ids);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await connection.OpenAsync();
                return connection;
            }
            catch when (attempt < 10)
            {
                await Task.Delay(1000);
            }
        }

        await connection.OpenAsync();
        return connection;
    }

    private static User ReadUser(NpgsqlDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Email = reader.GetString(reader.GetOrdinal("email")),
            PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
            Status = reader.GetString(reader.GetOrdinal("status")),
            ConfirmationToken = reader.GetString(reader.GetOrdinal("confirmation_token")),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
            LastLoginAt = reader.IsDBNull(reader.GetOrdinal("last_login_at"))
                ? null
                : reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("last_login_at"))
        };
    }

    private static string GetConnectionString(IConfiguration config)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            return config.GetConnectionString("Default") ?? "";
        }

        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port == -1 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            SslMode = SslMode.Require
        }.ConnectionString;
    }
}
