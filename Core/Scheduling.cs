using System.Collections.Concurrent;
using OHTC.Domain;

namespace OHTC.Core;

public interface ITaskRepository
{
    Task<IReadOnlyList<TransportTask>> LoadPendingTasksAsync(int batchSize, CancellationToken ct);
    Task<bool> TryClaimTaskAsync(string taskId, string instanceId, CancellationToken ct);
    Task<bool> TryAssignVehicleAsync(string taskId, string ohtCode, CancellationToken ct);
    Task MarkDispatchedAsync(string taskId, CancellationToken ct);
}

public interface IOhtAssignmentStore
{
    Task<bool> TryLockVehicleAsync(string ohtCode, string taskId, CancellationToken ct);
    Task ReleaseVehicleAsync(string ohtCode, CancellationToken ct);
}

public sealed class VehicleAllocator
{
    private readonly PathPlanner _planner;
    private readonly ReservationTable _reservationTable;

    public VehicleAllocator(PathPlanner planner, ReservationTable reservationTable)
    {
        _planner = planner;
        _reservationTable = reservationTable;
    }

    public (string OhtCode, PathResult Route)? Allocate(
        TransportTask task,
        IReadOnlyCollection<OhtSnapshot> candidates)
    {
        var bestCost = double.PositiveInfinity;
        (string OhtCode, PathResult Route)? selected = null;

        foreach (var oht in candidates)
        {
            if (oht.Status != OhtStatus.Idle)
            {
                continue;
            }

            var toSource = _planner.FindShortestPath(oht.CurrentPoint, task.SourcePoint);
            var sourceToDest = _planner.FindShortestPath(task.SourcePoint, task.DestinationPoint);
            if (double.IsInfinity(toSource.Cost) || double.IsInfinity(sourceToDest.Cost))
            {
                continue;
            }

            var mergedPath = toSource.PointPath.Concat(sourceToDest.PointPath.Skip(1)).ToList();
            var total = toSource.Cost + sourceToDest.Cost;

            if (total < bestCost && _reservationTable.TryReservePath(oht.OhtCode, mergedPath))
            {
                bestCost = total;
                selected = (oht.OhtCode, new PathResult { PointPath = mergedPath, Cost = total });
            }
        }

        return selected;
    }
}

public interface ICommandDispatcher
{
    Task SendDispatchAsync(string ohtCode, TransportTask task, PathResult route, CancellationToken ct);
}

public sealed class TaskSchedulerService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IOhtAssignmentStore _assignmentStore;
    private readonly SnapshotManager _snapshotManager;
    private readonly VehicleAllocator _allocator;
    private readonly ICommandDispatcher _dispatcher;
    private readonly string _instanceId;

    public TaskSchedulerService(
        ITaskRepository taskRepository,
        IOhtAssignmentStore assignmentStore,
        SnapshotManager snapshotManager,
        VehicleAllocator allocator,
        ICommandDispatcher dispatcher,
        string instanceId)
    {
        _taskRepository = taskRepository;
        _assignmentStore = assignmentStore;
        _snapshotManager = snapshotManager;
        _allocator = allocator;
        _dispatcher = dispatcher;
        _instanceId = instanceId;
    }

    public async Task<int> ScheduleOnceAsync(int batchSize, CancellationToken ct)
    {
        var tasks = await _taskRepository.LoadPendingTasksAsync(batchSize, ct);
        var ordered = tasks.OrderByDescending(x => x.Priority).ThenBy(x => x.TaskId).ToList();

        var dispatched = 0;

        foreach (var task in ordered)
        {
            if (!await _taskRepository.TryClaimTaskAsync(task.TaskId, _instanceId, ct))
            {
                continue;
            }

            var snapshots = _snapshotManager.GetAllCached();
            var allocation = _allocator.Allocate(task, snapshots);
            if (allocation is null)
            {
                continue;
            }

            var (ohtCode, route) = allocation.Value;

            if (!await _assignmentStore.TryLockVehicleAsync(ohtCode, task.TaskId, ct))
            {
                continue;
            }

            if (!await _taskRepository.TryAssignVehicleAsync(task.TaskId, ohtCode, ct))
            {
                await _assignmentStore.ReleaseVehicleAsync(ohtCode, ct);
                continue;
            }

            await _dispatcher.SendDispatchAsync(ohtCode, task, route, ct);
            await _taskRepository.MarkDispatchedAsync(task.TaskId, ct);
            dispatched++;
        }

        return dispatched;
    }
}

public sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<string, TransportTask> _tasks;
    private readonly object _sync = new();

    public InMemoryTaskRepository(IEnumerable<TransportTask> seeds)
    {
        _tasks = new ConcurrentDictionary<string, TransportTask>(
            seeds.ToDictionary(x => x.TaskId, x => x),
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<TransportTask>> LoadPendingTasksAsync(int batchSize, CancellationToken ct)
    {
        var result = _tasks.Values
            .Where(x => x.Status == TaskStatus.Pending)
            .OrderByDescending(x => x.Priority)
            .Take(batchSize)
            .ToList();
        return Task.FromResult((IReadOnlyList<TransportTask>)result);
    }

    public Task<bool> TryClaimTaskAsync(string taskId, string instanceId, CancellationToken ct)
    {
        lock (_sync)
        {
            if (!_tasks.TryGetValue(taskId, out var task) || task.Status != TaskStatus.Pending)
            {
                return Task.FromResult(false);
            }

            task.Status = TaskStatus.Claimed;
            task.ClaimedByInstance = instanceId;
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryAssignVehicleAsync(string taskId, string ohtCode, CancellationToken ct)
    {
        lock (_sync)
        {
            if (!_tasks.TryGetValue(taskId, out var task) || task.Status != TaskStatus.Claimed)
            {
                return Task.FromResult(false);
            }

            task.AssignedOht = ohtCode;
            task.Status = TaskStatus.Assigned;
            return Task.FromResult(true);
        }
    }

    public Task MarkDispatchedAsync(string taskId, CancellationToken ct)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Status = TaskStatus.Dispatched;
        }

        return Task.CompletedTask;
    }

    public IReadOnlyCollection<TransportTask> Dump() => _tasks.Values.OrderBy(x => x.TaskId).ToList();
}

public sealed class InMemoryOhtAssignmentStore : IOhtAssignmentStore
{
    private readonly ConcurrentDictionary<string, string> _busy = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> TryLockVehicleAsync(string ohtCode, string taskId, CancellationToken ct)
        => Task.FromResult(_busy.TryAdd(ohtCode, taskId));

    public Task ReleaseVehicleAsync(string ohtCode, CancellationToken ct)
    {
        _busy.TryRemove(ohtCode, out _);
        return Task.CompletedTask;
    }
}
