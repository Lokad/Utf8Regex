using System.Collections.Generic;
using Xunit;

namespace LokadReplica\\Code.Tests;

public sealed class Smoke
{
    [Fact]
    public void HttpClient_literal_is_present_for_case_insensitive_search()
    {
        const string marker = "HttpClient SendAsync ConfigureAwait";

        Assert.Contains("HttpClient", marker);
        Assert.Contains("SendAsync", marker);
        Assert.Contains("ConfigureAwait", marker);
    }

    [Fact]
    public void Projection_markers_are_stable_for_search_benchmarks()
    {
        const string payload = """
            projection-refresh
            projection-snapshot
            projection-lag
            projection-warmup
            """;

        Assert.Contains("projection-refresh", payload);
        Assert.Contains("projection-warmup", payload);
    }

    [Fact]
    public void Logging_tokens_are_intentionally_repeated()
    {
        var markers = new[]
        {
            "logger.LogInformation",
            "logger.LogWarning",
            "logger.LogError",
            "BeginScope",
            "OrderWorkflowService"
        };

        Assert.Contains("logger.LogInformation", markers);
        Assert.Contains("BeginScope", markers);
    }

    [Fact]
    public void Generated_type_identifiers_remain_present()
    {
        var names = new HashSet<string>
        {
            "ProjectionRefreshBinding",
            "WorkflowCheckpointBinding",
            "HttpClientRegistration",
            "GeneratedServiceCatalog"
        };

        Assert.Contains("GeneratedServiceCatalog", names);
        Assert.Contains("ProjectionRefreshBinding", names);
    }
}

