using System.Collections.Concurrent;

namespace OHTC.Core;

public sealed class ReservationTable
{
    // point -> oht
    private readonly ConcurrentDictionary<string, string> _pointReservations = new(StringComparer.OrdinalIgnoreCase);

    public bool TryReservePath(string ohtCode, IReadOnlyList<string> path)
    {
        var acquired = new List<string>();

        foreach (var point in path)
        {
            if (_pointReservations.TryAdd(point, ohtCode))
            {
                acquired.Add(point);
                continue;
            }

            if (_pointReservations.TryGetValue(point, out var owner) && string.Equals(owner, ohtCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var rollback in acquired)
            {
                _pointReservations.TryRemove(rollback, out _);
            }

            return false;
        }

        return true;
    }

    public void ReleaseByOht(string ohtCode)
    {
        foreach (var kv in _pointReservations)
        {
            if (string.Equals(kv.Value, ohtCode, StringComparison.OrdinalIgnoreCase))
            {
                _pointReservations.TryRemove(kv.Key, out _);
            }
        }
    }
}
