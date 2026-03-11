using OHTC.Domain;

namespace OHTC.Core;

public sealed class PathPlanner
{
    private readonly GraphEngine _graph;

    public PathPlanner(GraphEngine graph)
    {
        _graph = graph;
    }

    public PathResult FindShortestPath(string startPoint, string endPoint)
    {
        if (!_graph.ContainsPoint(startPoint) || !_graph.ContainsPoint(endPoint))
        {
            return PathResult.Empty;
        }

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

            foreach (var (to, cost, _) in _graph.GetNeighbors(current))
            {
                var candidate = gScore[current] + cost;
                if (!gScore.TryGetValue(to, out var existing) || candidate < existing)
                {
                    gScore[to] = candidate;
                    cameFrom[to] = current;
                    var priority = candidate + _graph.Heuristic(to, endPoint);
                    frontier.Enqueue(to, priority);
                }
            }
        }

        return PathResult.Empty;
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
