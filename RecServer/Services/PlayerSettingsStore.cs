using System.Text.Json;

namespace RecServer.Services;

/// <summary>Key/value player settings the client uploads (PUT /playersettings, form fields key and value) and reads back.
/// Persisted to data/playersettings.json so onboarding flags such as Recroom.AccountCreation.HasChosenUsername survive restarts
/// (otherwise the account-creation screens appear on every launch).</summary>
public class PlayerSettingsStore
{
    private readonly string _file;
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _values = new();

    public PlayerSettingsStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _file = Path.Combine(dataDir, "playersettings.json");
        try { if (File.Exists(_file)) _values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_file)) ?? new(); }
        catch { /* start empty */ }
    }

    public void Set(string key, string value)
    {
        lock (_lock) { _values[key] = value; File.WriteAllText(_file, JsonSerializer.Serialize(_values)); }
    }

    public object[] All()
    {
        lock (_lock) return _values.Select(kv => (object)new { Key = kv.Key, Value = kv.Value }).ToArray();
    }
}
