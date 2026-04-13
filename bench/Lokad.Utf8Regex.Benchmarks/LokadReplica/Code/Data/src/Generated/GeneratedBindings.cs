namespace LokadReplica\\Code.Generated;

public static class GeneratedBindings
{
    public static string BindCustomerIdentifier(string value) => $"binding:customer-id:{value}";
    public static string BindProjectionIdentifier(string value) => $"binding:projection-id:{value}";
    public static string BindDispatchEnvelope(string value) => $"binding:dispatch-envelope:{value}";
    public static string BindWorkflowConfiguration(string value) => $"binding:workflow-config:{value}";
    public static string BindRegistrationDescriptor(string value) => $"binding:registration:{value}";
    public static string BindProjectionWarmupRequest(string value) => $"binding:projection-warmup:{value}";
    public static string BindCheckpointCursor(string value) => $"binding:checkpoint-cursor:{value}";
    public static string BindHttpClientDiagnostic(string value) => $"binding:httpclient-diagnostic:{value}";

    public static IReadOnlyList<string> BuildBindingCatalog()
    {
        return
        [
            BindCustomerIdentifier("cust-001"),
            BindCustomerIdentifier("cust-002"),
            BindProjectionIdentifier("proj-101"),
            BindProjectionIdentifier("proj-102"),
            BindDispatchEnvelope("dispatch-order-refresh"),
            BindDispatchEnvelope("dispatch-order-retry"),
            BindWorkflowConfiguration("checkout-workflow"),
            BindWorkflowConfiguration("projection-refresh"),
            BindRegistrationDescriptor("AddSingleton<IClock, SystemClock>"),
            BindRegistrationDescriptor("AddScoped<IOrderReader, SqlOrderReader>"),
            BindRegistrationDescriptor("AddTransient<IWorkflowRunner, WorkflowRunner>"),
            BindProjectionWarmupRequest("warmup-shard-01"),
            BindProjectionWarmupRequest("warmup-shard-02"),
            BindCheckpointCursor("cursor:00001234"),
            BindCheckpointCursor("cursor:00005678"),
            BindHttpClientDiagnostic("GET /api/orders/current"),
            BindHttpClientDiagnostic("POST /api/orders/projection"),
        ];
    }

    public static IReadOnlyDictionary<string, string> BuildServiceBindings()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["IOrderWorkflowService"] = BindRegistrationDescriptor("OrderWorkflowService"),
            ["IProjectionRefreshService"] = BindRegistrationDescriptor("ProjectionRefreshService"),
            ["IProjectionSnapshotReader"] = BindProjectionIdentifier("projection-snapshot-reader"),
            ["IHttpClientDiagnostics"] = BindHttpClientDiagnostic("diagnostics-httpclient"),
            ["IProjectionWarmupService"] = BindProjectionWarmupRequest("projection-warmup"),
        };
    }

    public static string RenderBindingDiagnostic(string prefix, string value)
    {
        return $"{prefix}:{value}:binding-generated";
    }

    public static string RenderBindingDump()
    {
        var lines = new List<string>();
        foreach (var entry in BuildBindingCatalog())
        {
            lines.Add(RenderBindingDiagnostic("generated", entry));
        }

        foreach (var pair in BuildServiceBindings())
        {
            lines.Add($"{pair.Key} => {pair.Value}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

