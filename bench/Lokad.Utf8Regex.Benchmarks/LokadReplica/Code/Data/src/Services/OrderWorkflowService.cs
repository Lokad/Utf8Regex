using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace LokadReplica\\Code.Services.Orders;

public sealed class OrderWorkflowService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderWorkflowService> _logger;

    public OrderWorkflowService(HttpClient httpClient, ILogger<OrderWorkflowService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OrderEnvelope> RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting order workflow");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders/current");
        request.Headers.Add("x-workflow", "checkout");
        request.Headers.Add("x-projection-mode", "snapshot");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Completed order workflow");
        return new OrderEnvelope("current", response.IsSuccessStatusCode, "/api/orders/current");
    }

    public async Task<OrderEnvelope> RunProjectionAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Projection refresh requested");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/projection");
        request.Headers.Add("x-projection-reason", "scheduled-refresh");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Projection refresh completed");
        return new OrderEnvelope("projection", response.IsSuccessStatusCode, "/api/orders/projection");
    }

    public async Task<OrderEnvelope> RunRetryAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrying order workflow");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders/retry");
        request.Headers.Add("x-retry-reason", "projection-lag");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogError("Retry branch completed with diagnostics");
        return new OrderEnvelope("retry", response.IsSuccessStatusCode, "/api/orders/retry");
    }

    public async Task<OrderEnvelope> WarmProjectionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Projection warmup requested");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/projection/warmup");
        request.Headers.Add("x-generated-binding", "projection-warmup");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Projection warmup completed");
        return new OrderEnvelope("warmup", response.IsSuccessStatusCode, "/api/orders/projection/warmup");
    }

    public async Task<IReadOnlyList<OrderEnvelope>> ReplayAsync(CancellationToken cancellationToken)
    {
        var results = new List<OrderEnvelope>(capacity: 3)
        {
            await RunAsync(cancellationToken).ConfigureAwait(false),
            await RunProjectionAsync(cancellationToken).ConfigureAwait(false),
            await RunRetryAsync(cancellationToken).ConfigureAwait(false),
        };

        results.Add(await WarmProjectionAsync(cancellationToken).ConfigureAwait(false));
        return results;
    }
}

public readonly record struct OrderEnvelope(string Identifier, bool Succeeded, string Route);

