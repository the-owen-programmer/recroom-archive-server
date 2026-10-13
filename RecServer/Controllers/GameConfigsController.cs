using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace RecServer.Controllers;

/// <summary>Remote game configuration (feature switches / tuning values), host <c>api.rec.net</c>.</summary>
[ApiController]
[Host("api.rec.net")]
[Tags("GameConfigs (api.rec.net)")]
public class GameConfigsController : ControllerBase
{
    /// <summary>All game configs. Client type <c>RecNet.GameConfig</c>: Key, Value, ActiveExperiments, StartTime, EndTime.</summary>
    /// <remarks>An empty list means every setting falls back to the value compiled into the client.</remarks>
    [HttpGet("/api/gameconfigs/v1/all")]
    [Produces("application/json")]
    public object[] All() => [];
}
