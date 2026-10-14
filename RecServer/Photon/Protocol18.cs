using System.Text;

namespace RecServer.Photon;

// Photon "GpBinaryV18" serialization (the PUN 2 default), written from the wire format the client library uses:
// type byte + value; shorts little-endian, floats/doubles big-endian, ints/longs as zig-zag varints, lengths as varints.
// Values the stand-in has to understand (numbers, strings, hashtables, arrays) are decoded; everything else (custom types,
// typed dictionaries, nested arrays) is kept as RawValue with its exact bytes, so it can be sent back unchanged.

/// <summary>A value kept as its exact serialized bytes, type byte included.</summary>
public sealed record RawValue(byte[] Bytes)
{
    public override string ToString() => $"raw[{Bytes.Length}] type {Bytes[0]}";
}

/// <summary>Photon Hashtable (keys usually byte, int or string). Boxed key equality matches Photon's.</summary>
public sealed class PhotonHashtable : Dictionary<object, object?>
{
    public PhotonHashtable() { }
    public PhotonHashtable(IDictionary<object, object?> other) : base(other) { }
}

public sealed record OperationRequest(byte Code, Dictionary<byte, object?> Parameters);

public static class Protocol18
{
    public const byte Unknown = 0, Boolean = 2, Byte = 3, Short = 4, Float = 5, Double = 6, String = 7, Null = 8,
        CompressedInt = 9, CompressedLong = 10, Int1 = 11, Int1Neg = 12, Int2 = 13, Int2Neg = 14,
        L1 = 15, L1Neg = 16, L2 = 17, L2Neg = 18, Custom = 19, Dictionary = 20, Hashtable = 21, ObjectArray = 23,
        OperationRequestType = 24, OperationResponseType = 25, EventDataType = 26,
        BooleanFalse = 27, BooleanTrue = 28, ShortZero = 29, IntZero = 30, LongZero = 31, FloatZero = 32, DoubleZero = 33, ByteZero = 34,
        Array = 64, BooleanArray = 66, ByteArray = 67, ShortArray = 68, FloatArray = 69, DoubleArray = 70, StringArray = 71,
        CompressedIntArray = 73, CompressedLongArray = 74, CustomTypeArray = 83, DictionaryArray = 84, HashtableArray = 85,
        CustomTypeSlim = 128;

    // ---------------------------------------------------------------- reading

    public sealed class Reader
    {
        private readonly byte[] _b;
        public int Pos;
        public Reader(byte[] b, int pos = 0) { _b = b; Pos = pos; }
        public bool AtEnd => Pos >= _b.Length;

        public byte U8() => _b[Pos++];
        public short I16() { var v = (short)(_b[Pos] | _b[Pos + 1] << 8); Pos += 2; return v; }
        public ushort U16() { var v = (ushort)(_b[Pos] | _b[Pos + 1] << 8); Pos += 2; return v; }
        /// <summary>Skip n bytes. A method, so a length read in the argument happens before Pos is read
        /// (`Pos += ReadLength()` would add to the old Pos).</summary>
        private void Advance(int n) => Pos += n;
        public byte[] Bytes(int n) { var r = _b.AsSpan(Pos, n).ToArray(); Pos += n; return r; }

        public uint VarU32()
        {
            uint v = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte x = _b[Pos++];
                v |= (uint)(x & 0x7F) << shift;
                if ((x & 0x80) == 0) break;
            }
            return v;
        }

        public ulong VarU64()
        {
            ulong v = 0;
            for (int shift = 0; shift < 70; shift += 7)
            {
                byte x = _b[Pos++];
                v |= (ulong)(x & 0x7F) << shift;
                if ((x & 0x80) == 0) break;
            }
            return v;
        }

        public int VarI32() { uint v = VarU32(); return (int)(v >> 1) ^ -(int)(v & 1); }
        public long VarI64() { ulong v = VarU64(); return (long)(v >> 1) ^ -(long)(v & 1); }

        public float F32() { Span<byte> t = stackalloc byte[4]; for (int i = 0; i < 4; i++) t[3 - i] = _b[Pos + i]; Pos += 4; return BitConverter.ToSingle(t); }
        public double F64() { Span<byte> t = stackalloc byte[8]; for (int i = 0; i < 8; i++) t[7 - i] = _b[Pos + i]; Pos += 8; return BitConverter.ToDouble(t); }
        public string Str() { int n = (int)VarU32(); var s = Encoding.UTF8.GetString(_b, Pos, n); Pos += n; return s; }

        public object? Value() => Value(U8());

        public object? Value(byte type)
        {
            int start = Pos - 1;
            switch (type)
            {
                case Null: return null;
                case Boolean: return U8() != 0;
                case BooleanTrue: return true;
                case BooleanFalse: return false;
                case Byte: return U8();
                case ByteZero: return (byte)0;
                case Short: return I16();
                case ShortZero: return (short)0;
                case Float: return F32();
                case FloatZero: return 0f;
                case Double: return F64();
                case DoubleZero: return 0d;
                case String: return Str();
                case CompressedInt: return VarI32();
                case Int1: return (int)U8();
                case Int1Neg: return -(int)U8();
                case Int2: return (int)U16();
                case Int2Neg: return -(int)U16();
                case IntZero: return 0;
                case CompressedLong: return VarI64();
                case L1: return (long)U8();
                case L1Neg: return -(long)U8();
                case L2: return (long)U16();
                case L2Neg: return -(long)U16();
                case LongZero: return 0L;
                case Hashtable:
                {
                    int n = (int)VarU32();
                    var h = new PhotonHashtable();
                    for (int i = 0; i < n; i++)
                    {
                        var k = Value();
                        var v = Value();
                        if (k != null) h[k] = v;
                    }
                    return h;
                }
                case ObjectArray:
                {
                    int n = (int)VarU32();
                    var a = new object?[n];
                    for (int i = 0; i < n; i++) a[i] = Value();
                    return a;
                }
                case ByteArray: return Bytes((int)VarU32());
                case CompressedIntArray:
                {
                    int n = (int)VarU32();
                    var a = new int[n];
                    for (int i = 0; i < n; i++) a[i] = VarI32();
                    return a;
                }
                case StringArray:
                {
                    int n = (int)VarU32();
                    var a = new string[n];
                    for (int i = 0; i < n; i++) a[i] = Str();
                    return a;
                }
                default:
                    // anything else: skip it and keep the exact bytes
                    Skip(type);
                    var raw = new byte[Pos - start];
                    Buffer.BlockCopy(_b, start, raw, 0, raw.Length);
                    return new RawValue(raw);
            }
        }

        /// <summary>Advance past a value of the given type without building it.</summary>
        public void Skip(byte type)
        {
            if (type >= CustomTypeSlim && type <= 228) { Advance((int)VarU32()); return; }
            switch (type)
            {
                case Null: case BooleanTrue: case BooleanFalse: case ByteZero: case ShortZero: case FloatZero:
                case DoubleZero: case IntZero: case LongZero: return;
                case Boolean: case Byte: case Int1: case Int1Neg: case L1: case L1Neg: Pos += 1; return;
                case Short: case Int2: case Int2Neg: case L2: case L2Neg: Pos += 2; return;
                case Float: Pos += 4; return;
                case Double: Pos += 8; return;
                case CompressedInt: VarU32(); return;
                case CompressedLong: VarU64(); return;
                case String: case ByteArray: Advance((int)VarU32()); return;
                case Custom: Pos += 1; Advance((int)VarU32()); return;
                case Hashtable: { int n = (int)VarU32(); for (int i = 0; i < 2 * n; i++) Skip(U8()); return; }
                case ObjectArray: { int n = (int)VarU32(); for (int i = 0; i < n; i++) Skip(U8()); return; }
                case BooleanArray: { int n = (int)VarU32(); Pos += (n + 7) / 8; return; }
                case ShortArray: Advance(2 * (int)VarU32()); return;
                case FloatArray: Advance(4 * (int)VarU32()); return;
                case DoubleArray: Advance(8 * (int)VarU32()); return;
                case StringArray: { int n = (int)VarU32(); for (int i = 0; i < n; i++) Advance((int)VarU32()); return; }
                case CompressedIntArray: { int n = (int)VarU32(); for (int i = 0; i < n; i++) VarU32(); return; }
                case CompressedLongArray: { int n = (int)VarU32(); for (int i = 0; i < n; i++) VarU64(); return; }
                case HashtableArray: { int n = (int)VarU32(); for (int i = 0; i < n; i++) Skip(Hashtable); return; }
                case CustomTypeArray: { int n = (int)VarU32(); Pos += 1; for (int i = 0; i < n; i++) Advance((int)VarU32()); return; }
                case Dictionary: { var (k, v) = DictTypes(); SkipDictElements(k, v); return; }
                case DictionaryArray: { var (k, v) = DictTypes(); int n = (int)VarU32(); for (int i = 0; i < n; i++) SkipDictElements(k, v); return; }
                case Array:
                {
                    int n = (int)VarU32();
                    for (int i = 0; i < n; i++) Skip(U8());   // each inner array is written with its own type
                    return;
                }
                case OperationRequestType: U8(); SkipParams(); return;
                case EventDataType: U8(); SkipParams(); return;
                case OperationResponseType: U8(); Pos += 2; Skip(U8()); SkipParams(); return;
                default: throw new InvalidDataException($"unknown Protocol18 type {type} at {Pos - 1}");
            }
        }

        /// <summary>Key and value type of a typed dictionary; nested dictionary/array value types carry more type bytes.</summary>
        private (byte Key, byte Value) DictTypes()
        {
            byte k = U8(), v = U8();
            if (v == Dictionary) DictTypes();
            else if (v == Array) { byte t; do t = U8(); while (t == Array); }
            return (k, v == Array ? Unknown : v);
        }

        private void SkipDictElements(byte keyType, byte valueType)
        {
            int n = (int)VarU32();
            for (int i = 0; i < n; i++)
            {
                Skip(keyType == Unknown ? U8() : keyType);
                Skip(valueType == Unknown ? U8() : valueType);
            }
        }

        private void SkipParams() { int n = U8(); for (int i = 0; i < n; i++) { U8(); Skip(U8()); } }

        public Dictionary<byte, object?> Params()
        {
            int n = U8();
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

    // ---------------------------------------------------------------- writing

    public sealed class Writer
    {
        private readonly MemoryStream _m = new();
        public byte[] ToArray() => _m.ToArray();
        public void U8(byte b) => _m.WriteByte(b);
        public void Raw(byte[] b) => _m.Write(b);
        public void I16(short v) { _m.WriteByte((byte)v); _m.WriteByte((byte)(v >> 8)); }

        public void VarU32(uint v)
        {
            while (v >= 0x80) { _m.WriteByte((byte)(v | 0x80)); v >>= 7; }
            _m.WriteByte((byte)v);
        }

        public void VarU64(ulong v)
        {
            while (v >= 0x80) { _m.WriteByte((byte)(v | 0x80)); v >>= 7; }
            _m.WriteByte((byte)v);
        }

        public void Str(string s) { var b = Encoding.UTF8.GetBytes(s); VarU32((uint)b.Length); _m.Write(b); }

        public void Value(object? v)
        {
            switch (v)
            {
                case null: U8(Null); break;
                case RawValue raw: Raw(raw.Bytes); break;
                case bool b: U8(b ? BooleanTrue : BooleanFalse); break;
                case byte b: U8(Byte); U8(b); break;
                case short s: U8(Short); I16(s); break;
                case int i: U8(CompressedInt); VarU32((uint)((i << 1) ^ (i >> 31))); break;
                case long l: U8(CompressedLong); VarU64((ulong)((l << 1) ^ (l >> 63))); break;
                case float f: U8(Float); { var t = BitConverter.GetBytes(f); System.Array.Reverse(t); Raw(t); } break;
                case double d: U8(Double); { var t = BitConverter.GetBytes(d); System.Array.Reverse(t); Raw(t); } break;
                case string s: U8(String); Str(s); break;
                case byte[] b: U8(ByteArray); VarU32((uint)b.Length); Raw(b); break;
                case int[] a:
                    U8(CompressedIntArray); VarU32((uint)a.Length);
                    foreach (var i in a) VarU32((uint)((i << 1) ^ (i >> 31)));
                    break;
                case string[] a: U8(StringArray); VarU32((uint)a.Length); foreach (var s in a) Str(s); break;
                case PhotonHashtable h:
                    U8(Hashtable); VarU32((uint)h.Count);
                    foreach (var (k, val) in h) { Value(k); Value(val); }
                    break;
                case object?[] a: U8(ObjectArray); VarU32((uint)a.Length); foreach (var x in a) Value(x); break;
                case Dictionary<string, object?> d:   // Dictionary<string, object>: key type String, values typed
                    U8(Dictionary); U8(String); U8(Unknown); VarU32((uint)d.Count);
                    foreach (var (k, val) in d) { Str(k); Value(val); }
                    break;
                default: throw new NotSupportedException($"cannot serialize {v.GetType().Name}");
            }
        }

        public void Params(Dictionary<byte, object?>? p)
        {
            U8((byte)(p?.Count ?? 0));
            if (p == null) return;
            foreach (var (k, v) in p) { U8(k); Value(v); }
        }
    }

    /// <summary>Operation response body (no message header): code, return code, debug message, parameters.</summary>
    public static byte[] OperationResponse(byte code, short returnCode, string? debugMessage, Dictionary<byte, object?>? parameters)
    {
        var w = new Writer();
        w.U8(code);
        w.I16(returnCode);
        w.Value(debugMessage);
        w.Params(parameters);
        return w.ToArray();
    }

    /// <summary>Event body (no message header): code, parameters.</summary>
    public static byte[] Event(byte code, Dictionary<byte, object?>? parameters)
    {
        var w = new Writer();
        w.U8(code);
        w.Params(parameters);
        return w.ToArray();
    }

    /// <summary>Short human-readable dump for the log.</summary>
    public static string Describe(object? v, int depth = 0) => v switch
    {
        null => "null",
        string s => $"\"{(s.Length > 60 ? s[..60] + "…" : s)}\"",
        byte[] b => $"bytes[{b.Length}]",
        PhotonHashtable h when depth < 2 => "{" + string.Join(", ", h.Take(12).Select(kv => $"{Describe(kv.Key, depth + 1)}: {Describe(kv.Value, depth + 1)}")) + (h.Count > 12 ? ", …" : "") + "}",
        PhotonHashtable h => $"{{{h.Count} entries}}",
        int[] a => "[" + string.Join(",", a.Take(16)) + "]",
        string[] a => "[" + string.Join(",", a.Take(8).Select(x => Describe(x, depth + 1))) + "]",
        object?[] a when depth < 2 => "[" + string.Join(", ", a.Take(8).Select(x => Describe(x, depth + 1))) + (a.Length > 8 ? ", …" : "") + "]",
        object?[] a => $"[{a.Length} items]",
        _ => v.ToString() ?? "?",
    };
}
