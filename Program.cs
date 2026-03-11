using OHTC.Core;
using OHTC.Domain;
using OHTC.Infrastructure;

var points = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase)
{
    ["P1"] = new("P1", 0, 0),
    ["P2"] = new("P2", 10, 0),
    ["P3"] = new("P3", 20, 0),
    ["P4"] = new("P4", 20, 10),
    ["P5"] = new("P5", 10, 10),
    ["P6"] = new("P6", 0, 10)
};

var segments = new Dictionary<string, Segment>(StringComparer.OrdinalIgnoreCase)
{
    ["S1"] = new("S1", "P1", "P2", 10, true, SegmentTrafficLevel.Normal, 2.0),
    ["S2"] = new("S2", "P2", "P3", 10, true, SegmentTrafficLevel.Busy, 2.0),
    ["S3"] = new("S3", "P3", "P4", 10, true, SegmentTrafficLevel.Unavailable, 2.0),
    ["S4"] = new("S4", "P4", "P5", 10, true, SegmentTrafficLevel.Normal, 2.0),
    ["S5"] = new("S5", "P5", "P6", 10, true, SegmentTrafficLevel.Normal, 2.0),
    ["S6"] = new("S6", "P6", "P1", 10, true, SegmentTrafficLevel.Normal, 2.0),
    ["S7"] = new("S7", "P2", "P5", 10, true, SegmentTrafficLevel.Normal, 2.0)
};

var graph = new GraphEngine(points, segments);
var planner = new PathPlanner(graph);
var reservation = new ReservationTable();
var allocator = new VehicleAllocator(planner, reservation);

var redis = new InMemoryRedisClient();
var snapshots = new SnapshotManager(redis);

await redis.SetAsync("OHT:V001", $"P1|{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}|None", default);
await redis.SetAsync("OHT:V002", $"P3|{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}|None", default);
await redis.SetAsync("OHT:V003", $"P6|{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}|Move", default);

await snapshots.RefreshAsync("V001", default);
await snapshots.RefreshAsync("V002", default);
await snapshots.RefreshAsync("V003", default);

var tasks = new List<TransportTask>
{
    new() { TaskId = "T001", SourcePoint = "P2", DestinationPoint = "P4", Priority = 100 },
    new() { TaskId = "T002", SourcePoint = "P6", DestinationPoint = "P3", Priority = 90 },
    new() { TaskId = "T003", SourcePoint = "P1", DestinationPoint = "P5", Priority = 80 }
};

var taskRepository = new InMemoryTaskRepository(tasks);
var assignmentStore = new InMemoryOhtAssignmentStore();
var dispatcher = new ConsoleDispatchService();

var schedulerA = new TaskSchedulerService(taskRepository, assignmentStore, snapshots, allocator, dispatcher, "OHTC-A");
var schedulerB = new TaskSchedulerService(taskRepository, assignmentStore, snapshots, allocator, dispatcher, "OHTC-B");

var scheduled = await Task.WhenAll(
    schedulerA.ScheduleOnceAsync(batchSize: 20, default),
    schedulerB.ScheduleOnceAsync(batchSize: 20, default));

Console.WriteLine($"Total dispatched: {scheduled.Sum()}");
foreach (var task in taskRepository.Dump())
{
    Console.WriteLine(task);
}
