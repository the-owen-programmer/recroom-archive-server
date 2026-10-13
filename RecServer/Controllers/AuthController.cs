using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RecServer.Models;

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
}
