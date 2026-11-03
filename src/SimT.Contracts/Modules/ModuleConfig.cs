namespace SimT.Contracts.Modules;

/// <summary>
/// Flat, validated access to the settings a module declares in <c>module.json</c>. Defaults
/// live in module code; the manifest only overrides them, and a malformed override fails at
/// boot rather than silently falling back.
/// </summary>
public sealed class ModuleConfig
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public ModuleConfig(IReadOnlyDictionary<string, string>? values, string moduleId)
    {
        _values = values ?? new Dictionary<string, string>(StringComparer.Ordinal);
        ModuleId = moduleId;
    }

    public string ModuleId { get; }

    public IReadOnlyDictionary<string, string> Values => _values;

    public bool Has(string key) => _values.ContainsKey(key);

    public long GetLong(string key, long fallback) =>
        _values.TryGetValue(key, out var raw) && long.TryParse(raw, out var value) ? value : fallback;

    public int GetInt(string key, int fallback) => (int)GetLong(key, fallback);

    public string GetString(string key, string fallback) =>
        _values.TryGetValue(key, out var raw) ? raw : fallback;

    /// <summary>Strict read: a missing or malformed value is a packaging bug.</summary>
    public long RequireLong(string key)
    {
        if (!_values.TryGetValue(key, out var raw))
        {
            throw new SimException("module.config_missing", $"Module '{ModuleId}' requires config '{key}'.");
        }

        return long.TryParse(raw, out var value)
            ? value
            : throw new SimException("module.config_invalid", $"Module '{ModuleId}' config '{key}' must be a number, got '{raw}'.");
    }
}
