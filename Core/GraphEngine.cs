using OHTC.Domain;

namespace OHTC.Core;

public sealed class GraphEngine
{
    private readonly Dictionary<string, Point> _points;
    private readonly Dictionary<string, List<(string To, double Cost, string SegmentId)>> _adjacency;

    public GraphEngine(IReadOnlyDictionary<string, Point> points, IReadOnlyDictionary<string, Segment> segments)
    {
        _points = points.ToDictionary(kv => kv.Key, kv => kv.Value);
        _adjacency = new(StringComparer.OrdinalIgnoreCase);

        foreach (var pointId in _points.Keys)
        {
            _adjacency[pointId] = new();
        }

        foreach (var segment in segments.Values)
        {
            if (!_adjacency.ContainsKey(segment.FromPoint) || !_adjacency.ContainsKey(segment.ToPoint))
            {
                continue;
            }

            _adjacency[segment.FromPoint].Add((segment.ToPoint, segment.Length, segment.Id));

            if (!segment.IsOneWay)
            {
                _adjacency[segment.ToPoint].Add((segment.FromPoint, segment.Length, segment.Id));
            }
        }
    }

    public IEnumerable<(string To, double Cost, string SegmentId)> GetNeighbors(string pointId)
        => _adjacency.TryGetValue(pointId, out var neighbors) ? neighbors : Enumerable.Empty<(string, double, string)>();

    public bool ContainsPoint(string pointId) => _points.ContainsKey(pointId);

    public Point GetPoint(string pointId) => _points[pointId];

    public double Heuristic(string fromPointId, string toPointId)
    {
        var a = _points[fromPointId];
        var b = _points[toPointId];
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
