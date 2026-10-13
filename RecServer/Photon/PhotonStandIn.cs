namespace RecServer.Photon;

/// <summary>
/// A single-player stand-in for Photon Cloud (Realtime / LoadBalancing), for offline archival use. It answers just enough of the
/// name server -> master server -> game server flow for the client to "join a room" alone:
/// <list type="bullet">
/// <item>name server: key exchange, region list, authenticate (returns the local master address)</item>
/// <item>master: authenticate, lobby, create/join room (returns the local game server address)</item>
/// <item>game server: create/join makes the local player actor 1 and master client; RaiseEvent comes back to the sender when it
/// was addressed to everyone or to the master client; properties are stored and echoed as PropertiesChanged</item>
/// </list>
/// Anything else gets an empty "OK". Every operation is logged with its parameters, which is the main way to find out what the
/// client still needs.
/// </summary>
public sealed class PhotonStandIn : IDisposable
{
    // operation codes (Photon Realtime)
    private const byte OpGetRegions = 220, OpAuthenticate = 230, OpAuthenticateOnce = 231, OpJoinLobby = 229, OpLeaveLobby = 228,
        OpCreateGame = 227, OpJoinGame = 226, OpJoinRandomGame = 225, OpLeave = 254, OpRaiseEvent = 253, OpSetProperties = 252,
        OpGetProperties = 251, OpChangeGroups = 248, OpFindFriends = 222, OpGetGameList = 217;
    // parameter codes
    private const byte PAddress = 230, PUserId = 225, PSecret = 221, PRegion = 210, PCluster = 196, PNickName = 202,
        PRoomName = 255, PActorNr = 254, PTargetActorNr = 253, PActorList = 252, PProperties = 251, PBroadcast = 250,
        PPlayerProperties = 249, PGameProperties = 248, PReceiverGroup = 246, PData = 245, PCode = 244, PGameList = 222;
    // event codes
    private const byte EvJoin = 255, EvLeave = 254, EvPropertiesChanged = 253, EvGameList = 230;
    // game property keys
    private const byte GpMaxPlayers = 255, GpIsVisible = 254, GpIsOpen = 253, GpPlayerCount = 252, GpMasterClientId = 248;

    private const int LocalActor = 1;

    private readonly PhotonUdpServer _udp;
    private readonly Action<string> _log;
    private readonly string _host;
    private readonly int _masterPort, _gamePort;
    private readonly string[] _regions;

    /// <summary>Rooms by name; offline there is one player, but the client may hop between rooms.</summary>
    private readonly Dictionary<string, Room> _rooms = new();

    private sealed class Room
    {
        public required string Name;
        public PhotonHashtable Properties = new();
        public PhotonHashtable PlayerProperties = new();
    }

    private sealed class Session
    {
        public PhotonCrypto? Crypto;
        public Room? Room;
        public string UserId = "";
    }

    /// <param name="host">Address the client is sent to for master and game servers (it runs on the same machine).</param>
    /// <param name="nameServerPort">UDP port of the name server (Photon default 5058; 27000-27002 are added as alternatives).</param>
    /// <param name="masterPort">UDP port of the master server (default 5055; also answers region pings).</param>
    /// <param name="gamePort">UDP port of the game server (default 5056).</param>
    /// <param name="regions">Region codes offered to the client.</param>
    /// <param name="log">Log sink.</param>
    public PhotonStandIn(string host, int nameServerPort, int masterPort, int gamePort, string[] regions, Action<string> log)
    {
        _host = host;
        _masterPort = masterPort;
        _gamePort = gamePort;
        _regions = regions;
        _log = log;
        // The client's default ports, plus the "alternative" ones it may be configured for.
        var ports = new List<(int, PhotonRole)> { (nameServerPort, PhotonRole.NameServer), (masterPort, PhotonRole.Master), (gamePort, PhotonRole.Game) };
        if (nameServerPort == 5058) ports.AddRange(new[] { (27000, PhotonRole.NameServer), (27001, PhotonRole.Master), (27002, PhotonRole.Game) });
        _udp = new PhotonUdpServer(ports, OnMessage, log);
    }

    public void Start() => _udp.Start();
    public void Dispose() => _udp.Dispose();

    private Task OnMessage(UdpPeer peer, byte[] m)
    {
        try { Handle(peer, m); }
        catch (Exception e) { _log($"{peer.Role} peer {peer.PeerId}: message failed: {e.Message}"); }
        return Task.CompletedTask;
    }

    private void Handle(UdpPeer peer, byte[] m)
    {
        if (m.Length < 2 || (m[0] != 0xF3 && m[0] != 0xFD)) { _log($"{peer.Role} peer {peer.PeerId}: not a Photon message ({m.Length} bytes)"); return; }
        var s = (Session)(peer.AppState ??= new Session());
        byte type = (byte)(m[1] & 0x7F);
        bool encrypted = (m[1] & 0x80) != 0;
        switch (type)
        {
            case 0:   // Init: protocol version in bytes 2-3 (1.8 = GpBinaryV18), client version, app id
            {
                string app = System.Text.Encoding.ASCII.GetString(m, 9, Math.Min(32, m.Length - 9)).TrimEnd('\0');
                _log($"{peer.Role} peer {peer.PeerId}: init protocol {m[2]}.{m[3]} app {app}");
                if (m[3] != 8) _log($"  WARNING: serialization protocol {m[2]}.{m[3]} is not 1.8; only GpBinaryV18 is implemented");
                peer.SendReliable(new byte[] { 0xF3, 1, 0, 0 });   // InitResponse
                return;
            }
            case 6:   // internal operation: key exchange
            {
                var op = Protocol18.ReadOperationRequest(m, 2);
                if (op.Code == 0 && op.Parameters.TryGetValue(1, out var key) && key is byte[] clientKey)
                {
                    s.Crypto = new PhotonCrypto(clientKey);
                    _log($"{peer.Role} peer {peer.PeerId}: key exchange ({clientKey.Length}-byte client key)");
                    Send(peer, 7, Protocol18.OperationResponse(0, 0, null, new() { [1] = s.Crypto.ServerPublicKey }));
                }
                else _log($"{peer.Role} peer {peer.PeerId}: internal operation {op.Code} ignored");
                return;
            }
            case 2:   // operation
            {
                OperationRequest op;
                if (encrypted)
                {
                    if (s.Crypto == null) { _log($"{peer.Role} peer {peer.PeerId}: encrypted operation before key exchange"); return; }
                    var plain = s.Crypto.Decrypt(m, 2, m.Length - 2);
                    op = Protocol18.ReadOperationRequest(plain, 0);
                }
                else op = Protocol18.ReadOperationRequest(m, 2);
                _log($"{peer.Role} peer {peer.PeerId}: op {op.Code}{(encrypted ? " (encrypted)" : "")} {Describe(op.Parameters)}");
                Operation(peer, s, op);
                return;
            }
            default:
                _log($"{peer.Role} peer {peer.PeerId}: message type {type} ignored");
                return;
        }
    }

    private void Operation(UdpPeer peer, Session s, OperationRequest op)
    {
        var p = op.Parameters;
        switch (peer.Role, op.Code)
        {
            case (PhotonRole.NameServer, OpGetRegions):
                Respond(peer, op.Code, new()
                {
                    [PRegion] = _regions,
                    [PAddress] = _regions.Select(_ => $"{_host}:{_masterPort}").ToArray(),
                });
                return;

            case (_, OpAuthenticate):
            case (_, OpAuthenticateOnce):
            {
                s.UserId = p.GetValueOrDefault(PUserId) as string is { Length: > 0 } uid ? uid : $"local-{peer.PeerId}";
                var r = new Dictionary<byte, object?> { [PUserId] = s.UserId, [PSecret] = "offline-token" };
                if (peer.Role == PhotonRole.NameServer)
                {
                    r[PAddress] = $"{_host}:{_masterPort}";
                    r[PCluster] = "default";
                }
                Respond(peer, op.Code, r);
                return;
            }

            case (PhotonRole.Master, OpJoinLobby):
                Respond(peer, op.Code, null);
                Event(peer, EvGameList, new() { [PGameList] = new PhotonHashtable() });
                return;

            case (PhotonRole.Master, OpCreateGame):
            case (PhotonRole.Master, OpJoinGame):
            case (PhotonRole.Master, OpJoinRandomGame):
            {
                string name = p.GetValueOrDefault(PRoomName) as string is { Length: > 0 } n ? n : $"offline-{Guid.NewGuid():N}";
                Respond(peer, op.Code, new() { [PAddress] = $"{_host}:{_gamePort}", [PRoomName] = name });
                return;
            }

            case (PhotonRole.Game, OpCreateGame):
            case (PhotonRole.Game, OpJoinGame):
            case (PhotonRole.Game, OpJoinRandomGame):
                EnterRoom(peer, s, op);
                return;

            case (PhotonRole.Game, OpRaiseEvent):
                RaiseEvent(peer, s, p);
                return;   // no response: Photon only answers RaiseEvent on error

            case (PhotonRole.Game, OpSetProperties):
                SetProperties(peer, s, op);
                return;

            case (PhotonRole.Game, OpGetProperties):
                Respond(peer, op.Code, new()
                {
                    [PGameProperties] = s.Room?.Properties ?? new PhotonHashtable(),
                    [PPlayerProperties] = new PhotonHashtable { [LocalActor] = s.Room?.PlayerProperties ?? new PhotonHashtable() },
                });
                return;

            case (PhotonRole.Game, OpLeave):
                Respond(peer, op.Code, null);
                s.Room = null;
                return;

            case (_, OpFindFriends):
                Respond(peer, op.Code, new() { [1] = System.Array.Empty<object?>(), [2] = System.Array.Empty<string>() });
                return;

            case (_, OpGetGameList):
                Respond(peer, op.Code, new() { [PGameList] = new PhotonHashtable() });
                return;

            default:
                _log($"  -> op {op.Code} on {peer.Role} not modelled; answering OK");
                Respond(peer, op.Code, null);
                return;
        }
    }

    private void EnterRoom(UdpPeer peer, Session s, OperationRequest op)
    {
        var p = op.Parameters;
        string name = p.GetValueOrDefault(PRoomName) as string ?? "offline";
        Room room;
        lock (_rooms)
        {
            if (!_rooms.TryGetValue(name, out room!)) _rooms[name] = room = new Room { Name = name };
        }
        if (p.GetValueOrDefault(PGameProperties) is PhotonHashtable gp) foreach (var (k, v) in gp) room.Properties[k] = v;
        room.PlayerProperties = p.GetValueOrDefault(PPlayerProperties) as PhotonHashtable ?? new PhotonHashtable();
        // the room as the client should see it: open, visible, one player, the local player is master client
        room.Properties.TryAdd(GpMaxPlayers, (byte)20);
        room.Properties[GpIsOpen] = true;
        room.Properties[GpIsVisible] = room.Properties.GetValueOrDefault(GpIsVisible) ?? true;
        room.Properties[GpPlayerCount] = (byte)1;
        room.Properties[GpMasterClientId] = LocalActor;
        s.Room = room;
        _log($"  -> {(op.Code == OpCreateGame ? "created" : "joined")} room \"{name}\" as actor {LocalActor} (master client)");

        Respond(peer, op.Code, new()
        {
            [PActorNr] = LocalActor,
            [PGameProperties] = room.Properties,
            [PPlayerProperties] = new PhotonHashtable { [LocalActor] = room.PlayerProperties },
            [PActorList] = new[] { LocalActor },
        });
        Event(peer, EvJoin, new()
        {
            [PActorNr] = LocalActor,
            [PPlayerProperties] = room.PlayerProperties,
            [PActorList] = new[] { LocalActor },
        });
    }

    private void RaiseEvent(UdpPeer peer, Session s, Dictionary<byte, object?> p)
    {
        byte code = p.GetValueOrDefault(PCode) is byte c ? c : (byte)0;
        // ReceiverGroup: 0 others (nobody, offline), 1 all, 2 master client (that is us). Explicit target actors win.
        bool toSelf = p.GetValueOrDefault(PActorList) is int[] targets
            ? targets.Contains(LocalActor)
            : (p.GetValueOrDefault(PReceiverGroup) is byte g && g is 1 or 2);
        if (!toSelf) return;
        var ev = new Dictionary<byte, object?> { [PActorNr] = LocalActor };
        if (p.TryGetValue(PData, out var data)) ev[PData] = data;
        Event(peer, code, ev);
    }

    private void SetProperties(UdpPeer peer, Session s, OperationRequest op)
    {
        var p = op.Parameters;
        var props = p.GetValueOrDefault(PProperties) as PhotonHashtable ?? new PhotonHashtable();
        int target = p.GetValueOrDefault(PActorNr) is int a ? a : 0;   // 0 / absent = room properties
        if (s.Room != null)
        {
            var store = target == 0 ? s.Room.Properties : s.Room.PlayerProperties;
            foreach (var (k, v) in props)
            {
                if (v == null) store.Remove(k); else store[k] = v;
            }
        }
        Respond(peer, op.Code, null);
        // Rooms created with "broadcast property changes to all" wait for this event before applying the change locally.
        Event(peer, EvPropertiesChanged, new() { [PTargetActorNr] = target, [PProperties] = props, [PActorNr] = LocalActor });
    }

    private void Respond(UdpPeer peer, byte code, Dictionary<byte, object?>? parameters, short returnCode = 0)
    {
        _log($"  <- response {code} rc {returnCode} {Describe(parameters)}");
        Send(peer, 3, Protocol18.OperationResponse(code, returnCode, null, parameters));
    }

    private void Event(UdpPeer peer, byte code, Dictionary<byte, object?> parameters)
    {
        _log($"  <- event {code} {Describe(parameters)}");
        Send(peer, 4, Protocol18.Event(code, parameters));
    }

    private static void Send(UdpPeer peer, byte messageType, byte[] body)
    {
        var msg = new byte[body.Length + 2];
        msg[0] = 0xF3;
        msg[1] = messageType;
        Buffer.BlockCopy(body, 0, msg, 2, body.Length);
        peer.SendReliable(msg);
    }

    private static string Describe(Dictionary<byte, object?>? p) =>
        p == null || p.Count == 0 ? "{}" : "{" + string.Join(", ", p.Select(kv => $"{kv.Key}: {Protocol18.Describe(kv.Value)}")) + "}";
}
