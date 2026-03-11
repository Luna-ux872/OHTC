# OHTC Scheduler (.NET 8)

工业级 OHT 调度系统示例，实现了：
- Path Planning（A*）
- Vehicle Allocation（最近 + Idle + 无冲突）
- Task Scheduling（优先级 + 多实例并发）
- Deadlock Avoidance（路径预占）
- Redis 状态同步（示例使用内存 Redis 适配器）

## 项目结构
- `Domain/Models.cs`：核心实体。
- `Core/GraphEngine.cs`：图构建。
- `Core/PathPlanner.cs`：A* 路径规划。
- `Core/DeadlockAvoidance.cs`：路径资源预占。
- `Core/SnapshotManager.cs`：Redis 快照管理。
- `Core/Scheduling.cs`：任务调度与车辆分配。
- `Infrastructure/Adapters.cs`：Redis/Dispatch 基础设施适配。
- `Docs/Architecture.md`：架构、并发与流程说明。

## 运行
```bash
dotnet run
```

> 说明：当前示例采用内存实现，生产环境请将 `IRedisClient` 与 `ITaskRepository/IOhtAssignmentStore` 替换为真实 Redis + DB 事务实现。
