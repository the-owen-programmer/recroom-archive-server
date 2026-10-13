using RecServer.Models;

namespace RecServer.Services;

/// <summary>Where the local player is right now (in memory only; a restart puts the player back on the title screen).</summary>
public class PresenceService
{
    private readonly int _localId;
    private readonly object _lock = new();
    private RoomInstance? _current;
    private bool _online;
    private string? _appVersion;

    /// <param name="localId">Account id of the local player (whatever the auth layer hands out; REC_ACCOUNT_ID, default 1).</param>
    public PresenceService(int localId) => _localId = localId;

    public int LocalId => _localId;

    public RoomInstance? Current { get { lock (_lock) return _current; } }

    public void Heartbeat(string? appVersion) { lock (_lock) { _online = true; _appVersion = appVersion ?? _appVersion; } }
    public void Logout() { lock (_lock) { _online = false; _current = null; } }
    public void Enter(RoomInstance? instance) { lock (_lock) { _online = true; _current = instance; } }

    public PlayerPresence Get(int playerId)
    {
        lock (_lock)
        {
            bool me = playerId == _localId;
            return new PlayerPresence
            {
                PlayerId = playerId,
                IsOnline = me && _online,
                RoomInstance = me ? _current : null,
                AppVersion = me ? _appVersion : null,
            };
        }
    }
}
