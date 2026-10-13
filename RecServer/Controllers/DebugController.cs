using Microsoft.AspNetCore.Mvc;
using RecServer.Infrastructure;
using RecServer.Services;

namespace RecServer.Controllers;

/// <summary>Development aids, not part of the real RecNet.</summary>
[ApiController]
[Route("_debug")]
[Tags("Debug")]
public class DebugController : ControllerBase
{
    private readonly RequestLog _log;
    private readonly RoomService _rooms;
    public DebugController(RequestLog log, RoomService rooms) { _log = log; _rooms = rooms; }

    /// <summary>The most recent requests the server saw (also appended to logs/requests.jsonl).</summary>
    [HttpGet("requests")]
    public IReadOnlyList<LoggedRequest> Requests([FromQuery] int take = 50) => _log.Recent(take);

    /// <summary>Re-read Seed/rooms.json and data/rooms/*.json (after tools/import_rooms.py) without restarting.</summary>
    [HttpPost("rooms/reload")]
    public object ReloadRooms() { _rooms.Reload(); return new { rooms = _rooms.Count }; }
}
