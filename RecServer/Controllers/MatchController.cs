using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RecServer.Models;
using RecServer.Services;

namespace RecServer.Controllers;

/// <summary>
/// Matchmaking, host <c>match.rec.net</c>: putting the player into a room instance and reporting where they are. Offline there is
/// only ever one player, so every <c>goto</c> creates a fresh private instance. Login/logout stay with the startup chain.
/// Paths and shapes follow older RecNet matchmaking (2021-23); unverified against the 2024 client.
/// </summary>
[ApiController]
[Host("match.rec.net")]
[Tags("Matchmaking (match.rec.net)")]
[Produces("application/json")]
public class MatchController : ControllerBase
{
    /// <summary>Not found: no room with that name.</summary>
    public const int ErrorNoSuchRoom = 1;

    private readonly RoomService _rooms;
    private readonly PresenceService _presence;

    public MatchController(RoomService rooms, PresenceService presence) { _rooms = rooms; _presence = presence; }

    /// <summary>Go to a room by name (<c>RecCenter</c>, or <c>RecCenter.SubRoom</c> for a sub-room).</summary>
    [HttpPost("/goto/room/{roomName}")]
    [HttpGet("/goto/room/{roomName}")]
    public MatchmakingResponse GotoRoom(string roomName)
    {
        var parts = roomName.TrimStart('^').Split('.', 2);
        return _rooms.ByName(parts[0]) is { } room ? Enter(room, parts.Length > 1 ? parts[1] : null) : new MatchmakingResponse { ErrorCode = ErrorNoSuchRoom };
    }

    /// <summary>Go to a room by id.</summary>
    [HttpPost("/goto/roomid/{roomId:long}")]
    public MatchmakingResponse GotoRoomId(long roomId) =>
        _rooms.Get(roomId) is { } room ? Enter(room, null) : new MatchmakingResponse { ErrorCode = ErrorNoSuchRoom };

    /// <summary>Re-join the current instance (the only instance that exists).</summary>
    [HttpPost("/goto/roominstance/{instanceId:long}")]
    public MatchmakingResponse GotoInstance(long instanceId) =>
        _presence.Current is { } cur && cur.RoomInstanceId == instanceId ? new MatchmakingResponse { RoomInstance = cur } : GotoDorm();

    /// <summary>"Join player": offline the only player is you, so this lands in your current room or dorm.</summary>
    [HttpPost("/goto/player/{playerId:int}")]
    public MatchmakingResponse GotoPlayer(int playerId) =>
        _presence.Current is { } cur ? new MatchmakingResponse { RoomInstance = cur } : GotoDorm();

    /// <summary>Go home.</summary>
    [HttpPost("/goto/dorm")]
    public MatchmakingResponse GotoDorm() =>
        _rooms.Dorm is { } dorm ? Enter(dorm, null) : new MatchmakingResponse { ErrorCode = ErrorNoSuchRoom };

    /// <summary>Leave the room (back to the title / loading screen).</summary>
    [HttpPost("/goto/none")]
    public MatchmakingResponse GotoNone() { _presence.Enter(null); return new MatchmakingResponse(); }

    /// <summary>Presence keep-alive while in game; returns where the player is.</summary>
    [HttpPost("/player/heartbeat")]
    public PlayerPresence Heartbeat([FromHeader(Name = "X-Rec-Build")] string? build = null)
    {
        _presence.Heartbeat(build);
        return _presence.Get(_presence.LocalId);
    }

    /// <summary>Presence of players (<c>?id=1&amp;id=2</c>).</summary>
    [HttpGet("/player")]
    public IEnumerable<PlayerPresence> Players([FromQuery] int[] id) => id.Select(_presence.Get);

    /// <summary>Presence of one player.</summary>
    [HttpGet("/player/{id:int}")]
    public PlayerPresence Player(int id) => _presence.Get(id);

    /// <summary>The current room instance, if its id matches.</summary>
    [HttpGet("/roominstance/{id:long}")]
    public ActionResult<RoomInstance> Instance(long id) =>
        _presence.Current is { } cur && cur.RoomInstanceId == id ? cur : NotFound();

    private MatchmakingResponse Enter(Room room, string? subRoom)
    {
        var instance = _rooms.Instantiate(room, subRoom);
        _presence.Enter(instance);
        _rooms.Visited(room.RoomId);
        return new MatchmakingResponse { RoomInstance = instance };
    }
}
