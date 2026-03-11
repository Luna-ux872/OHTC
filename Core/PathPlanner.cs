using OHTC.Domain;

namespace OHTC.Core;

public sealed class PathPlanner
{
    private readonly GraphEngine _graph;

    public PathPlanner(GraphEngine graph)
    {
        _graph = graph;
    }

    public PathResult FindShortestPath(string startPoint, string endPoint, DynamicCostContext? costContext = null)
    {
        if (!_graph.ContainsPoint(startPoint) || !_graph.ContainsPoint(endPoint))
        {
            return PathResult.Empty;
        }

        var context = costContext ?? DynamicCostContext.Default;

        var frontier = new PriorityQueue<string, double>();
        var cameFrom = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var gScore = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        frontier.Enqueue(startPoint, 0);
        cameFrom[startPoint] = null;
        gScore[startPoint] = 0;

        while (frontier.TryDequeue(out var current, out _))
        {
            if (string.Equals(current, endPoint, StringComparison.OrdinalIgnoreCase))
            {
                return BuildPathResult(endPoint, cameFrom, gScore[endPoint]);
            }

            foreach (var edge in _graph.GetNeighbors(current))
            {
                var segmentCost = EstimateSegmentTravelTime(edge, context);
                if (double.IsInfinity(segmentCost))
                {
                    continue;
                }

                var candidate = gScore[current] + segmentCost;
                if (!gScore.TryGetValue(edge.To, out var existing) || candidate < existing)
                {
                    gScore[edge.To] = candidate;
                    cameFrom[edge.To] = current;
                    var priority = candidate + _graph.Heuristic(edge.To, endPoint, context);
                    frontier.Enqueue(edge.To, priority);
                }
            }
        }

        return PathResult.Empty;
    }

    private static double EstimateSegmentTravelTime(SegmentEdge edge, DynamicCostContext context)
    {
        if (edge.TrafficLevel == SegmentTrafficLevel.Unavailable)
        {
            return double.PositiveInfinity;
        }

        var speed = Math.Max(0.1, edge.SpeedMetersPerSecond);
        var baseTime = edge.Length / speed;

        return edge.TrafficLevel switch
        {
            SegmentTrafficLevel.Busy => baseTime * context.BusyPenaltyFactor,
            _ => baseTime
        };
    }

    private static PathResult BuildPathResult(
        string endPoint,
        IReadOnlyDictionary<string, string?> cameFrom,
        double cost)
    {
        var path = new List<string>();
        string? cursor = endPoint;

        while (cursor is not null)
        {
            path.Add(cursor);
            cursor = cameFrom[cursor];
        }

        path.Reverse();
        return new PathResult { PointPath = path, Cost = cost };
    }
}
