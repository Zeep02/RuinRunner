using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using RuinRunner.Api;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 65536);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
var connection = builder.Configuration["RR_DB"] ?? throw new InvalidOperationException("RR_DB must be supplied outside the Unity project.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<CatalogStore>();
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddSingleton<RunService>();
builder.Services.AddSingleton<EconomyService>();
builder.Services.AddAuthentication("Guest").AddScheme<AuthenticationSchemeOptions, GuestAuthentication>("Guest", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected = async (context, cancellation) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { code = "rate_limited", message = "잠시 뒤 다시 연결합니다.", traceId = context.HttpContext.TraceIdentifier }, cancellation);
    };
    o.AddPolicy("admin", context => RateLimitPartition.GetFixedWindowLimiter("admin", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    o.AddPolicy("guest", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
var app = builder.Build();
if (args.Contains("--migrate"))
{
    var migration = builder.Configuration["RR_MIGRATION_DB"] ?? throw new InvalidOperationException("RR_MIGRATION_DB required only for schema migration.");
    await Database.Migrate(migration, Path.Combine(AppContext.BaseDirectory, "Migrations"));
    await app.Services.GetRequiredService<CatalogStore>().PublishDirectory(Path.Combine(AppContext.BaseDirectory, "Content"));
    Console.WriteLine("Schema and immutable catalogs ready.");
    return;
}
var catalogStore = app.Services.GetRequiredService<CatalogStore>();
catalogStore.LoadEnemyTypes(Path.Combine(AppContext.BaseDirectory, "Admin", "enemy-defaults.json"));
await catalogStore.PublishDirectory(Path.Combine(AppContext.BaseDirectory, "Content"));
await catalogStore.RestoreActivation();
var admin = new AdminAccess(builder.Configuration);
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    try
    {
        await next();
    }
    catch (ApiFailure e)
    {
        context.Response.StatusCode = e.Status;
        await context.Response.WriteAsJsonAsync(new { code = e.Code, message = e.Message, traceId = context.TraceIdentifier });
    }
    catch (BadHttpRequestException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { code = "invalid_request", message = "요청을 확인하세요.", traceId = context.TraceIdentifier });
    }
    catch (JsonException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { code = "invalid_json", message = "요청을 확인하세요.", traceId = context.TraceIdentifier });
    }
    catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { code = "conflict", message = "이미 처리된 변경입니다.", traceId = context.TraceIdentifier });
    }
    catch (NpgsqlException e)
    {
        app.Logger.LogWarning("Database unavailable; type={Type}, trace={Trace}", e.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new { code = "storage_unavailable", message = "저장을 확인하지 못했습니다. 다시 연결합니다.", traceId = context.TraceIdentifier });
    }
    catch (Exception e)
    {
        app.Logger.LogError("Request failed; type={Type}, trace={Trace}", e.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { code = "request_failed", message = "처리를 확인하지 못했습니다. 다시 연결하세요.", traceId = context.TraceIdentifier });
    }
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "live", apiVersion = "1" }));
app.MapGet("/health/ready", async (Database db) =>
{
    await using var c = await db.Source.OpenConnectionAsync();
    await Database.Scalar(c, "SELECT 1");
    return Results.Ok(new { status = "ready" });
});
app.MapPost("/v1/auth/guest", (GuestRequest r, Database db) => GuestCredentials.SignIn(db, r)).RequireRateLimiting("guest");
var api = app.MapGroup("/v1").RequireAuthorization();
static Guid Player(HttpContext c) => Guid.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
static IResult Json(string json) => Results.Content(json, "application/json; charset=utf-8");
api.MapGet("/profiles", (HttpContext c, ProfileService s) => s.List(Player(c)));
api.MapPost("/profiles", async (HttpContext c, ProfileService s, CreateProfileRequest r) => Json(await s.Create(Player(c), r)));
api.MapGet("/profiles/{id:guid}", (HttpContext c, ProfileService s, Guid id) => s.Get(Player(c), id));
api.MapGet("/profiles/{id:guid}/active-run", (HttpContext c, RunService s, Guid id) => s.Active(Player(c), id));
api.MapPut("/profiles/{id:guid}/configuration", async (HttpContext c, ProfileService s, Guid id, ConfigurationRequest r) => Json(await s.Configure(Player(c), id, r)));
api.MapPost("/profiles/{id:guid}/unlocks", async (HttpContext c, ProfileService s, Guid id, UnlockRequest r) => Json(await s.Unlock(Player(c), id, r)));
api.MapPost("/profiles/{id:guid}/legacy-import", async (HttpContext c, ProfileService s, Guid id, ImportRequest r) => Json(await s.Import(Player(c), id, r)));
api.MapPost("/profiles/{id:guid}/reset-challenge", async (HttpContext c, ProfileService s, Guid id, BeginProfileResetRequest r) => Json(await s.BeginReset(Player(c), id, r)));
api.MapPost("/profiles/{id:guid}/reset", async (HttpContext c, ProfileService s, Guid id, ConfirmProfileResetRequest r) => Json(await s.ConfirmReset(Player(c), id, r)));
api.MapPost("/runs", async (HttpContext c, RunService s, StartRunRequest r) => Json(await s.Start(Player(c), r)));
api.MapGet("/runs/{id:guid}", (HttpContext c, RunService s, Guid id) => s.Get(Player(c), id));
api.MapPost("/runs/{id:guid}/settle", async (HttpContext c, RunService s, Guid id, SettleRequest r) => Json(await s.Settle(Player(c), id, r)));
api.MapGet("/runs/{id:guid}/economy", (HttpContext c, EconomyService s, Guid id) => s.Get(Player(c), id));
api.MapPost("/runs/{id:guid}/trades", async (HttpContext c, EconomyService s, Guid id, TradeRequest r) => Json(await s.Trade(Player(c), id, r)));
api.MapPost("/runs/{id:guid}/rewards", async (HttpContext c, EconomyService s, Guid id, RewardRequest r) => Json(await s.Reward(Player(c), id, r)));
api.MapGet("/catalogs/current", async (Database db, CatalogStore s) =>
{
    await using var c = await db.Source.OpenConnectionAsync();
    return await s.Get(c, s.CurrentVersion);
});
api.MapGet("/catalogs/{version}", async (Database db, CatalogStore s, string version) =>
{
    await using var c = await db.Source.OpenConnectionAsync();
    return await s.Get(c, version);
});
// Admin tool: a page and API on the same origin, off unless RR_ADMIN_TOKEN is set, loopback-only unless RR_ADMIN_ALLOW_REMOTE=1.
app.MapGet("/admin", (HttpContext c) =>
{
    if (!admin.Enabled || !admin.AddressAllowed(c))
        return Results.NotFound();
    c.Response.Headers["X-Frame-Options"] = "DENY";
    c.Response.Headers["Referrer-Policy"] = "no-referrer";
    return Results.File(Path.Combine(AppContext.BaseDirectory, "Admin", "admin.html"), "text/html; charset=utf-8");
});
var adminApi = app.MapGroup("/admin/api").AddEndpointFilter(async (context, next) =>
{
    var failure = admin.Check(context.HttpContext);
    return failure ?? await next(context);
}).RequireRateLimiting("admin");
static string Remote(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "";
adminApi.MapGet("/state", async (Database db, CatalogStore s) =>
{
    await using var c = await db.Source.OpenConnectionAsync();
    var versions = new List<object>();
    await using (var cmd = Database.Command(c, "SELECT k.version, k.published_utc, h.base_version, h.note FROM content_catalogs k LEFT JOIN admin_catalog_history h ON h.version=k.version WHERE k.payload ? 'economy' ORDER BY k.published_utc DESC, k.version DESC"))
    await using (var reader = await cmd.ExecuteReaderAsync())
        while (await reader.ReadAsync())
            versions.Add(new { version = reader.GetString(0), publishedUtc = reader.GetDateTime(1), baseVersion = reader.IsDBNull(2) ? null : reader.GetString(2), note = reader.IsDBNull(3) ? null : reader.GetString(3) });
    return Results.Ok(new { current = s.CurrentVersion, configured = s.ConfiguredVersion, versions });
});
adminApi.MapGet("/catalogs/{version}", async (Database db, CatalogStore s, string version) =>
{
    await using var c = await db.Source.OpenConnectionAsync();
    return Results.Json(await s.Get(c, version), Wire.CatalogJson);
});
adminApi.MapGet("/accessory-rules/{version}", async (Database db, CatalogStore s, string version) =>
{
    await using var c = await db.Source.OpenConnectionAsync();
    var result = new Dictionary<string, Dictionary<string, object>>();
    foreach (var item in (await s.Get(c, version)).Items ?? [])
    {
        if (item.Kind != "accessory")
            continue;
        try
        {
            result[item.Id] = RuinRunner.Backend.AccessoryTuningSchema.Rules(item.Id).ToDictionary(r => r.Key, r => (object)new { min = r.Value.Minimum, max = r.Value.Maximum, integer = r.Value.Integer });
        }
        catch (ArgumentException)
        {
        }
    }
    return Results.Ok(result);
});
adminApi.MapGet("/enemy-defaults", () => Results.File(Path.Combine(AppContext.BaseDirectory, "Admin", "enemy-defaults.json"), "application/json; charset=utf-8"));
adminApi.MapPost("/catalogs", async (HttpContext c, CatalogStore s) =>
{
    var request = await JsonSerializer.DeserializeAsync<AdminPublishRequest>(c.Request.Body, Wire.Json) ?? throw new ApiFailure(400, "invalid_json", "요청을 읽을 수 없습니다.");
    var version = await s.PublishAdmin(request, Remote(c));
    app.Logger.LogInformation("Admin published catalog {Version}", version);
    return Results.Ok(new { version, current = s.CurrentVersion });
});
adminApi.MapPost("/activate", async (HttpContext c, CatalogStore s, AdminActivateRequest r) =>
{
    await s.ActivateAdmin(r.Version, Remote(c));
    app.Logger.LogInformation("Admin activated catalog {Version}", r.Version);
    return Results.Ok(new { current = s.CurrentVersion });
});
await app.RunAsync();
