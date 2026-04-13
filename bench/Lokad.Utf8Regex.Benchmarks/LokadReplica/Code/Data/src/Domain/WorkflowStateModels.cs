namespace LokadReplica\\Code.Domain.Workflow;

public sealed class ProjectionSnapshotState
{
    public required string SnapshotId { get; init; }
    public required string ProjectionId { get; init; }
    public required string TenantId { get; init; }
    public required string ReplicaName { get; init; }
    public DateTimeOffset ComputedAtUtc { get; init; }
    public DateTimeOffset ValidUntilUtc { get; init; }
    public bool RequiresRefresh { get; init; }
    public long PendingOrders { get; init; }
    public long PendingInvoices { get; init; }
    public long PendingTransfers { get; init; }
}

public sealed class ProjectionRefreshState
{
    public required string ProjectionId { get; init; }
    public required string ProjectionKind { get; init; }
    public required string CurrentStage { get; init; }
    public required string CurrentOwner { get; init; }
    public required string LastCommandId { get; init; }
    public required string LastCorrelationId { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public bool WarmupCompleted { get; init; }
    public bool DiagnosticsEnabled { get; init; }
}

public sealed class ProjectionReplayWindow
{
    public required string ProjectionId { get; init; }
    public required string ReplayKind { get; init; }
    public required string Route { get; init; }
    public required string ReplayReason { get; init; }
    public DateTimeOffset WindowStartUtc { get; init; }
    public DateTimeOffset WindowEndUtc { get; init; }
    public int ItemCount { get; init; }
}

public readonly record struct ProjectionLagSample(
    string ProjectionId,
    long ElapsedMilliseconds,
    string ReplicaName,
    string Route);

public readonly record struct ProjectionHealthSample(
    string ProjectionId,
    string Status,
    int StatusCode,
    long ElapsedMilliseconds);

public readonly record struct ProjectionCursorState(
    string ProjectionId,
    long Offset,
    string? ContinuationToken,
    string? ShardKey);

public readonly record struct ProjectionCheckpointRecord(
    string ProjectionId,
    long SequenceNumber,
    DateTimeOffset CheckpointedAtUtc);

public sealed class ProjectionRefreshDescriptor
{
    public required string ProjectionId { get; init; }
    public required string ProjectionKind { get; init; }
    public required string InputRoute { get; init; }
    public required string OutputRoute { get; init; }
    public required string ErrorRoute { get; init; }
    public bool UsesHttpClient { get; init; }
    public bool UsesReplayWindow { get; init; }
    public bool UsesWarmupPath { get; init; }
}

public sealed class ProjectionStageDescriptor
{
    public required string StageName { get; init; }
    public required string StageKind { get; init; }
    public required string ServiceName { get; init; }
    public required string HandlerName { get; init; }
    public bool IsCritical { get; init; }
    public bool EmitsDiagnostics { get; init; }
}

public sealed class ProjectionRegistrationDescriptor
{
    public required string Service { get; init; }
    public required string Implementation { get; init; }
    public required string Lifetime { get; init; }
    public bool UsesHttpClient { get; init; }
}

public interface IProjectionRefreshHandler
{
    Task<ProjectionRefreshState> RefreshAsync(string projectionId, CancellationToken cancellationToken);
}

public interface IProjectionReplayHandler
{
    IAsyncEnumerable<ProjectionReplayWindow> ReplayAsync(string projectionId, CancellationToken cancellationToken);
}

public interface IProjectionSnapshotHandler
{
    ValueTask<ProjectionSnapshotState?> GetSnapshotAsync(string projectionId, CancellationToken cancellationToken);
}
