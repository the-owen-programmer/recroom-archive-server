using Microsoft.AspNetCore.Mvc;
using RecServer.Infrastructure;

namespace RecServer.Controllers;

/// <summary>Development aids, not part of the real RecNet.</summary>
[ApiController]
[Route("_debug")]
[Tags("Debug")]
public class DebugController : ControllerBase
{
    private readonly RequestLog _log;
    public DebugController(RequestLog log) => _log = log;

    /// <summary>The most recent requests the server saw (also appended to logs/requests.jsonl).</summary>
    [HttpGet("requests")]
    public IReadOnlyList<LoggedRequest> Requests([FromQuery] int take = 50) => _log.Recent(take);
}
