using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace RuinRunner.Api;

public static class GuestCredentials
{
    public static byte[] Decode(string? value)
    {
        if (value == null || value.Length != 43)
            throw new ApiFailure(401, "credential_invalid", "게스트 인증 정보를 확인하세요.");
        try
        {
            var bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + "=");
            if (bytes.Length == 32)
                return bytes;
        }
        catch (FormatException) { }
        throw new ApiFailure(401, "credential_invalid", "게스트 인증 정보를 확인하세요.");
    }
    public static async Task<GuestResponse> SignIn(Database db, GuestRequest request)
    {
        Wire.Require(request.InstallationId != Guid.Empty, "installation_required", "설치 식별자가 필요합니다.");
        var hash = SHA256.HashData(Decode(request.GuestSecret));
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await Database.Exec(c, "INSERT INTO players(id,installation_id,guest_secret_hash) VALUES($1,$2,$3) ON CONFLICT(installation_id) DO NOTHING", Guid.NewGuid(), request.InstallationId, hash);
        Guid player;
        await using (var cmd = Database.Command(c, "SELECT id,guest_secret_hash FROM players WHERE installation_id=$1", request.InstallationId))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync() || !CryptographicOperations.FixedTimeEquals(hash, (byte[])r[1]))
                throw new ApiFailure(401, "credential_invalid", "게스트 인증 정보를 확인하세요.");
            player = r.GetGuid(0);
        }
        var token = RandomNumberGenerator.GetBytes(32);
        var expires = DateTimeOffset.UtcNow.AddHours(2);
        await Database.Exec(c, "DELETE FROM sessions WHERE player_id=$1 AND expires_utc<now()", player);
        await Database.Exec(c, "INSERT INTO sessions(token_hash,player_id,expires_utc) VALUES($1,$2,$3)", SHA256.HashData(token), player, expires);
        await tx.CommitAsync();
        return new(player, Convert.ToBase64String(token).TrimEnd('=').Replace('+', '-').Replace('/', '_'), expires);
    }
}
public sealed class GuestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, Database db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();
        byte[] bytes;
        try
        {
            bytes = GuestCredentials.Decode(header[7..]);
        }
        catch (ApiFailure)
        {
            return AuthenticateResult.Fail("Invalid bearer");
        }
        await using var c = await db.Source.OpenConnectionAsync();
        var player = await Database.Scalar(c, "SELECT player_id FROM sessions WHERE token_hash=$1 AND expires_utc>now()", SHA256.HashData(bytes));
        if (player is not Guid id)
            return AuthenticateResult.Fail("Expired or unknown bearer");
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Response.WriteAsJsonAsync(new { code = "authentication_required", message = "다시 연결 중입니다.", traceId = Context.TraceIdentifier });
    }
}
