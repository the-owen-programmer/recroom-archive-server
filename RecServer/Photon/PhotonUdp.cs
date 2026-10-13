using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace RecServer.Photon;

// Photon's reliable-UDP transport (an ENet derivative), server side, as the client library speaks it:
//   packet  = header(12, big-endian): peerId:i16, crcFlag:u8 (0xCC = CRC follows), commandCount:u8, sentTime:i32, challenge:i32
//             then commandCount commands
//   command = type:u8, channel:u8, flags:u8 (1 reliable, 2 unsequenced), reserved:u8, length:i32 (incl. header), reliableSeq:i32
//             + unreliableSeq:i32 (type 7) | unsequencedGroup:i32 (type 11)
//             + startSeq, fragmentCount, fragmentNumber, totalLength, fragmentOffset (5 x i32; types 8 and 15)
//             + payload
// The client drops packets whose challenge is not its own, so every packet echoes the challenge from the client's connect.
// Reliable commands are delivered in order per channel, starting at sequence 1; every reliable command is acknowledged with
// the client's packet sentTime so it can measure round trips.

public enum PhotonRole { NameServer, Master, Game }

public sealed class PhotonUdpServer : IDisposable
{
    public const byte CtAck = 1, CtConnect = 2, CtVerifyConnect = 3, CtDisconnect = 4, CtPing = 5, CtSendReliable = 6,
        CtSendUnreliable = 7, CtSendFragment = 8, CtSendUnsequenced = 11, CtServerTime = 12, CtReliableUnsequenced = 14,
        CtFragmentUnsequenced = 15, CtAckUnsequenced = 16;

    private const int Mtu = 1200;
    private const int MaxPayloadPerCommand = Mtu - 12 - 32 - 8;

    private readonly List<(UdpClient Socket, PhotonRole Role, int Port)> _sockets = new();
    private readonly ConcurrentDictionary<(int Port, IPEndPoint Remote), UdpPeer> _peers = new();
    private readonly Func<UdpPeer, byte[], Task> _onMessage;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stop = new();
    private short _nextPeerId = 1;

    public PhotonUdpServer(IEnumerable<(int Port, PhotonRole Role)> ports, Func<UdpPeer, byte[], Task> onMessage, Action<string> log)
    {
        _onMessage = onMessage;
        _log = log;
        foreach (var (port, role) in ports)
        {
            try
            {
                var s = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                _sockets.Add((s, role, port));
            }
            catch (SocketException e) { _log($"cannot listen on udp {port} ({role}): {e.Message}"); }
        }
    }

    public void Start()
    {
        foreach (var (socket, role, port) in _sockets)
        {
            _log($"listening on udp {port} as {role}");
            _ = Task.Run(() => ReceiveLoop(socket, role, port));
        }
        _ = Task.Run(ServiceLoop);
    }

    private async Task ReceiveLoop(UdpClient socket, PhotonRole role, int port)
    {
        while (!_stop.IsCancellationRequested)
        {
            UdpReceiveResult r;
            try { r = await socket.ReceiveAsync(_stop.Token); }
            catch (OperationCanceledException) { return; }
            catch (SocketException) { continue; }   // ICMP port unreachable from a vanished client, etc.
            try { Handle(socket, role, port, r.Buffer, r.RemoteEndPoint); }
            catch (Exception e) { _log($"udp {port} from {r.RemoteEndPoint}: bad packet ({e.Message})"); }
        }
    }

    private void Handle(UdpClient socket, PhotonRole role, int port, byte[] data, IPEndPoint from)
    {
        // region ping (PhotonPing): 13 bytes of 0x7D with an id in the last byte, answered by echoing it back
        if (data.Length == 13 && data[0] == 0x7D && data[1] == 0x7D)
        {
            socket.Send(data, data.Length, from);
            return;
        }
        if (data.Length < 12) return;
        byte crcFlag = data[2];
        if (crcFlag == 1) { _log($"{from}: datagram-encrypted packet; not supported by the stand-in"); return; }
        int count = data[3];
        int sentTime = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));
        int challenge = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(8));
        int offset = 12 + (crcFlag == 0xCC ? 4 : 0);

        var key = (port, from);
        _peers.TryGetValue(key, out var peer);
        for (int i = 0; i < count && offset + 12 <= data.Length; i++)
        {
            var cmd = Command.Parse(data, ref offset);
            if (cmd.Type == CtConnect)
            {
                if (peer == null || peer.Challenge != challenge)
                {
                    peer = new UdpPeer(this, socket, role, port, from, NextPeerId(), challenge, _log);
                    _peers[key] = peer;
                    _log($"{role} udp {port}: connect from {from}, peer {peer.PeerId}");
                }
                peer.OnConnect(cmd, sentTime);
                continue;
            }
            if (peer == null) continue;   // not connected: ignore (the client retries its connect)
            if (challenge != peer.Challenge) continue;
            peer.OnCommand(cmd, sentTime);
        }
        peer?.Flush();
    }

    private short NextPeerId()
    {
        lock (_peers) { if (_nextPeerId >= short.MaxValue - 1) _nextPeerId = 1; return _nextPeerId++; }
    }

    internal Task Deliver(UdpPeer peer, byte[] message) => _onMessage(peer, message);

    internal void Remove(UdpPeer peer)
    {
        _peers.TryRemove((peer.Port, peer.Remote), out _);
        _log($"{peer.Role} udp {peer.Port}: peer {peer.PeerId} ({peer.Remote}) gone");
    }

    private async Task ServiceLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            try { await Task.Delay(20, _stop.Token); } catch (OperationCanceledException) { return; }
            foreach (var p in _peers.Values)
            {
                try { p.Service(); }
                catch (Exception e) { _log($"peer {p.PeerId}: {e.Message}"); }
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        foreach (var (s, _, _) in _sockets) s.Dispose();
    }

    // ------------------------------------------------------------------------------------------------------------------

    internal sealed class Command
    {
        public byte Type, Channel, Flags, Reserved;
        public int Length, ReliableSeq, UnreliableSeq, StartSeq, FragmentCount, FragmentNumber, TotalLength, FragmentOffset;
        public int AckSeq, AckSentTime;
        public byte[] Payload = System.Array.Empty<byte>();

        public bool Reliable => (Flags & 1) != 0;
        public bool Unsequenced => (Flags & 2) != 0;

        public static Command Parse(byte[] b, ref int o)
        {
            var c = new Command
            {
                Type = b[o], Channel = b[o + 1], Flags = b[o + 2], Reserved = b[o + 3],
                Length = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o + 4)),
                ReliableSeq = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o + 8)),
            };
            int start = o;
            o += 12;
            int header = 12;
            switch (c.Type)
            {
                case CtAck:
                case CtAckUnsequenced:
                    c.AckSeq = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o));
                    c.AckSentTime = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o + 4));
                    break;
                case CtSendUnreliable:
                    c.UnreliableSeq = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o)); header = 16; break;
                case CtSendUnsequenced:
                    header = 16; break;
                case CtSendFragment:
                case CtFragmentUnsequenced:
                    c.StartSeq = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o));
                    c.FragmentCount = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o + 4));
                    c.FragmentNumber = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o + 8));
                    c.TotalLength = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o + 12));
                    c.FragmentOffset = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(o + 16));
                    header = 32;
                    break;
            }
            if (c.Type is CtSendReliable or CtReliableUnsequenced or CtSendUnreliable or CtSendUnsequenced or CtSendFragment or CtFragmentUnsequenced)
                c.Payload = b.AsSpan(start + header, Math.Max(0, c.Length - header)).ToArray();
            o = start + Math.Max(c.Length, 12);
            return c;
        }
    }
}

/// <summary>One connected client on one port (name, master or game server connection).</summary>
public sealed class UdpPeer
{
    private sealed class Outgoing
    {
        public required byte[] Bytes;   // full serialized command
        public byte Channel;
        public int Seq;
        public int SentAt;
        public int Tries;
    }

    private sealed class Channel
    {
        public int OutReliable;
        public int InReliable;   // last delivered
        public readonly SortedDictionary<int, PhotonUdpServer.Command> Pending = new();
        public readonly Dictionary<int, List<PhotonUdpServer.Command>> Fragments = new();
    }

    private readonly PhotonUdpServer _server;
    private readonly UdpClient _socket;
    private readonly Action<string> _log;
    private readonly object _lock = new();
    private readonly Dictionary<byte, Channel> _channels = new();
    private readonly List<byte[]> _acks = new();
    private readonly List<Outgoing> _unacked = new();
    private readonly Queue<Outgoing> _toSend = new();
    private int _lastHeard = Environment.TickCount;
    private bool _verified;

    public PhotonRole Role { get; }
    public int Port { get; }
    public IPEndPoint Remote { get; }
    public short PeerId { get; }
    public int Challenge { get; }
    public bool Closed { get; private set; }

    /// <summary>Session state owned by the application layer (crypto, room, ...).</summary>
    public object? AppState { get; set; }

    internal UdpPeer(PhotonUdpServer server, UdpClient socket, PhotonRole role, int port, IPEndPoint remote, short peerId, int challenge, Action<string> log)
    {
        _server = server; _socket = socket; Role = role; Port = port; Remote = remote; PeerId = peerId; Challenge = challenge; _log = log;
    }

    private Channel Ch(byte id) { if (!_channels.TryGetValue(id, out var c)) _channels[id] = c = new Channel(); return c; }

    internal void OnConnect(PhotonUdpServer.Command cmd, int sentTime)
    {
        lock (_lock)
        {
            _lastHeard = Environment.TickCount;
            QueueAck(cmd, sentTime);
            if (_verified) return;   // a resent connect: the ack above is enough, VerifyConnect is already queued/resent
            _verified = true;
            var payload = new byte[32];
            BinaryPrimitives.WriteInt16BigEndian(payload, PeerId);   // the client takes its peer id from here
            QueueReliable(PhotonUdpServer.CtVerifyConnect, 0xFF, payload);
        }
    }

    internal void OnCommand(PhotonUdpServer.Command cmd, int sentTime)
    {
        List<byte[]> deliver = new();
        lock (_lock)
        {
            _lastHeard = Environment.TickCount;
            if (cmd.Reliable && cmd.Type is not (PhotonUdpServer.CtAck or PhotonUdpServer.CtAckUnsequenced)) QueueAck(cmd, sentTime);
            switch (cmd.Type)
            {
                case PhotonUdpServer.CtAck:
                case PhotonUdpServer.CtAckUnsequenced:
                    _unacked.RemoveAll(o => o.Seq == cmd.AckSeq && o.Channel == cmd.Channel);
                    break;
                case PhotonUdpServer.CtDisconnect:
                    Closed = true;
                    break;
                case PhotonUdpServer.CtPing:
                case PhotonUdpServer.CtServerTime:
                    break;   // the ack (with our packet time) is the answer
                case PhotonUdpServer.CtSendUnreliable:
                case PhotonUdpServer.CtSendUnsequenced:
                case PhotonUdpServer.CtReliableUnsequenced:
                    deliver.Add(cmd.Payload);
                    break;
                case PhotonUdpServer.CtFragmentUnsequenced:
                    AddFragment(Ch(cmd.Channel), cmd, deliver);
                    break;
                case PhotonUdpServer.CtSendReliable:
                case PhotonUdpServer.CtSendFragment:
                {
                    var ch = Ch(cmd.Channel);
                    if (cmd.ReliableSeq <= ch.InReliable || ch.Pending.ContainsKey(cmd.ReliableSeq)) break;   // duplicate
                    ch.Pending[cmd.ReliableSeq] = cmd;
                    while (ch.Pending.Remove(ch.InReliable + 1, out var next))
                    {
                        ch.InReliable++;
                        if (next.Type == PhotonUdpServer.CtSendReliable) deliver.Add(next.Payload);
                        else AddFragment(ch, next, deliver);
                    }
                    break;
                }
            }
        }
        foreach (var m in deliver) _ = _server.Deliver(this, m);
        if (Closed) _server.Remove(this);
    }

    private static void AddFragment(Channel ch, PhotonUdpServer.Command cmd, List<byte[]> deliver)
    {
        if (!ch.Fragments.TryGetValue(cmd.StartSeq, out var parts)) ch.Fragments[cmd.StartSeq] = parts = new();
        if (parts.Any(p => p.FragmentNumber == cmd.FragmentNumber)) return;
        parts.Add(cmd);
        if (parts.Count < cmd.FragmentCount) return;
        var whole = new byte[cmd.TotalLength];
        foreach (var p in parts) Buffer.BlockCopy(p.Payload, 0, whole, p.FragmentOffset, Math.Min(p.Payload.Length, whole.Length - p.FragmentOffset));
        ch.Fragments.Remove(cmd.StartSeq);
        deliver.Add(whole);
    }

    private void QueueAck(PhotonUdpServer.Command cmd, int sentTime)
    {
        var a = new byte[20];
        a[0] = cmd.Unsequenced ? PhotonUdpServer.CtAckUnsequenced : PhotonUdpServer.CtAck;
        a[1] = cmd.Channel;
        a[2] = 0;
        a[3] = cmd.Reserved;
        BinaryPrimitives.WriteInt32BigEndian(a.AsSpan(4), 20);
        BinaryPrimitives.WriteInt32BigEndian(a.AsSpan(8), 0);
        BinaryPrimitives.WriteInt32BigEndian(a.AsSpan(12), cmd.ReliableSeq);
        BinaryPrimitives.WriteInt32BigEndian(a.AsSpan(16), sentTime);
        _acks.Add(a);
    }

    private void QueueReliable(byte type, byte channel, byte[] payload, int startSeq = 0, int fragCount = 0, int fragNo = 0, int total = 0, int fragOffset = 0)
    {
        var ch = Ch(channel);
        int seq = ++ch.OutReliable;
        bool fragment = type == PhotonUdpServer.CtSendFragment;
        int header = fragment ? 32 : 12;
        int size = type == PhotonUdpServer.CtVerifyConnect ? 44 : header + payload.Length;
        var b = new byte[size];
        b[0] = type; b[1] = channel; b[2] = 1; b[3] = 4;
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(4), size);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(8), seq);
        if (fragment)
        {
            BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(12), startSeq == 0 ? seq : startSeq);
            BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(16), fragCount);
            BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(20), fragNo);
            BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(24), total);
            BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(28), fragOffset);
        }
        Buffer.BlockCopy(payload, 0, b, header, payload.Length);
        _toSend.Enqueue(new Outgoing { Bytes = b, Channel = channel, Seq = seq });
    }

    /// <summary>Send a complete message (0xF3 header included) reliably, fragmenting it when it does not fit one packet.</summary>
    public void SendReliable(byte[] message, byte channel = 0)
    {
        lock (_lock)
        {
            const int max = 1200 - 12 - 32 - 8;
            if (message.Length <= max) { QueueReliable(PhotonUdpServer.CtSendReliable, channel, message); }
            else
            {
                int count = (message.Length + max - 1) / max;
                int start = Ch(channel).OutReliable + 1;
                for (int i = 0; i < count; i++)
                {
                    int off = i * max;
                    var part = message.AsSpan(off, Math.Min(max, message.Length - off)).ToArray();
                    QueueReliable(PhotonUdpServer.CtSendFragment, channel, part, start, count, i, message.Length, off);
                }
            }
        }
        Flush();
    }

    /// <summary>Write queued acks and new commands into packets; called after every incoming packet and by the service loop.</summary>
    public void Flush()
    {
        List<(byte[] Buf, int Len)> packets = new();
        lock (_lock)
        {
            int now = Environment.TickCount;
            var cmds = new List<byte[]>(_acks);
            _acks.Clear();
            while (_toSend.TryDequeue(out var o))
            {
                o.SentAt = now; o.Tries = 1;
                _unacked.Add(o);
                cmds.Add(o.Bytes);
            }
            if (cmds.Count == 0) return;
            var buf = new byte[1200 + 64];
            int len = 12, n = 0;
            foreach (var c in cmds)
            {
                if (n > 0 && (len + c.Length > 1200 || n == 255)) { packets.Add(Finish(buf, len, n, now)); buf = new byte[1200 + 64]; len = 12; n = 0; }
                if (len + c.Length > buf.Length) System.Array.Resize(ref buf, len + c.Length);
                Buffer.BlockCopy(c, 0, buf, len, c.Length);
                len += c.Length; n++;
            }
            packets.Add(Finish(buf, len, n, now));
        }
        foreach (var (b, l) in packets)
        {
            try { _socket.Send(b, l, Remote); } catch (SocketException) { }
        }
    }

    private (byte[], int) Finish(byte[] buf, int len, int count, int now)
    {
        BinaryPrimitives.WriteInt16BigEndian(buf, PeerId);
        buf[2] = 0;   // no CRC
        buf[3] = (byte)count;
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(4), now);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(8), Challenge);
        return (buf, len);
    }

    /// <summary>Resend unacknowledged reliable commands; drop the peer after a long silence.</summary>
    internal void Service()
    {
        int now = Environment.TickCount;
        if (now - _lastHeard > 30_000) { Closed = true; _server.Remove(this); return; }
        bool resend = false;
        lock (_lock)
        {
            foreach (var o in _unacked)
            {
                if (now - o.SentAt < 250 * Math.Min(o.Tries, 4)) continue;
                o.SentAt = now; o.Tries++;
                _acks.Add(o.Bytes);   // goes out with the next flush (acks are not tracked)
                resend = true;
            }
            _unacked.RemoveAll(o => o.Tries > 40);
        }
        if (resend) Flush();
    }
}
