using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace RuinRunner.Api;

// One shared admin token from the start script; requests also need a loopback address unless remote admin is opted in.
public sealed class AdminAccess(IConfiguration configuration)
{
    readonly byte[] token = Encoding.UTF8.GetBytes(configuration["RR_ADMIN_TOKEN"] ?? "");
    readonly bool allowRemote = configuration["RR_ADMIN_ALLOW_REMOTE"] == "1";
    public bool Enabled => token.Length >= 32;
    public bool AddressAllowed(HttpContext c) => allowRemote || c.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address);

    public IResult? Check(HttpContext c)
    {
        if (!Enabled || !AddressAllowed(c))
            return Results.NotFound();
        var supplied = Encoding.UTF8.GetBytes(c.Request.Headers["X-Admin-Token"].ToString());
        if (!CryptographicOperations.FixedTimeEquals(supplied, token))
            return Results.Json(new { code = "admin_token", message = "관리자 토큰이 맞지 않습니다." }, statusCode: 401);
        // Whole catalogs exceed the 64 KB player request limit.
        if (c.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
            size.MaxRequestBodySize = 4_000_000;
        return null;
    }
}
