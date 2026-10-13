using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace RecServer.Controllers;

/// <summary>Client version gate, host <c>api.rec.net</c>. Failing it gives "Failed to connect to RecNet (error code: student)".</summary>
[ApiController]
[Host("api.rec.net")]
[Tags("VersionCheck (api.rec.net)")]
public class VersionCheckController : ControllerBase
{
    /// <summary>Is this client version allowed to play? Client query: <c>?v=20240815.01&amp;p=0&amp;pid=0</c>.</summary>
    /// <remarks>
    /// Client type <c>RecNet.VersionCheck.VersionCheckResponse</c>. <c>VersionStatus</c>: 0 ValidForPlay, 1 UpdateRequired.
    /// <c>UpdateNotificationStage</c>: 0 None, 1 Silent, 2 Warn, 3 Prompt, 4 Require. Always valid: the archived build is the only one.
    /// </remarks>
    [HttpGet("/api/versioncheck/v4")]
    [Produces("application/json")]
    public object Check() => new { VersionStatus = 0, UpdateNotificationStage = 0, IsCrossPlayDisabled = false };

    /// <summary>Versions that are "islanded" (kept apart from the main population). None.</summary>
    [HttpGet("/api/versioncheck/islandedversions")]
    [Produces("application/json")]
    public string[] Islanded() => [];
}
