using System.Collections.Concurrent;
using OHTC.Domain;

namespace OHTC.Core;

public interface IRedisClient
{
    Task<string?> GetAsync(string key, CancellationToken ct);
    Task SetAsync(string key, string value, CancellationToken ct);
}

public sealed class SnapshotManager
{
    private readonly IRedisClient _redis;
    private readonly ConcurrentDictionary<string, OhtSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SnapshotManager(IRedisClient redis)
    {
        _redis = redis;
    }

    public async Task<OhtSnapshot?> RefreshAsync(string ohtCode, CancellationToken ct)
    {
        var key = $"OHT:{ohtCode}";
        var raw = await _redis.GetAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var snapshot = Parse(ohtCode, raw);
        _cache[ohtCode] = snapshot;
        return snapshot;
    }

    public OhtSnapshot? GetCached(string ohtCode)
        => _cache.TryGetValue(ohtCode, out var value) ? value : null;

    public IReadOnlyCollection<OhtSnapshot> GetAllCached()
        => _cache.Values.ToList();

    public async Task UpdateStatusAsync(OhtSnapshot snapshot, CancellationToken ct)
    {
        _cache[snapshot.OhtCode] = snapshot;
        var payload = $"{snapshot.CurrentPoint}|{snapshot.Timestamp.ToUnixTimeMilliseconds()}|{snapshot.CommandCode}";
        await _redis.SetAsync($"OHT:{snapshot.OhtCode}", payload, ct);
    }

    public static OhtSnapshot Parse(string ohtCode, string redisValue)
    {
        var parts = redisValue.Split('|');
        var currentPoint = parts.ElementAtOrDefault(0) ?? "UNKNOWN";
        var ts = long.TryParse(parts.ElementAtOrDefault(1), out var unixTs)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixTs)
            : DateTimeOffset.UtcNow;

        var command = Enum.TryParse<OhtCommandCode>(parts.ElementAtOrDefault(2), ignoreCase: true, out var parsed)
            ? parsed
            : OhtCommandCode.None;

        var status = command == OhtCommandCode.None ? OhtStatus.Idle : OhtStatus.Busy;

        return new OhtSnapshot(ohtCode, currentPoint, ts, command, status);
    }
}
