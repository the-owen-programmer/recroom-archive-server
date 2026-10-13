using System.Text.Json;
using RecServer.Infrastructure;
using RecServer.Models;

namespace RecServer.Services;

/// <summary>
/// The room catalog: built-in rooms from <c>Seed/rooms.json</c> plus archived rooms dropped in <c>data/rooms/*.json</c>
/// (one room object or an array per file; later files win on equal RoomId). Also hands out room instances for matchmaking.
/// </summary>
public class RoomService
{
    private readonly DataStore _data;
    private readonly string _seedFile;
    private Dictionary<long, Room> _rooms = new();
    private long _nextInstance = 1000;
    private readonly object _lock = new();
    private readonly RoomInteractions _interactions;

    public RoomService(DataStore data, string seedFile)
    {
        _data = data;
        _seedFile = seedFile;
        _interactions = data.Load("room-interactions", () => new RoomInteractions());
        Reload();
    }

    public int Count => _rooms.Count;

    /// <summary>Re-read the seed and data/rooms; called at start and from the debug endpoint after adding files.</summary>
    public void Reload()
    {
        var rooms = new Dictionary<long, Room>();
        void Add(JsonElement e, string source)
        {
            if (e.ValueKind == JsonValueKind.Array) { foreach (var x in e.EnumerateArray()) Add(x, source); return; }
            var room = e.Deserialize<Room>(DataStore.Json);
            if (room == null || room.RoomId == 0 || room.Name.Length == 0) { Console.WriteLine($"{source}: room without RoomId/Name skipped"); return; }
            // archived dumps have no sub-rooms: keep the scene info an earlier source (the seed, a hand-made file) had for the
            // same RoomId, or else for a built-in room of the same name (whose seed entry it then replaces)
            if (room.SubRooms.Count == 0)
            {
                if (rooms.TryGetValue(room.RoomId, out var earlier)) room.SubRooms = earlier.SubRooms;
                else if (rooms.Values.FirstOrDefault(r => r.IsRRO && string.Equals(r.Name, room.Name, StringComparison.OrdinalIgnoreCase)) is { } seeded)
                {
                    room.SubRooms = seeded.SubRooms.Select(sr => new SubRoom
                    {
                        Name = sr.Name, UnitySceneId = sr.UnitySceneId, DataBlob = sr.DataBlob, IsSandbox = sr.IsSandbox,
                        MaxPlayers = sr.MaxPlayers, Accessibility = sr.Accessibility, Extra = sr.Extra,
                    }).ToList();
                    room.IsDorm |= seeded.IsDorm;
                    rooms.Remove(seeded.RoomId);
                }
            }
            Normalize(room);
            rooms[room.RoomId] = room;
        }
        if (File.Exists(_seedFile)) Add(JsonDocument.Parse(File.ReadAllText(_seedFile)).RootElement, _seedFile);
        foreach (var (file, node) in _data.ReadAll("rooms"))
        {
            try { Add(JsonSerializer.Deserialize<JsonElement>(node.ToJsonString()), file); }
            catch (JsonException e) { Console.WriteLine($"{file}: {e.Message}"); }
        }
        _rooms = rooms;
        Console.WriteLine($"room catalog: {rooms.Count} rooms");
    }

    /// <summary>Fill the parts archived room dumps usually lack (the public API leaves out sub-rooms and roles).</summary>
    private static void Normalize(Room r)
    {
        if (r.SubRooms.Count == 0) r.SubRooms.Add(new SubRoom { Name = "Home" });
        for (int i = 0; i < r.SubRooms.Count; i++)
        {
            var s = r.SubRooms[i];
            s.RoomId = r.RoomId;
            if (s.SubRoomId == 0) s.SubRoomId = unchecked(r.RoomId * 31 + i + 1) & long.MaxValue;   // stable across restarts, positive
            if (s.MaxPlayers == 0) s.MaxPlayers = r.MaxPlayers;
        }
        if (r.Roles.Count == 0) r.Roles.Add(new RoomRole { AccountId = r.CreatorAccountId, Role = 255 });
        r.PublishedAt ??= r.CreatedAt;
        r.RankedEntityId ??= r.RoomId.ToString();
        // notes such as "_archive" (where the import came from) are for people, not the client
        StripNotes(r.Extra);
        foreach (var s in r.SubRooms) StripNotes(s.Extra);
    }

    private static void StripNotes(Dictionary<string, JsonElement>? extra)
    {
        foreach (var k in extra?.Keys.Where(k => k.StartsWith('_')).ToList() ?? new()) extra!.Remove(k);
    }

    public IEnumerable<Room> All => _rooms.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase);
    public Room? Get(long id) => _rooms.GetValueOrDefault(id);
    public Room? ByName(string name) => _rooms.Values.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
    public IEnumerable<Room> Public => All.Where(r => r.Accessibility == 1 && !r.IsDorm && !r.ExcludeFromLists);
    public IEnumerable<Room> CreatedBy(int accountId) => All.Where(r => r.CreatorAccountId == accountId);

    public IEnumerable<Room> Search(string query) => All.Where(r => !r.ExcludeFromSearch && (
        r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        r.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        r.Tags.Any(t => t.Tag.Contains(query, StringComparison.OrdinalIgnoreCase))));

    /// <summary>The local player's dorm: the room flagged IsDorm in the catalog.</summary>
    public Room? Dorm => _rooms.Values.FirstOrDefault(r => r.IsDorm);

    /// <summary>A new private instance of a room (offline: every instance is the player's own).</summary>
    public RoomInstance Instantiate(Room room, string? subRoomName = null)
    {
        var sub = room.SubRooms.FirstOrDefault(s => string.Equals(s.Name, subRoomName, StringComparison.OrdinalIgnoreCase)) ?? room.SubRooms[0];
        var id = Interlocked.Increment(ref _nextInstance);
        return new RoomInstance
        {
            RoomInstanceId = id,
            RoomId = room.RoomId,
            SubRoomId = sub.SubRoomId,
            Location = sub.UnitySceneId,
            DataBlob = sub.DataBlob,
            Name = room.SubRooms.Count > 1 ? $"^{room.Name}.{sub.Name}" : $"^{room.Name}",
            PhotonRoomId = $"{room.Name}-{id}-offline",
            MaxCapacity = sub.MaxPlayers,
            IsPrivate = true,
        };
    }

    // --- the local player's cheers, favorites and visit history (data/state/room-interactions.json) ---

    public bool IsCheered(long roomId) { lock (_lock) return _interactions.Cheered.Contains(roomId); }
    public bool IsFavorited(long roomId) { lock (_lock) return _interactions.Favorited.Contains(roomId); }
    public IEnumerable<Room> Favorites { get { lock (_lock) return _interactions.Favorited.Select(Get).OfType<Room>().ToList(); } }
    public IEnumerable<Room> Recent { get { lock (_lock) return _interactions.Visited.Select(Get).OfType<Room>().ToList(); } }

    public void SetCheer(long roomId, bool on) => Change(i => Toggle(i.Cheered, roomId, on));
    public void SetFavorite(long roomId, bool on) => Change(i => Toggle(i.Favorited, roomId, on));

    /// <summary>Most recent first, at most 50.</summary>
    public void Visited(long roomId) => Change(i =>
    {
        i.Visited.Remove(roomId);
        i.Visited.Insert(0, roomId);
        if (i.Visited.Count > 50) i.Visited.RemoveRange(50, i.Visited.Count - 50);
    });

    private static void Toggle(List<long> list, long id, bool on)
    {
        list.Remove(id);
        if (on) list.Add(id);
    }

    private void Change(Action<RoomInteractions> change)
    {
        lock (_lock) { change(_interactions); _data.Save("room-interactions", _interactions); }
    }

    public class RoomInteractions
    {
        public List<long> Cheered { get; set; } = new();
        public List<long> Favorited { get; set; } = new();
        public List<long> Visited { get; set; } = new();
    }
}
