using System.Text.Json.Serialization;

namespace RecServer.Models;

// Room shapes as rooms.rec.net served them (PascalCase) around 2023-2024. Field list comes from the public rooms API; not yet
// checked against the 2024-08-20 client's RoomDetails classes, so expect to fix names from requests.jsonl / overrides.

public class Room
{
    public long RoomId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageName { get; set; } = "";
    public int CreatorAccountId { get; set; } = 1;
    /// <summary>0 = active.</summary>
    public int State { get; set; }
    /// <summary>0 private, 1 public, 2 unlisted.</summary>
    public int Accessibility { get; set; } = 1;
    public bool IsDorm { get; set; }
    /// <summary>Rec Room Original (made by the studio); built-in scenes.</summary>
    public bool IsRRO { get; set; }
    public int MaxPlayerCalculationMode { get; set; }
    public int MaxPlayers { get; set; } = 20;
    public bool CloningAllowed { get; set; }
    public bool DisableMicAutoMute { get; set; }
    public bool DisableRoomComments { get; set; }
    public bool EncryptVoiceChat { get; set; }
    public bool LoadScreenLocked { get; set; }
    public bool SupportsLevelVoting { get; set; }
    public bool SupportsScreens { get; set; } = true;
    public bool SupportsWalkVR { get; set; } = true;
    public bool SupportsTeleportVR { get; set; } = true;
    public bool SupportsVRLow { get; set; } = true;
    public bool SupportsQuest2 { get; set; } = true;
    public bool SupportsMobile { get; set; } = true;
    public bool SupportsJuniors { get; set; } = true;
    public int MinLevel { get; set; }
    public int WarningMask { get; set; }
    public string? CustomWarning { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = new(2016, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    public DateTime? PublishedAt { get; set; }
    public RoomStats Stats { get; set; } = new();
    public List<SubRoom> SubRooms { get; set; } = new();
    public List<RoomRole> Roles { get; set; } = new();
    public List<RoomTag> Tags { get; set; } = new();
    public List<object> PromoImages { get; set; } = new();
    public List<object> PromoExternalContent { get; set; } = new();
    public List<object> LoadScreens { get; set; } = new();
}

public class SubRoom
{
    public long SubRoomId { get; set; }
    public long RoomId { get; set; }
    public string Name { get; set; } = "Home";
    /// <summary>GUID of the Unity scene that hosts this sub-room. Build-specific: must match a scene in the game build.</summary>
    public string UnitySceneId { get; set; } = "";
    /// <summary>Name of the saved room-state blob on cdn.rec.net (user-built rooms); empty for built-in scenes.</summary>
    public string? DataBlob { get; set; }
    public DateTime? DataSavedAt { get; set; }
    public bool IsSandbox { get; set; }
    public int MaxPlayers { get; set; } = 20;
    public int Accessibility { get; set; } = 1;
}

public class RoomStats
{
    public int CheerCount { get; set; }
    public int FavoriteCount { get; set; }
    public int VisitorCount { get; set; }
    public int VisitCount { get; set; }
}

public class RoomRole
{
    public int AccountId { get; set; }
    /// <summary>255 = owner, 30 = co-owner, 20 = moderator, 10 = host? (values as seen in old API dumps).</summary>
    public int Role { get; set; } = 255;
    public int? LastChangedByAccountId { get; set; }
    public int InvitedRole { get; set; }
}

public class RoomTag
{
    public string Tag { get; set; } = "";
    /// <summary>0 = user tag, 2 = auto/RRO tag.</summary>
    public int Type { get; set; }
}

/// <summary>A running copy of a room the player is (or is going) in. Returned by match.rec.net (camelCase).</summary>
public class RoomInstance
{
    [JsonPropertyName("roomInstanceId")] public long RoomInstanceId { get; set; }
    [JsonPropertyName("roomId")] public long RoomId { get; set; }
    [JsonPropertyName("subRoomId")] public long SubRoomId { get; set; }
    [JsonPropertyName("roomInstanceType")] public int RoomInstanceType { get; set; }
    /// <summary>Unity scene GUID the client loads.</summary>
    [JsonPropertyName("location")] public string Location { get; set; } = "";
    [JsonPropertyName("dataBlob")] public string? DataBlob { get; set; }
    [JsonPropertyName("eventId")] public long? EventId { get; set; }
    [JsonPropertyName("clubId")] public long? ClubId { get; set; }
    [JsonPropertyName("roomCode")] public string? RoomCode { get; set; }
    [JsonPropertyName("photonRegionId")] public string PhotonRegionId { get; set; } = "us";
    [JsonPropertyName("photonRoomId")] public string PhotonRoomId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("maxCapacity")] public int MaxCapacity { get; set; } = 20;
    [JsonPropertyName("isFull")] public bool IsFull { get; set; }
    [JsonPropertyName("isPrivate")] public bool IsPrivate { get; set; } = true;
    [JsonPropertyName("isInProgress")] public bool IsInProgress { get; set; }
}

/// <summary>Where a player is; match.rec.net <c>/player</c> and heartbeat responses.</summary>
public class PlayerPresence
{
    [JsonPropertyName("playerId")] public int PlayerId { get; set; }
    [JsonPropertyName("statusVisibility")] public int StatusVisibility { get; set; }
    [JsonPropertyName("platform")] public int Platform { get; set; } = 1;
    [JsonPropertyName("deviceClass")] public int DeviceClass { get; set; }
    [JsonPropertyName("vrMovementMode")] public int VrMovementMode { get; set; }
    [JsonPropertyName("roomInstance")] public RoomInstance? RoomInstance { get; set; }
    [JsonPropertyName("isOnline")] public bool IsOnline { get; set; } = true;
    [JsonPropertyName("appVersion")] public string? AppVersion { get; set; }
    [JsonPropertyName("lastOnline")] public DateTime LastOnline { get; set; } = DateTime.UtcNow;
}

/// <summary>match.rec.net <c>/goto/...</c> result.</summary>
public class MatchmakingResponse
{
    /// <summary>0 = success.</summary>
    [JsonPropertyName("errorCode")] public int ErrorCode { get; set; }
    [JsonPropertyName("roomInstance")] public RoomInstance? RoomInstance { get; set; }
}

