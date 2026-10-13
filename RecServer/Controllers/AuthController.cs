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
    /// <remarks>Shape is a guess (an object with a <c>Challenge</c> string; the client has a <c>Challenge</c> property). EAC is not
    /// running in the archive build ("EAC runtime inactive"), so any value is fine and the answer is never checked.</remarks>
    [HttpGet("/eac/challenge")]
    [Produces("application/json")]
    public object EacChallenge() => new { Challenge = Convert.ToBase64String(new byte[32]) };
}
