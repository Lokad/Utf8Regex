using Microsoft.Extensions.Logging;

namespace LokadReplica\\Code.Logging;

public static class OrderLogging
{
    public static void LogDispatch(ILogger logger, string orderId, string tenantId)
    {
        using var _ = logger.BeginScope("dispatch:{OrderId}:{TenantId}", orderId, tenantId);

        logger.LogTrace("Dispatch trace {OrderId} {TenantId}", orderId, tenantId);
        logger.LogDebug("Dispatch debug {OrderId} {TenantId}", orderId, tenantId);
        logger.LogInformation("Dispatch info {OrderId} {TenantId}", orderId, tenantId);
        logger.LogWarning("Dispatch warning {OrderId} {TenantId}", orderId, tenantId);
        logger.LogError("Dispatch error {OrderId} {TenantId}", orderId, tenantId);
    }

    public static void LogProjection(ILogger logger, string projectionId, string correlationId)
    {
        using var _ = logger.BeginScope("projection:{ProjectionId}:{CorrelationId}", projectionId, correlationId);

        logger.LogTrace("Projection trace {ProjectionId} {CorrelationId}", projectionId, correlationId);
        logger.LogDebug("Projection debug {ProjectionId} {CorrelationId}", projectionId, correlationId);
        logger.LogInformation("Projection info {ProjectionId} {CorrelationId}", projectionId, correlationId);
        logger.LogWarning("Projection warning {ProjectionId} {CorrelationId}", projectionId, correlationId);
        logger.LogError("Projection error {ProjectionId} {CorrelationId}", projectionId, correlationId);
    }

    public static void LogRefresh(ILogger logger, string refreshId, TimeSpan elapsed)
    {
        using var _ = logger.BeginScope("refresh:{RefreshId}", refreshId);

        logger.LogTrace("Refresh trace {RefreshId} {Elapsed}", refreshId, elapsed);
        logger.LogDebug("Refresh debug {RefreshId} {Elapsed}", refreshId, elapsed);
        logger.LogInformation("Refresh info {RefreshId} {Elapsed}", refreshId, elapsed);
        logger.LogWarning("Refresh warning {RefreshId} {Elapsed}", refreshId, elapsed);
        logger.LogError("Refresh error {RefreshId} {Elapsed}", refreshId, elapsed);
    }

    public static void LogHttpClientResponse(ILogger logger, string route, int statusCode, long elapsedMilliseconds)
    {
        logger.LogDebug(
            "HttpClient SendAsync completed for {Route} with status {StatusCode} in {ElapsedMilliseconds}ms",
            route,
            statusCode,
            elapsedMilliseconds);

        if (statusCode >= 500)
        {
            logger.LogError(
                "HttpClient SendAsync failed for {Route} with status {StatusCode} after {ElapsedMilliseconds}ms",
                route,
                statusCode,
                elapsedMilliseconds);
        }
        else if (elapsedMilliseconds > 2000)
        {
            logger.LogWarning(
                "HttpClient SendAsync was slow for {Route} with status {StatusCode} after {ElapsedMilliseconds}ms",
                route,
                statusCode,
                elapsedMilliseconds);
        }
        else
        {
            logger.LogInformation(
                "HttpClient SendAsync succeeded for {Route} with status {StatusCode} after {ElapsedMilliseconds}ms",
                route,
                statusCode,
                elapsedMilliseconds);
        }
    }
}

