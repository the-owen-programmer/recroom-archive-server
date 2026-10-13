using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RecServer.Models;
using RecServer.Services;

namespace RecServer.Controllers;

/// <summary>
/// Room (world) directory, host <c>rooms.rec.net</c>. Serves the local catalog: built-in rooms from <c>Seed/rooms.json</c> plus
/// archived rooms in <c>data/rooms/*.json</c>. Paths follow the public rooms API (rooms.rec.net / apim.rec.net/public/rooms, 2023-24);
/// the client's exact paths and query parameters (e.g. <c>include</c>) still have to be confirmed from requests.jsonl.
/// </summary>
[ApiController]
[Host("rooms.rec.net")]
[Tags("Rooms (rooms.rec.net)")]
[Produces("application/json")]
public class RoomsController : ControllerBase
{
    private readonly RoomService _rooms;
    private readonly PresenceService _presence;

    public RoomsController(RoomService rooms, PresenceService presence) { _rooms = rooms; _presence = presence; }

    /// <summary>One room by id.</summary>
    [HttpGet("/rooms/{id:long}")]
    public ActionResult<Room> Get(long id) => _rooms.Get(id) is { } r ? r : NotFound();

    /// <summary>One room by name (<c>?name=RecCenter</c>), the lookup <c>^RoomName</c> links use.</summary>
    [HttpGet("/rooms")]
    public ActionResult<Room> ByName([FromQuery] string name) => _rooms.ByName(name) is { } r ? r : NotFound();

    /// <summary>Several rooms by id and/or name (<c>?id=1&amp;id=2&amp;name=X</c>); unknown ones are left out.</summary>
    [HttpGet("/rooms/bulk")]
    public IEnumerable<Room> Bulk([FromQuery] long[] id, [FromQuery] string[] name) =>
        id.Select(_rooms.Get).Concat(name.Select(_rooms.ByName)).OfType<Room>().DistinctBy(r => r.RoomId);

    /// <summary>Bulk lookup with the ids as form fields (<c>id=1&amp;id=2</c>).</summary>
    [HttpPost("/rooms/bulk")]
    [Consumes("application/x-www-form-urlencoded")]
    public IEnumerable<Room> BulkPost([FromForm] long[] id, [FromForm] string[] name) => Bulk(id, name);

    /// <summary>"Hot" list: every public room in the catalog, most visited first.</summary>
    [HttpGet("/rooms/hot")]
    public object Hot([FromQuery] int skip = 0, [FromQuery] int take = 64) => Page(
        _rooms.Public.OrderByDescending(r => r.Stats.VisitCount), skip, take);

    /// <summary>Featured: the RRO (studio-made) rooms, then the rest.</summary>
    [HttpGet("/rooms/featured/current")]
    public object Featured() => new
    {
        FeaturedRoomGroupId = 1, Name = "Archive", StartAt = DateTime.UtcNow.Date, EndAt = DateTime.UtcNow.Date.AddYears(1),
        Rooms = _rooms.Public.OrderByDescending(r => r.IsRRO).Take(20)
            .Select(r => new { r.RoomId, RoomName = r.Name, r.ImageName }),
    };

    /// <summary>Text search over name, description and tags.</summary>
    [HttpGet("/rooms/search")]
    public object Search([FromQuery] string query = "", [FromQuery] int skip = 0, [FromQuery] int take = 64) =>
        Page(_rooms.Search(query).Where(r => !r.IsDorm), skip, take);

    /// <summary>Rooms made by an account.</summary>
    [HttpGet("/rooms/createdby/{accountId:int}")]
    public IEnumerable<Room> CreatedBy(int accountId) => _rooms.CreatedBy(accountId);

    /// <summary>The local player's own rooms (the dorm plus anything listed with their account as creator).</summary>
    [HttpGet("/rooms/createdby/me")]
    [HttpGet("/rooms/ownedby/me")]
    public IEnumerable<Room> Mine() => _rooms.All.Where(r => r.IsDorm || r.CreatorAccountId == _presence.LocalId);

    /// <summary>Rooms the local player favorited (kept in data/state).</summary>
    [HttpGet("/rooms/favoritedby/me")]
    public IEnumerable<Room> Favorites() => _rooms.Favorites;

    /// <summary>Rooms the local player visited, most recent first.</summary>
    [HttpGet("/rooms/visitedby/me")]
    public IEnumerable<Room> Recent() => _rooms.Recent;

    /// <summary>Studio "base" rooms new rooms are created from (the RRO entries of the catalog).</summary>
    [HttpGet("/rooms/base")]
    public IEnumerable<Room> Base() => _rooms.All.Where(r => r.IsRRO && !r.IsDorm);

    /// <summary>The local player's dorm room.</summary>
    [HttpGet("/rooms/dorm")]
    [HttpGet("/rooms/dorm/me")]
    public ActionResult<Room> Dorm() => _rooms.Dorm is { } r ? r : NotFound();

    /// <summary>Whether the local player cheered / favorited this room.</summary>
    [HttpGet("/rooms/{id:long}/interactionby/me")]
    public object Interaction(long id) => new { Cheered = _rooms.IsCheered(id), Favorited = _rooms.IsFavorited(id) };

    /// <summary>Cheer (POST) or un-cheer (DELETE) a room.</summary>
    [HttpPost("/rooms/{id:long}/cheer")]
    [HttpDelete("/rooms/{id:long}/cheer")]
    public IActionResult Cheer(long id) { _rooms.SetCheer(id, HttpMethods.IsPost(Request.Method)); return Ok(); }

    /// <summary>Favorite (POST) or un-favorite (DELETE) a room.</summary>
    [HttpPost("/rooms/{id:long}/favorite")]
    [HttpDelete("/rooms/{id:long}/favorite")]
    public IActionResult Favorite(long id) { _rooms.SetFavorite(id, HttpMethods.IsPost(Request.Method)); return Ok(); }

    /// <summary>Sub-rooms of a room.</summary>
    [HttpGet("/rooms/{id:long}/subrooms")]
    public ActionResult<List<SubRoom>> SubRooms(long id) => _rooms.Get(id) is { } r ? r.SubRooms : NotFound();

    /// <summary>The public API pages lists as <c>{ Results, TotalResults }</c>.</summary>
    private static object Page(IEnumerable<Room> rooms, int skip, int take)
    {
        var list = rooms.ToList();
        return new { Results = list.Skip(skip).Take(Math.Clamp(take, 1, 500)), TotalResults = list.Count };
    }
}
