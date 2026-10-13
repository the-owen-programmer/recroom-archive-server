using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RecServer.Models;
using RecServer.Services;

namespace RecServer.Controllers;

/// <summary>Login service, host <c>auth.rec.net</c>.</summary>
[ApiController]
[Host("auth.rec.net")]
[Tags("Auth (auth.rec.net)")]
public class AuthController : ControllerBase
{
    /// <summary>Cached logins for one platform account (platform 0 = Steam). The local player is returned for any id,
    /// so the fake Steam ticket never has to be verified.</summary>
    [HttpGet("/cachedlogin/forplatformid/{platform:int}/{platformId}")]
    [Produces("application/json")]
    public object[] ForPlatformId(int platform, string platformId) => [LocalPlayer.CachedLogin(platform, platformId)];

    /// <summary>Cached logins for several platform ids at once.</summary>
    [HttpGet("/cachedlogin/forplatformids")]
    [Produces("application/json")]
    public object[] ForPlatformIds() => [LocalPlayer.CachedLogin(0, "76561198000000001")];

    /// <summary>The cached login of the current device.</summary>
    [HttpGet("/cachedlogin/current")]
    [Produces("application/json")]
    public object[] Current() => [LocalPlayer.CachedLogin(0, "76561198000000001")];

    /// <summary>Easy Anti-Cheat handshake: the client asks for a challenge before logging in.</summary>
    /// <remarks>
    /// Verified shape: a bare JSON string (<c>"AAAAAAAAAAAAAAAAAAAAAA=="</c>). An object (<c>{"Challenge":..}</c>, <c>{"challenge":..}</c>)
    /// gives "Malformed Response" and login error code <c>challenge</c>; the bare string moves the failure on to code <c>wrapper</c>
    /// (the EAC native wrapper, which is not available in the archive build: "EAC runtime inactive").
    /// </remarks>
    [HttpGet("/eac/challenge")]
    public IActionResult EacChallenge() => Content("\"" + Convert.ToBase64String(new byte[16]) + "\"", "application/json");

    private static readonly string[] Scopes =
        ["offline_access", "rn", "rn.accounts", "rn.api", "rn.chat", "rn.clubs", "rn.commerce", "rn.match.read", "rn.match.write",
         "rn.notify", "rn.rooms", "rn.storage", "rn.ai"];

    /// <summary>OAuth token endpoint. The client posts form fields: <c>grant_type=cached_login</c>, <c>account_id</c>, <c>platform</c>,
    /// <c>platform_id</c>, <c>device_id</c>, <c>eac_challenge</c>, <c>eac_response</c>, <c>platform_auth</c> (JSON with the Steam ticket), and
    /// built-in client constants. None of it is verified: the local player is always accepted.</summary>
    /// <remarks>Response is the standard OAuth shape (a guess; to be corrected from the client's behaviour).</remarks>
    [HttpPost("/connect/token")]
    [Produces("application/json")]
    public object Token()
    {
        int accountId = int.TryParse(Request.HasFormContentType ? Request.Form["account_id"].ToString() : "", out var a) ? a : LocalPlayer.AccountId;
        return new
        {
            access_token = TokenService.AccessToken(accountId, 3600, Scopes),
            expires_in = 3600,
            token_type = "Bearer",
            refresh_token = LocalPlayer.RefreshToken,
            scope = string.Join(' ', Scopes),
        };
    }
}
