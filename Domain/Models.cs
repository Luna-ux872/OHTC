namespace OHTC.Domain;

public sealed record Point(string Id, double X, double Y);

public sealed record Segment(
    string Id,
    string FromPoint,
    string ToPoint,
    double Length,
    bool IsOneWay,
    SegmentTrafficLevel TrafficLevel = SegmentTrafficLevel.Normal,
    double SpeedMetersPerSecond = 1.0);

public enum SegmentTrafficLevel
{
    Normal,
    Busy,
    Unavailable
}

public enum OhtCommandCode
{
    None,
    Move,
    Load,
    Unload
}

public enum OhtStatus
{
    Idle,
    Busy,
    Offline
}

public sealed record OhtSnapshot(
    string OhtCode,
    string CurrentPoint,
    DateTimeOffset Timestamp,
    OhtCommandCode CommandCode,
    OhtStatus Status);

public enum TaskStatus
{
    Pending,
    Claimed,
    Assigned,
    Dispatched,
    Completed,
    Failed
}

public sealed class TransportTask
{
    public required string TaskId { get; init; }
    public required string SourcePoint { get; init; }
    public required string DestinationPoint { get; init; }
    public int Priority { get; init; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    public string? ClaimedByInstance { get; set; }
    public string? AssignedOht { get; set; }

    public override string ToString()
        => $"Task={TaskId},Priority={Priority},Status={Status},Source={SourcePoint},Dest={DestinationPoint},OHT={AssignedOht}";
}

public sealed class PathResult
{
    public required IReadOnlyList<string> PointPath { get; init; }
    public required double Cost { get; init; }

    public static PathResult Empty { get; } = new() { PointPath = Array.Empty<string>(), Cost = double.PositiveInfinity };
}
