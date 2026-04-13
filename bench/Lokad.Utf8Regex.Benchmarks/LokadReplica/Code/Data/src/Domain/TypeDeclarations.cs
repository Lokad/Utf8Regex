namespace LokadReplica\\Code.Domain;

public sealed class CustomerProfile
{
    public required string CustomerId { get; init; }
    public required string TenantId { get; init; }
    public required string Status { get; init; }
    public required string PreferredRegion { get; init; }
    public required string Segment { get; init; }
    public required string DefaultCurrency { get; init; }
}

public sealed class CustomerProjection
{
    public required string ProjectionId { get; init; }
    public required string CustomerId { get; init; }
    public required string TenantId { get; init; }
    public DateTimeOffset LastComputedAt { get; init; }
    public DateTimeOffset SnapshotValidUntilUtc { get; init; }
    public bool RequiresRefresh { get; init; }
}

public sealed class DispatchEnvelope
{
    public required string CorrelationId { get; init; }
    public required string RoutingKey { get; init; }
    public required string PayloadType { get; init; }
    public required string WorkflowName { get; init; }
    public int AttemptCount { get; init; }
}

public sealed class ProjectionSnapshot
{
    public required string SnapshotId { get; init; }
    public required string SnapshotKind { get; init; }
    public required string TenantId { get; init; }
    public int Version { get; init; }
    public long PendingOrders { get; init; }
    public long FailedOrders { get; init; }
}

public readonly record struct DispatchResult(int StatusCode, string ResultCode);
public readonly record struct ProjectionResult(int Version, bool RequiresRefresh);
public readonly record struct ProjectionLagSample(long ElapsedMilliseconds, string ReplicaName);
public readonly record struct WorkflowCheckpoint(string WorkflowId, long SequenceNumber, DateTimeOffset TimestampUtc);

public struct ProjectionCursor
{
    public long Offset { get; init; }
    public string? ContinuationToken { get; init; }
    public string? ShardKey { get; init; }
}

public sealed class WorkflowConfiguration
{
    public required string Name { get; init; }
    public required string QueueName { get; init; }
    public required string ProjectionName { get; init; }
    public bool EnableDiagnostics { get; init; }
    public bool EnableWarmup { get; init; }
    public int MaxInFlightRequests { get; init; }
}

public sealed class WorkflowRuntimeState
{
    public required string WorkflowId { get; init; }
    public required string CurrentStage { get; init; }
    public required string CurrentOwner { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset LastCheckpointAtUtc { get; init; }
}

public sealed class HttpClientDiagnosticSample
{
    public required string Route { get; init; }
    public required string Method { get; init; }
    public required string CorrelationId { get; init; }
    public int StatusCode { get; init; }
    public long ElapsedMilliseconds { get; init; }
}

public record SnapshotEnvelope(string SnapshotId, string SnapshotKind);
public record RetryDecision(bool ShouldRetry, string Reason);
public record RegistrationDescriptor(string Service, string Implementation, string Lifetime);
public record ReplicaDescriptor(string ReplicaName, string Region, string Environment);
public record ProjectionRefreshCommand(string ProjectionId, string Reason, DateTimeOffset RequestedAtUtc);

