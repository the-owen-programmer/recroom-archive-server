using System.Buffers.Binary;
using System.Text;

namespace RecServer.Photon;

/// <summary>
/// Photon "GpBinaryV16" serialization, the older format a client may pick instead of V18 (it says which in its init message).
/// Everything is big-endian; types are single characters ('i' int, 's' string, 'h' Hashtable, ...); counts are shorts, except
/// int and byte arrays (int length). Same value model as <see cref="Protocol18"/>: numbers, strings, hashtables and simple
/// arrays are decoded, everything else is kept as <see cref="RawValue"/> and re-sent byte for byte.
/// </summary>
public static class Protocol16
{
    public const byte Unknown = 0, Null = (byte)'*', Dictionary = (byte)'D', StringArray = (byte)'a', Byte = (byte)'b',
        Custom = (byte)'c', Double = (byte)'d', EventData = (byte)'e', Float = (byte)'f', Hashtable = (byte)'h', Integer = (byte)'i',
        Short = (byte)'k', Long = (byte)'l', IntArray = (byte)'n', Boolean = (byte)'o', OperationResponse = (byte)'p',
        OperationRequest = (byte)'q', String = (byte)'s', ByteArray = (byte)'x', Array = (byte)'y', ObjectArray = (byte)'z';

    public sealed class Reader
    {
        private readonly byte[] _b;
        public int Pos;
        public Reader(byte[] b, int pos = 0) { _b = b; Pos = pos; }

        public byte U8() => _b[Pos++];
        public short I16() { var v = BinaryPrimitives.ReadInt16BigEndian(_b.AsSpan(Pos)); Pos += 2; return v; }
        public int I32() { var v = BinaryPrimitives.ReadInt32BigEndian(_b.AsSpan(Pos)); Pos += 4; return v; }
        public long I64() { var v = BinaryPrimitives.ReadInt64BigEndian(_b.AsSpan(Pos)); Pos += 8; return v; }
        public string Str() { int n = (ushort)I16(); var s = Encoding.UTF8.GetString(_b, Pos, n); Pos += n; return s; }
        public byte[] Bytes(int n) { var r = _b.AsSpan(Pos, n).ToArray(); Pos += n; return r; }

        public object? Value() => Value(U8());

        public object? Value(byte type)
        {
            int start = Pos - 1;
            switch (type)
            {
                case Unknown:
                case Null: return null;
                case Boolean: return U8() != 0;
                case Byte: return U8();
                case Short: return I16();
                case Integer: return I32();
                case Long: return I64();
                case Float: return BinaryPrimitives.ReadSingleBigEndian(Bytes(4));
                case Double: return BinaryPrimitives.ReadDoubleBigEndian(Bytes(8));
                case String: return Str();
                case ByteArray: return Bytes(I32());
                case IntArray: { int n = I32(); var a = new int[n]; for (int i = 0; i < n; i++) a[i] = I32(); return a; }
                case StringArray: { int n = I16(); var a = new string[n]; for (int i = 0; i < n; i++) a[i] = Str(); return a; }
                case ObjectArray: { int n = I16(); var a = new object?[n]; for (int i = 0; i < n; i++) a[i] = Value(); return a; }
                case Hashtable:
                {
                    int n = I16();
                    var h = new PhotonHashtable();
                    for (int i = 0; i < n; i++)
                    {
                        var k = Value();
                        var v = Value();
                        if (k != null) h[k] = v;
                    }
                    return h;
                }
                default:
                    Skip(type);
                    var raw = new byte[Pos - start];
                    Buffer.BlockCopy(_b, start, raw, 0, raw.Length);
                    return new RawValue(raw);
            }
        }

        public void Skip(byte type)
        {
            switch (type)
            {
                case Unknown: case Null: return;
                case Boolean: case Byte: Pos += 1; return;
                case Short: Pos += 2; return;
                case Integer: case Float: Pos += 4; return;
                case Long: case Double: Pos += 8; return;
                case String: Pos += (ushort)I16(); return;
                case ByteArray: Pos += I32(); return;
                case IntArray: Pos += 4 * I32(); return;
                case StringArray: { int n = I16(); for (int i = 0; i < n; i++) Pos += (ushort)I16(); return; }
                case ObjectArray: { int n = I16(); for (int i = 0; i < n; i++) Skip(U8()); return; }
                case Hashtable: { int n = I16(); for (int i = 0; i < 2 * n; i++) Skip(U8()); return; }
                case Custom: Pos += 1; Pos += (ushort)I16(); return;
                case Dictionary: { byte k = U8(), v = U8(); SkipDictBody(k, v); return; }
                case Array: SkipArrayBody(I16()); return;
                case EventData: U8(); SkipParams(); return;
                case OperationRequest: U8(); SkipParams(); return;
                case OperationResponse: U8(); Pos += 2; Skip(U8()); SkipParams(); return;
                default: throw new InvalidDataException($"unknown Protocol16 type {type} at {Pos - 1}");
            }
        }

        private void SkipDictBody(byte keyType, byte valueType)
        {
            int n = I16();
            for (int i = 0; i < n; i++)
            {
                Skip(keyType is Unknown or Null ? U8() : keyType);
                Skip(valueType is Unknown or Null ? U8() : valueType);
            }
        }

        /// <summary>Array body after its length: element type, then untyped elements.</summary>
        private void SkipArrayBody(int n)
        {
            byte t = U8();
            switch (t)
            {
                case Array: for (int i = 0; i < n; i++) SkipArrayBody(I16()); return;
                case ByteArray: for (int i = 0; i < n; i++) Pos += I32(); return;
                case Byte: Pos += n; return;
                case Integer: Pos += 4 * n; return;
                case Custom: Pos += 1; for (int i = 0; i < n; i++) Pos += (ushort)I16(); return;
                case Dictionary:
                {
                    byte k = U8(), v = U8();
                    for (int i = 0; i < n; i++) SkipDictBody(k, v);
                    return;
                }
                default: for (int i = 0; i < n; i++) Skip(t); return;
            }
        }

        private void SkipParams() { int n = I16(); for (int i = 0; i < n; i++) { U8(); Skip(U8()); } }

        public Dictionary<byte, object?> Params()
        {
            int n = I16();
            var d = new Dictionary<byte, object?>(n);
            for (int i = 0; i < n; i++)
            {
                byte key = U8();
                d[key] = Value();
            }
            return d;
        }
    }

    public static OperationRequest ReadOperationRequest(byte[] data, int offset)
    {
        var r = new Reader(data, offset);
        byte code = r.U8();
        return new OperationRequest(code, r.Params());
    }

    public sealed class Writer
    {
        private readonly MemoryStream _m = new();
        public byte[] ToArray() => _m.ToArray();
        public void U8(byte b) => _m.WriteByte(b);
        public void Raw(byte[] b) => _m.Write(b);
        public void I16(short v) { Span<byte> t = stackalloc byte[2]; BinaryPrimitives.WriteInt16BigEndian(t, v); _m.Write(t); }
        public void I32(int v) { Span<byte> t = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(t, v); _m.Write(t); }
        public void I64(long v) { Span<byte> t = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(t, v); _m.Write(t); }
        public void Str(string s) { var b = Encoding.UTF8.GetBytes(s); I16((short)b.Length); _m.Write(b); }

        public void Value(object? v)
        {
            switch (v)
            {
                case null: U8(Null); break;
                case RawValue raw: Raw(raw.Bytes); break;
                case bool b: U8(Boolean); U8(b ? (byte)1 : (byte)0); break;
                case byte b: U8(Byte); U8(b); break;
                case short s: U8(Short); I16(s); break;
                case int i: U8(Integer); I32(i); break;
                case long l: U8(Long); I64(l); break;
                case float f: U8(Float); { Span<byte> t = stackalloc byte[4]; BinaryPrimitives.WriteSingleBigEndian(t, f); _m.Write(t); } break;
                case double d: U8(Double); { Span<byte> t = stackalloc byte[8]; BinaryPrimitives.WriteDoubleBigEndian(t, d); _m.Write(t); } break;
                case string s: U8(String); Str(s); break;
                case byte[] b: U8(ByteArray); I32(b.Length); Raw(b); break;
                case int[] a: U8(IntArray); I32(a.Length); foreach (var i in a) I32(i); break;
                case string[] a: U8(StringArray); I16((short)a.Length); foreach (var s in a) Str(s); break;
                case PhotonHashtable h:
                    U8(Hashtable); I16((short)h.Count);
                    foreach (var (k, val) in h) { Value(k); Value(val); }
                    break;
                case object?[] a: U8(ObjectArray); I16((short)a.Length); foreach (var x in a) Value(x); break;
                case Dictionary<string, object?> d:
                    U8(Dictionary); U8(String); U8(Unknown); I16((short)d.Count);
                    foreach (var (k, val) in d) { Str(k); Value(val); }
                    break;
                default: throw new NotSupportedException($"cannot serialize {v.GetType().Name}");
            }
        }

        public void Params(Dictionary<byte, object?>? p)
        {
            I16((short)(p?.Count ?? 0));
            if (p == null) return;
            foreach (var (k, v) in p) { U8(k); Value(v); }
        }
    }

    public static byte[] OperationResponseBody(byte code, short returnCode, string? debugMessage, Dictionary<byte, object?>? parameters)
    {
        var w = new Writer();
        w.U8(code);
        w.I16(returnCode);
        w.Value(debugMessage);
        w.Params(parameters);
        return w.ToArray();
    }

    public static byte[] EventBody(byte code, Dictionary<byte, object?>? parameters)
    {
        var w = new Writer();
        w.U8(code);
        w.Params(parameters);
        return w.ToArray();
    }
}
