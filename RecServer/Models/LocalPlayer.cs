namespace RecServer.Models;

/// <summary>The single local player the archive build logs in as (no real accounts exist any more).</summary>
public static class LocalPlayer
{
    public const int AccountId = 1;
    public const string Username = "Player";
    public const string RefreshToken = "local-archive-refresh-token";

    /// <summary>Shape of the client's <c>RecNet.Account</c> (camelCase on the wire, like the historical RecNet API).</summary>
    public static object Account() => new
    {
        accountId = AccountId,
        username = Username,
        displayName = Username,
        displayEmoji = "",
        profileImage = "DefaultProfileImage",
        bannerImage = (string?)null,
        isJunior = false,
        platforms = 1,
        personalPronouns = 0,
        identityFlags = 0,
        createdAt = "2016-06-01T00:00:00Z",
        isMetaPlatformBlocked = false,
    };

    /// <summary>Shape of <c>RecNet.Login.CachedLogin</c>.</summary>
    public static object CachedLogin(int platform, string platformId) => new
    {
        platform,
        platformId,
        accountId = AccountId,
        lastLoginTime = DateTime.UtcNow.ToString("o"),
        requirePassword = false,
        refreshToken = RefreshToken,
        account = Account(),
    };
}
