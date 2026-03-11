using OHTC.Domain;

namespace OHTC.Core;

public sealed class GraphEngine
{
    private readonly Dictionary<string, Point> _points;
    private readonly Dictionary<string, List<SegmentEdge>> _adjacency;

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

            _adjacency[segment.FromPoint].Add(new SegmentEdge(segment.Id, segment.ToPoint, segment.Length, segment.TrafficLevel, segment.SpeedMetersPerSecond));

            if (!segment.IsOneWay)
            {
                _adjacency[segment.ToPoint].Add(new SegmentEdge(segment.Id, segment.FromPoint, segment.Length, segment.TrafficLevel, segment.SpeedMetersPerSecond));
            }
        }
    }

    public IEnumerable<SegmentEdge> GetNeighbors(string pointId)
        => _adjacency.TryGetValue(pointId, out var neighbors) ? neighbors : Enumerable.Empty<SegmentEdge>();

    public bool ContainsPoint(string pointId) => _points.ContainsKey(pointId);

    public Point GetPoint(string pointId) => _points[pointId];

    public double Heuristic(string fromPointId, string toPointId, DynamicCostContext context)
    {
        var a = _points[fromPointId];
        var b = _points[toPointId];
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        return distance / Math.Max(0.1, context.DefaultSpeedMetersPerSecond);
    }
}

public sealed record SegmentEdge(
    string SegmentId,
    string To,
    double Length,
    SegmentTrafficLevel TrafficLevel,
    double SpeedMetersPerSecond);

public sealed record DynamicCostContext(
    double BusyPenaltyFactor,
    double DefaultSpeedMetersPerSecond)
{
    public static DynamicCostContext Default { get; } = new(BusyPenaltyFactor: 1.6, DefaultSpeedMetersPerSecond: 1.0);
}
