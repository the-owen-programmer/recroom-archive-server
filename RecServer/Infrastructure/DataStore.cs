using System.Text.Json;
using System.Text.Json.Nodes;

namespace RecServer.Infrastructure;

/// <summary>
/// File-backed storage under the data directory (REC_DATA, default <c>&lt;repo&gt;/data</c>):
/// <list type="bullet">
/// <item><c>state/</c>: what the server writes (cheers, favorites, visited rooms); delete it to start over.</item>
/// <item><c>rooms/*.json</c>: archived room definitions, added to the built-in catalog.</item>
/// <item><c>img/</c>, <c>cdn/</c>: archived images and room/invention blobs served by img.rec.net and cdn.rec.net.</item>
/// <item><c>overrides/&lt;host&gt;/&lt;path&gt;.json</c>: canned responses that win over the built-in endpoints (see OverrideMiddleware).</item>
/// </list>
/// No ASP.NET types here so the services built on it can later move into the in-game mod unchanged.
/// </summary>
public class DataStore
{
    /// <summary>Archived dumps sometimes write ids as strings ("RoomId": "170126"); those are read as numbers too.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, PropertyNamingPolicy = null,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };
    private readonly object _lock = new();

    public string Root { get; }

    public DataStore(string root)
    {
        Root = Path.GetFullPath(root);
        foreach (var d in new[] { "state", "rooms", "img", "cdn", "overrides" })
            Directory.CreateDirectory(Path.Combine(Root, d));
    }

    /// <summary>Read <c>state/&lt;name&gt;.json</c>, creating it from <paramref name="seed"/> the first time.</summary>
    public T Load<T>(string name, Func<T> seed)
    {
        var file = Path.Combine(Root, "state", name + ".json");
        lock (_lock)
        {
            if (File.Exists(file))
            {
                try { return JsonSerializer.Deserialize<T>(File.ReadAllText(file), Json) ?? seed(); }
                catch (JsonException e) { Console.WriteLine($"state/{name}.json is not valid ({e.Message}); using defaults"); return seed(); }
            }
            var value = seed();
            File.WriteAllText(file, JsonSerializer.Serialize(value, Json));
            return value;
        }
    }

    public void Save<T>(string name, T value)
    {
        lock (_lock) File.WriteAllText(Path.Combine(Root, "state", name + ".json"), JsonSerializer.Serialize(value, Json));
    }

    /// <summary>Every <c>*.json</c> file in a data sub-directory, parsed; bad files are reported and skipped.</summary>
    public IEnumerable<(string File, JsonNode Node)> ReadAll(string subDir)
    {
        var dir = Path.Combine(Root, subDir);
        if (!Directory.Exists(dir)) yield break;
        foreach (var f in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).Order())
        {
            JsonNode? node = null;
            try { node = JsonNode.Parse(File.ReadAllText(f)); }
            catch (JsonException e) { Console.WriteLine($"skipping {f}: {e.Message}"); }
            if (node != null) yield return (f, node);
        }
    }

    /// <summary>
    /// Resolve <paramref name="relative"/> inside the data sub-directory <paramref name="subDir"/>, or null if it would leave it
    /// (the path comes from the request URL).
    /// </summary>
    public string? SafePath(string subDir, string relative)
    {
        var baseDir = Path.GetFullPath(Path.Combine(Root, subDir)) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(baseDir, relative.TrimStart('/', '\\')));
        return full.StartsWith(baseDir, StringComparison.Ordinal) ? full : null;
    }
}
