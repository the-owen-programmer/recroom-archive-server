using System.Collections.Concurrent;
using System.Text.Json;

namespace RecServer.Infrastructure;

/// <summary>One request seen by the server (kept in memory and appended to requests.jsonl).</summary>
public record LoggedRequest(
    DateTime Time, string Host, string Method, string Path, string Query,
    string? ContentType, string? UserAgent, Dictionary<string, string> Headers, string? Body,
    int Status, bool Handled);

/// <summary>Ring buffer plus JSON-lines file of every request; the main way to discover what the client expects.</summary>
public class RequestLog
{
    private readonly string _file;
    private readonly ConcurrentQueue<LoggedRequest> _recent = new();
    private readonly object _lock = new();

    public RequestLog(string file) => _file = file;

    public void Add(LoggedRequest r)
    {
        _recent.Enqueue(r);
        while (_recent.Count > 500) _recent.TryDequeue(out _);
        var line = JsonSerializer.Serialize(r);
        lock (_lock) File.AppendAllText(_file, line + "\n");
        Console.WriteLine($"{r.Time:HH:mm:ss.fff} {(r.Handled ? "    " : "MISS")} {r.Status} {r.Method} {r.Host}{r.Path}{r.Query}");
    }

    public IReadOnlyList<LoggedRequest> Recent(int take) => _recent.TakeLast(take).ToList();
}
