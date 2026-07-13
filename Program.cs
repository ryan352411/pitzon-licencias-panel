using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

const string connectionEnvironmentVariable = "REFACCIONARIA_DB_CONNECTION";
const string adminUserEnvironmentVariable = "LICENSING_ADMIN_USER";
const string adminPasswordEnvironmentVariable = "LICENSING_ADMIN_PASSWORD";
const string sessionCookieName = "licensing_session";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string? connectionString = Environment.GetEnvironmentVariable(connectionEnvironmentVariable);
string adminUser = Environment.GetEnvironmentVariable(adminUserEnvironmentVariable) ?? "admin";
string? adminPassword = Environment.GetEnvironmentVariable(adminPasswordEnvironmentVariable);
ConcurrentDictionary<string, DateTimeOffset> sessions = new();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    databaseConfigured = !string.IsNullOrWhiteSpace(connectionString),
    loginConfigured = !string.IsNullOrWhiteSpace(adminPassword)
}));

app.MapGet("/api/me", (HttpContext context) =>
{
    if (!Authorize(context, sessions))
    {
        return Unauthorized();
    }

    return Results.Ok(new { username = adminUser });
});

app.MapPost("/api/login", async (HttpContext context, LoginRequest request) =>
{
    if (string.IsNullOrWhiteSpace(adminPassword))
    {
        return Results.Json(new { error = "Configura LICENSING_ADMIN_PASSWORD antes de iniciar el panel." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!SecureEquals(request.Username, adminUser) || !SecureEquals(request.Password, adminPassword))
    {
        return Unauthorized();
    }

    string sessionToken = CreateSessionToken();
    sessions[sessionToken] = DateTimeOffset.UtcNow.AddHours(12);

    context.Response.Cookies.Append(sessionCookieName, sessionToken, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = IsHttpsRequest(context),
        Expires = sessions[sessionToken],
        IsEssential = true
    });

    await Task.CompletedTask;
    return Results.Ok(new { username = adminUser });
});

app.MapPost("/api/logout", (HttpContext context) =>
{
    if (context.Request.Cookies.TryGetValue(sessionCookieName, out string? sessionToken))
    {
        sessions.TryRemove(sessionToken, out _);
    }

    context.Response.Cookies.Delete(sessionCookieName);
    return Results.Ok(new { loggedOut = true });
});

app.MapGet("/api/summary", async (HttpContext context) =>
{
    if (!Authorize(context, sessions))
    {
        return Unauthorized();
    }

    await using NpgsqlConnection connection = CreateConnection(connectionString);
    await connection.OpenAsync();

    const string sql = @"
        SELECT app_code,
               COUNT(*) AS total_installations,
               COUNT(*) FILTER (WHERE is_active) AS active_installations,
               COUNT(*) FILTER (WHERE NOT is_active) AS inactive_installations,
               COALESCE(SUM(launch_count), 0) AS total_launches
        FROM app_installations
        GROUP BY app_code
        ORDER BY app_code;";

    List<AppSummary> summaries = new();
    await using NpgsqlCommand command = new(sql, connection);
    await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        summaries.Add(new AppSummary(
            reader.GetString(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4)));
    }

    return Results.Ok(summaries);
});

app.MapGet("/api/installations", async (HttpContext context, string? appCode, string? search) =>
{
    if (!Authorize(context, sessions))
    {
        return Unauthorized();
    }

    await using NpgsqlConnection connection = CreateConnection(connectionString);
    await connection.OpenAsync();

    const string sql = @"
        SELECT installation_id,
               app_code,
               app_name,
               machine_name,
               windows_user,
               COALESCE(app_version, '') AS app_version,
               first_seen_at,
               last_seen_at,
               launch_count,
               is_active,
               deactivated_at,
               COALESCE(notes, '') AS notes
        FROM app_installations
        WHERE (@appCode::text IS NULL OR app_code = @appCode::text)
          AND (
              @search::text IS NULL
              OR machine_name ILIKE @search::text
              OR windows_user ILIKE @search::text
              OR installation_id::text ILIKE @search::text
          )
        ORDER BY last_seen_at DESC;";

    List<InstallationRow> installations = new();
    await using NpgsqlCommand command = new(sql, connection);
    command.Parameters.AddWithValue("@appCode", string.IsNullOrWhiteSpace(appCode) || appCode == "ALL" ? DBNull.Value : appCode);
    command.Parameters.AddWithValue("@search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : $"%{search.Trim()}%");

    await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        installations.Add(new InstallationRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetDateTime(6),
            reader.GetDateTime(7),
            reader.GetInt32(8),
            reader.GetBoolean(9),
            reader.IsDBNull(10) ? null : reader.GetDateTime(10),
            reader.GetString(11)));
    }

    return Results.Ok(installations);
});

app.MapPost("/api/installations/{installationId:guid}/activation", async (HttpContext context, Guid installationId, ActivationRequest request) =>
{
    if (!Authorize(context, sessions))
    {
        return Unauthorized();
    }

    await using NpgsqlConnection connection = CreateConnection(connectionString);
    await connection.OpenAsync();

    const string sql = @"
        UPDATE app_installations
        SET is_active = @isActive,
            deactivated_at = CASE WHEN @isActive THEN NULL ELSE now() END
        WHERE installation_id = @installationId
        RETURNING installation_id;";

    await using NpgsqlCommand command = new(sql, connection);
    command.Parameters.AddWithValue("@isActive", request.IsActive);
    command.Parameters.AddWithValue("@installationId", installationId);

    object? result = await command.ExecuteScalarAsync();
    return result is null ? Results.NotFound() : Results.Ok(new { installationId, request.IsActive });
});

app.MapPost("/api/installations/{installationId:guid}/notes", async (HttpContext context, Guid installationId, NotesRequest request) =>
{
    if (!Authorize(context, sessions))
    {
        return Unauthorized();
    }

    await using NpgsqlConnection connection = CreateConnection(connectionString);
    await connection.OpenAsync();

    const string sql = @"
        UPDATE app_installations
        SET notes = NULLIF(@notes, '')
        WHERE installation_id = @installationId
        RETURNING installation_id;";

    await using NpgsqlCommand command = new(sql, connection);
    command.Parameters.AddWithValue("@notes", request.Notes.Trim());
    command.Parameters.AddWithValue("@installationId", installationId);

    object? result = await command.ExecuteScalarAsync();
    return result is null ? Results.NotFound() : Results.Ok(new { installationId });
});

app.MapGet("/api/events", async (HttpContext context, string? appCode) =>
{
    if (!Authorize(context, sessions))
    {
        return Unauthorized();
    }

    await using NpgsqlConnection connection = CreateConnection(connectionString);
    await connection.OpenAsync();

    const string sql = @"
        SELECT e.id,
               e.installation_id,
               e.app_code,
               e.event_type,
               e.event_at,
               e.machine_name,
               e.windows_user
        FROM app_installation_events e
        WHERE (@appCode::text IS NULL OR e.app_code = @appCode::text)
        ORDER BY e.event_at DESC
        LIMIT 80;";

    List<InstallationEventRow> events = new();
    await using NpgsqlCommand command = new(sql, connection);
    command.Parameters.AddWithValue("@appCode", string.IsNullOrWhiteSpace(appCode) || appCode == "ALL" ? DBNull.Value : appCode);

    await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        events.Add(new InstallationEventRow(
            reader.GetInt64(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDateTime(4),
            reader.GetString(5),
            reader.GetString(6)));
    }

    return Results.Ok(events);
});

app.Run();

static NpgsqlConnection CreateConnection(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Configura REFACCIONARIA_DB_CONNECTION antes de iniciar el panel.");
    }

    return new NpgsqlConnection(connectionString);
}

static bool Authorize(HttpContext context, ConcurrentDictionary<string, DateTimeOffset> sessions)
{
    if (!context.Request.Cookies.TryGetValue(sessionCookieName, out string? sessionToken))
    {
        return false;
    }

    if (!sessions.TryGetValue(sessionToken, out DateTimeOffset expiresAt))
    {
        return false;
    }

    if (expiresAt <= DateTimeOffset.UtcNow)
    {
        sessions.TryRemove(sessionToken, out _);
        return false;
    }

    return true;
}

static string CreateSessionToken()
{
    byte[] bytes = new byte[32];
    using RandomNumberGenerator generator = RandomNumberGenerator.Create();
    generator.GetBytes(bytes);
    return Convert.ToBase64String(bytes);
}

static bool SecureEquals(string provided, string expected)
{
    byte[] providedBytes = Encoding.UTF8.GetBytes(provided);
    byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);

    return providedBytes.Length == expectedBytes.Length
        && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
}

static bool IsHttpsRequest(HttpContext context)
{
    return context.Request.IsHttps
        || context.Request.Headers["X-Forwarded-Proto"].ToString().Equals("https", StringComparison.OrdinalIgnoreCase);
}

static IResult Unauthorized()
{
    return Results.Json(new { error = "Usuario o contrasena invalida." }, statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record LoginRequest(string Username, string Password);
public sealed record ActivationRequest(bool IsActive);
public sealed record NotesRequest(string Notes);
public sealed record AppSummary(string AppCode, long TotalInstallations, long ActiveInstallations, long InactiveInstallations, long TotalLaunches);
public sealed record InstallationRow(Guid InstallationId, string AppCode, string AppName, string MachineName, string WindowsUser, string AppVersion, DateTime FirstSeenAt, DateTime LastSeenAt, int LaunchCount, bool IsActive, DateTime? DeactivatedAt, string Notes);
public sealed record InstallationEventRow(long Id, Guid InstallationId, string AppCode, string EventType, DateTime EventAt, string MachineName, string WindowsUser);
