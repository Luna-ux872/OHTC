using System.Collections.Concurrent;
using OHTC.Core;
using OHTC.Domain;

namespace OHTC.Infrastructure;

public sealed class InMemoryRedisClient : IRedisClient
{
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<string?> GetAsync(string key, CancellationToken ct)
    {
        _store.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, CancellationToken ct)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }
}

public sealed class ConsoleDispatchService : ICommandDispatcher
{
    public Task SendDispatchAsync(string ohtCode, TransportTask task, PathResult route, CancellationToken ct)
    {
        Console.WriteLine($"[DISPATCH] OHT={ohtCode} Task={task.TaskId} Route={string.Join("->", route.PointPath)} Cost={route.Cost:F1}");
        return Task.CompletedTask;
    }
}
