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

    private static int _eacTry;

    /// <summary>Easy Anti-Cheat handshake: the client asks for a challenge before logging in.</summary>
    /// <remarks>EXPERIMENT: the response shape is unknown ("Malformed Response" for {"Challenge":"..."}), so each request gets the next
    /// candidate shape; the server log shows which one was sent. EAC is not running in the archive build ("EAC runtime inactive").</remarks>
    [HttpGet("/eac/challenge")]
    public IActionResult EacChallenge()
    {
        int n = Interlocked.Increment(ref _eacTry) - 1;
        string b64 = Convert.ToBase64String(new byte[16]);
        Console.WriteLine($"eac/challenge attempt {n} shape {n % 5}");
        return (n % 5) switch
        {
            0 => Content("\"" + b64 + "\"", "application/json"),
            1 => new JsonResult(new { challenge = b64 }),
            2 => Content(b64, "text/plain"),
            3 => new JsonResult(new { Challenge = b64, ChallengeId = 1 }),
            _ => new JsonResult(new[] { b64 }),
        };
    }
}
