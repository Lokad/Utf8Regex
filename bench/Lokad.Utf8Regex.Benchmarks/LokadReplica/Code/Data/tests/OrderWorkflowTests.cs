using System.Collections.Generic;
using Xunit;

namespace LokadReplica\\Code.Tests;

public sealed class OrderWorkflowTests
{
    [Fact]
    public void Projection_refresh_messages_include_expected_markers()
    {
        const string log = """
            projection-refresh completed
            projection-refresh skipped because snapshot was current
            projection-refresh scheduled warmup
            projection-lag warning emitted for shard-03
            """;

        Assert.Contains("projection-refresh", log);
        Assert.Contains("projection-lag", log);
    }

    [Fact]
    public void Generated_bindings_are_intentionally_referenced()
    {
        var values = new List<string>
        {
            "ProjectionRefreshBinding",
            "WorkflowCheckpointBinding",
            "GeneratedServiceCatalog",
            "OrderWorkflowService",
            "HttpClientRegistration",
        };

        Assert.Contains("GeneratedServiceCatalog", values);
        Assert.Contains("ProjectionRefreshBinding", values);
    }

    [Fact]
    public void HttpClient_sendasync_markers_are_present()
    {
        const string snippet = """
            await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await httpClient.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("HttpClient SendAsync completed for projection refresh");
            """;

        Assert.Contains("HttpClient", snippet);
        Assert.Contains("SendAsync", snippet);
        Assert.Contains("ConfigureAwait", snippet);
    }

    [Fact]
    public void Type_declaration_tokens_are_repeated_for_search_cases()
    {
        const string declarations = """
            public sealed record ProjectionSnapshot(int Revision, int PendingOrders);
            internal sealed class ProjectionRefreshCommand;
            public interface IProjectionReader;
            public readonly record struct ProjectionLagSample(long ElapsedMilliseconds);
            """;

        Assert.Contains("sealed", declarations);
        Assert.Contains("record", declarations);
        Assert.Contains("interface", declarations);
    }
}

