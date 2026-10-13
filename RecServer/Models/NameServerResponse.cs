namespace RecServer.Models;

/// <summary>
/// What the name server (<c>ns.rec.net</c>) returns: the base URL of every other RecNet service.
/// Field names are those of the client's <c>RecNet.Core.NameServerResponse</c> class; the client deserialises them
/// case-sensitively, so do not rename.
/// </summary>
public class NameServerResponse
{
    public string RecNetStatus { get; set; } = "";
    public string AI { get; set; } = "";
    public string Auth { get; set; } = "";
    public string API { get; set; } = "";
    public string WWW { get; set; } = "";
    public string Notifications { get; set; } = "";
    public string Images { get; set; } = "";
    public string CDN { get; set; } = "";
    public string Commerce { get; set; } = "";
    public string Matchmaking { get; set; } = "";
    public string Storage { get; set; } = "";
    public string Chat { get; set; } = "";
    public string Leaderboard { get; set; } = "";
    public string Accounts { get; set; } = "";
    public string Link { get; set; } = "";
    public string Lists { get; set; } = "";
    public string RoomComments { get; set; } = "";
    public string Clubs { get; set; } = "";
    public string Rooms { get; set; } = "";
    public string PlatformNotifications { get; set; } = "";
    public string Moderation { get; set; } = "";
    public string DataCollection { get; set; } = "";
    public string BugReporting { get; set; } = "";
    public string Discovery { get; set; } = "";
    public string PlayerSettings { get; set; } = "";
    public string Studio { get; set; } = "";
    public string GameLogs { get; set; } = "";
    public string Strings { get; set; } = "";
    public string Econ { get; set; } = "";
    public string StringsCDN { get; set; } = "";
    public string Data { get; set; } = "";
    public string Cards { get; set; } = "";

    /// <summary>Every service points at <c>https://&lt;name&gt;.rec.net</c>, all served by this process.</summary>
    public static NameServerResponse Local() => new()
    {
        RecNetStatus = "https://recnetstatus.rec.net", AI = "https://ai.rec.net", Auth = "https://auth.rec.net",
        API = "https://api.rec.net", WWW = "https://www.rec.net", Notifications = "https://notifications.rec.net",
        Images = "https://img.rec.net", CDN = "https://cdn.rec.net", Commerce = "https://commerce.rec.net",
        Matchmaking = "https://match.rec.net", Storage = "https://storage.rec.net", Chat = "https://chat.rec.net",
        Leaderboard = "https://leaderboard.rec.net", Accounts = "https://accounts.rec.net", Link = "https://link.rec.net",
        Lists = "https://lists.rec.net", RoomComments = "https://roomcomments.rec.net", Clubs = "https://clubs.rec.net",
        Rooms = "https://rooms.rec.net", PlatformNotifications = "https://platformnotifications.rec.net",
        Moderation = "https://moderation.rec.net", DataCollection = "https://datacollection.rec.net",
        BugReporting = "https://bugreporting.rec.net", Discovery = "https://discovery.rec.net",
        PlayerSettings = "https://playersettings.rec.net", Studio = "https://studio.rec.net",
        GameLogs = "https://gamelogs.rec.net", Strings = "https://strings.rec.net", Econ = "https://econ.rec.net",
        StringsCDN = "https://stringscdn.rec.net", Data = "https://data.rec.net", Cards = "https://cards.rec.net",
    };
}
