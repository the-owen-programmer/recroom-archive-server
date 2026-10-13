using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RecServer.Models;

namespace RecServer.Controllers;

/// <summary>
/// The batch of requests the client fires right after <c>connect/token</c> succeeds (observed in the live request log, 2024-08-20
/// build). Response shapes are minimal guesses ("nothing here, nothing blocked"); each is corrected when the client's reaction shows
/// a problem. Use data/overrides/&lt;host&gt;/&lt;path&gt;.json to try a different body without recompiling.
/// </summary>
[ApiController]
[Tags("Post-login batch")]
public class PostLoginController : ControllerBase
{
    private static readonly object[] None = [];

    [Host("api.rec.net")][HttpGet("/api/customAvatarItems/v1/isCreationAllowedForAccount")]
    public bool CustomAvatarItemsAllowed() => true;

    /// <summary>Moderation: is this account currently blocked/banned? Zeroed = no.</summary>
    [Host("api.rec.net")][HttpPost("/api/PlayerReporting/v1/moderationBlockDetails")]
    public object ModerationBlockDetails() => new
    {
        ReportCategory = 0, Duration = 0, GameSessionId = 0, Message = "", PlayerReportId = 0,
        IsHostKick = false, IsBan = false, IsWarning = false,
    };

    /// <summary>Cheat/anti-tamper report from the client (e.g. "Signature missing" for an image). Accepted and ignored.</summary>
    [Host("api.rec.net")][HttpPost("/api/PlayerReporting/v1/hile")]
    public IActionResult Hile() => Ok();

    /// <summary>SignalR negotiate for the notification hub. The hub itself (websocket) is not implemented yet.</summary>
    [Host("notifications.rec.net")][HttpPost("/hub/v1/negotiate")]
    public object Negotiate() => new
    {
        negotiateVersion = 1, connectionId = Guid.NewGuid().ToString("N"), connectionToken = Guid.NewGuid().ToString("N"),
        availableTransports = new[] { new { transport = "WebSockets", transferFormats = new[] { "Text", "Binary" } } },
    };

    [Host("playersettings.rec.net")][HttpGet("/playersettings")]
    public object[] PlayerSettings() => None;

    [Host("api.rec.net")][HttpGet("/outfits/me")]
    public object[] Outfits() => None;

    [Host("econ.rec.net")][HttpGet("/api/avatar/v4/items")]
    public object[] AvatarItems() => None;

    [Host("datacollection.rec.net")][HttpPost("/data/event")]
    public IActionResult DataEvent() => Ok();

    [Host("api.rec.net")][HttpGet("/api/players/v2/progression/bulk")]
    public object[] Progression([FromQuery(Name = "id")] int[] ids) =>
        ids.Select(i => (object)new { PlayerId = i, Level = 1, XP = 0 }).ToArray();

    [Host("api.rec.net")][HttpGet("/api/playerReputation/v2/bulk")]
    public object[] Reputation([FromQuery(Name = "id")] int[] ids) =>
        ids.Select(i => (object)new
        {
            AccountId = i, IsCheerful = true, Noteriety = 0, SelectedCheer = (int?)null, CheerCredit = 0, CheerGeneral = 0,
            CheerHelpful = 0, CheerCreative = 0, CheerGreatHost = 0, CheerSportsman = 0,
        }).ToArray();

    [Host("platformnotifications.rec.net")][HttpGet("/crm/me/config/v2")]
    public object CrmConfig() => new { };

    [Host("clubs.rec.net")][HttpGet("/subscription/mine/member")]
    public object[] ClubSubscriptions() => None;

    /// <summary>Matchmaking presence: the client announces itself with a login lock after sign-in.</summary>
    [Host("match.rec.net")][HttpPost("/player/login")]
    public object MatchLogin() => new
    {
        playerId = LocalPlayer.AccountId, isOnline = true, statusVisibility = 0, deviceClass = 2, vrMovementMode = 0,
        roomInstance = (object?)null, appVersion = "20240815.01",
    };

    [Host("match.rec.net")][HttpPost("/player/logout")]
    public IActionResult MatchLogout() => Ok();

    [Host("match.rec.net")][HttpGet("/player/connection-info")]
    public object ConnectionInfo() => new { };

    [Host("match.rec.net")][HttpGet("/player/qos")]
    public object Qos() => new { };
}
