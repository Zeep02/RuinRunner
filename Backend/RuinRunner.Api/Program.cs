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
await app.Services.GetRequiredService<CatalogStore>().PublishDirectory(Path.Combine(AppContext.BaseDirectory, "Content"));
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
await app.RunAsync();
