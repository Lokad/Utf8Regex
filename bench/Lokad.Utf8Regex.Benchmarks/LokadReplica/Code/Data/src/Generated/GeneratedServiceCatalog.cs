namespace LokadReplica\\Code.Generated;

public static partial class GeneratedServiceCatalog
{
    public const string ProjectionRefreshBinding = "projection-refresh-binding";
    public const string ProjectionSnapshotReader = "projection-snapshot-reader";
    public const string ProjectionLagMonitor = "projection-lag-monitor";
    public const string WorkflowCheckpointBinding = "workflow-checkpoint-binding";
    public const string HttpClientRegistration = "httpclient-registration";
    public const string OrderWorkflowService = "order-workflow-service";
    public const string OrderProjectionCoordinator = "order-projection-coordinator";
    public const string OrderProjectionWarmup = "order-projection-warmup";
    public const string CustomerProjectionReader = "customer-projection-reader";
    public const string InventoryProjectionReader = "inventory-projection-reader";
    public const string RetryPolicyFactory = "retry-policy-factory";
    public const string DiagnosticScopeEmitter = "diagnostic-scope-emitter";
    public const string QueryCacheCoordinator = "query-cache-coordinator";
    public const string ReplicaHealthReporter = "replica-health-reporter";
    public const string ReplicaDiffReporter = "replica-diff-reporter";
    public const string CheckpointReplayService = "checkpoint-replay-service";
    public const string ProjectionHistoryReader = "projection-history-reader";

    public static readonly string[] All =
    {
        ProjectionRefreshBinding,
        ProjectionSnapshotReader,
        ProjectionLagMonitor,
        WorkflowCheckpointBinding,
        HttpClientRegistration,
        OrderWorkflowService,
        OrderProjectionCoordinator,
        OrderProjectionWarmup,
        CustomerProjectionReader,
        InventoryProjectionReader,
        RetryPolicyFactory,
        DiagnosticScopeEmitter,
        QueryCacheCoordinator,
        ReplicaHealthReporter,
        ReplicaDiffReporter,
        CheckpointReplayService,
        ProjectionHistoryReader,
    };

    public static bool Contains(string name)
    {
        foreach (var candidate in All)
        {
            if (candidate == name)
            {
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<string> Describe()
    {
        yield return $"{ProjectionRefreshBinding}: POST /api/orders/projection";
        yield return $"{ProjectionSnapshotReader}: GET /api/orders/projection/details";
        yield return $"{ProjectionLagMonitor}: GET /api/orders/projection/lag";
        yield return $"{WorkflowCheckpointBinding}: POST /api/orders/checkpoints/replay";
        yield return $"{HttpClientRegistration}: services.AddHttpClient<IOrderWorkflowService, OrderWorkflowService>()";
        yield return $"{ReplicaHealthReporter}: GET /api/replica/health";
        yield return $"{ProjectionHistoryReader}: GET /api/orders/projection/history";
    }
}

