using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace LokadReplica\\Code.Services.Projection;

public sealed class ProjectionRefreshCoordinator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProjectionRefreshCoordinator> _logger;
    private readonly IProjectionSnapshotStore _snapshotStore;
    private readonly IProjectionCommandWriter _commandWriter;

    public ProjectionRefreshCoordinator(
        HttpClient httpClient,
        ILogger<ProjectionRefreshCoordinator> logger,
        IProjectionSnapshotStore snapshotStore,
        IProjectionCommandWriter commandWriter)
    {
        _httpClient = httpClient;
        _logger = logger;
        _snapshotStore = snapshotStore;
        _commandWriter = commandWriter;
    }

    public async Task<ProjectionRefreshResult> RefreshAsync(
        ProjectionRefreshRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting projection refresh for {ProjectionId} in tenant {TenantId}",
            request.ProjectionId,
            request.TenantId);

        var currentSnapshot = await _snapshotStore
            .GetSnapshotAsync(request.ProjectionId, cancellationToken)
            .ConfigureAwait(false);

        if (currentSnapshot is not null && !currentSnapshot.RequiresRefresh)
        {
            _logger.LogDebug(
                "Skipping refresh for {ProjectionId} because the current snapshot is still valid",
                request.ProjectionId);

            return new ProjectionRefreshResult(
                request.ProjectionId,
                refreshed: false,
                statusCode: 304,
                message: "snapshot-current");
        }

        await using var requestScope = await _commandWriter
            .CreateAsyncScope(cancellationToken)
            .ConfigureAwait(false);

        using var httpRequest = BuildRefreshRequest(request);
        using var response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Projection refresh call completed for {ProjectionId} with status {StatusCode}",
            request.ProjectionId,
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Projection refresh failed for {ProjectionId} with status {StatusCode}",
                request.ProjectionId,
                (int)response.StatusCode);
        }

        var command = new ProjectionRefreshCommandRecord(
            request.ProjectionId,
            request.TenantId,
            request.Reason,
            DateTimeOffset.UtcNow);

        await _commandWriter
            .AppendAsync(command, cancellationToken)
            .ConfigureAwait(false);

        await _snapshotStore
            .MarkSnapshotRefreshAsync(request.ProjectionId, cancellationToken)
            .ConfigureAwait(false);

        return new ProjectionRefreshResult(
            request.ProjectionId,
            refreshed: response.IsSuccessStatusCode,
            statusCode: (int)response.StatusCode,
            message: response.IsSuccessStatusCode ? "refreshed" : "failed");
    }

    public async ValueTask<bool> WarmProjectionAsync(
        ProjectionRefreshRequest request,
        CancellationToken cancellationToken)
    {
        await using var requestScope = await _commandWriter
            .CreateAsyncScope(cancellationToken)
            .ConfigureAwait(false);

        using var warmupRequest = BuildWarmupRequest(request);
        using var response = await _httpClient
            .SendAsync(warmupRequest, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Projection warmup completed for {ProjectionId} with status {StatusCode}",
            request.ProjectionId,
            (int)response.StatusCode);

        return response.IsSuccessStatusCode;
    }

    public async IAsyncEnumerable<ProjectionRefreshResult> RefreshBatchAsync(
        IReadOnlyList<ProjectionRefreshRequest> requests,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await RefreshAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ProjectionRefreshResult>> ReplayWarmRequestsAsync(
        IReadOnlyList<ProjectionRefreshRequest> requests,
        CancellationToken cancellationToken)
    {
        var results = new List<ProjectionRefreshResult>(requests.Count);
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var warmed = await WarmProjectionAsync(request, cancellationToken).ConfigureAwait(false);
            if (!warmed)
            {
                _logger.LogWarning("Warmup failed for {ProjectionId}", request.ProjectionId);
            }

            results.Add(await RefreshAsync(request, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private static HttpRequestMessage BuildRefreshRequest(ProjectionRefreshRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/projections/{request.ProjectionId}/refresh");
        message.Headers.Add("x-tenant-id", request.TenantId);
        message.Headers.Add("x-refresh-reason", request.Reason);
        message.Headers.Add("x-refresh-priority", request.Priority);
        return message;
    }

    private static HttpRequestMessage BuildWarmupRequest(ProjectionRefreshRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/projections/{request.ProjectionId}/warmup");
        message.Headers.Add("x-tenant-id", request.TenantId);
        message.Headers.Add("x-refresh-reason", request.Reason);
        return message;
    }
}

public interface IProjectionSnapshotStore
{
    Task<ProjectionSnapshotRecord?> GetSnapshotAsync(string projectionId, CancellationToken cancellationToken);
    Task MarkSnapshotRefreshAsync(string projectionId, CancellationToken cancellationToken);
}

public interface IProjectionCommandWriter
{
    ValueTask<IAsyncDisposable> CreateAsyncScope(CancellationToken cancellationToken);
    Task AppendAsync(ProjectionRefreshCommandRecord command, CancellationToken cancellationToken);
}

public sealed record ProjectionRefreshRequest(
    string ProjectionId,
    string TenantId,
    string Reason,
    string Priority);

public sealed record ProjectionRefreshResult(
    string ProjectionId,
    bool Refreshed,
    int StatusCode,
    string Message);

public sealed record ProjectionSnapshotRecord(
    string ProjectionId,
    int Version,
    bool RequiresRefresh,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProjectionRefreshCommandRecord(
    string ProjectionId,
    string TenantId,
    string Reason,
    DateTimeOffset RequestedAtUtc);
