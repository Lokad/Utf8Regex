using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LokadReplica\\Code.Diagnostics;

public sealed class HttpProjectionDiagnostics
{
    private readonly ILogger<HttpProjectionDiagnostics> _logger;

    public HttpProjectionDiagnostics(ILogger<HttpProjectionDiagnostics> logger)
    {
        _logger = logger;
    }

    public void LogSnapshotReplay(
        string projectionId,
        string route,
        int statusCode,
        long elapsedMilliseconds)
    {
        _logger.LogInformation(
            "Projection snapshot replay for {ProjectionId} used route {Route} and returned {StatusCode} in {ElapsedMilliseconds}ms",
            projectionId,
            route,
            statusCode,
            elapsedMilliseconds);

        if (statusCode >= 500)
        {
            _logger.LogError(
                "Projection snapshot replay failed for {ProjectionId} on route {Route} with status {StatusCode}",
                projectionId,
                route,
                statusCode);
        }
        else if (elapsedMilliseconds > 1500)
        {
            _logger.LogWarning(
                "Projection snapshot replay was slow for {ProjectionId} on route {Route}",
                projectionId,
                route);
        }
    }

    public void LogProjectionDispatch(
        ILogger logger,
        string route,
        string correlationId,
        int statusCode,
        long elapsedMilliseconds)
    {
        logger.LogDebug(
            "HttpClient SendAsync dispatched for {Route} with correlation {CorrelationId}",
            route,
            correlationId);

        logger.LogInformation(
            "HttpClient SendAsync completed for {Route} with status {StatusCode} in {ElapsedMilliseconds}ms",
            route,
            statusCode,
            elapsedMilliseconds);

        if (statusCode >= 500)
        {
            logger.LogError(
                "HttpClient SendAsync failed for {Route} with status {StatusCode}",
                route,
                statusCode);
        }
        else if (elapsedMilliseconds > 2500)
        {
            logger.LogWarning(
                "HttpClient SendAsync was slow for {Route} with status {StatusCode}",
                route,
                statusCode);
        }
    }
}

public static class ProjectionModuleRegistration
{
    public static IServiceCollection AddProjectionDiagnostics(this IServiceCollection services)
    {
        services.AddSingleton<HttpProjectionDiagnostics>();
        services.AddSingleton<IProjectionMetricsSink, ProjectionMetricsSink>();
        services.AddScoped<IProjectionAuditWriter, ProjectionAuditWriter>();
        services.AddScoped<IProjectionEnvelopeReader, ProjectionEnvelopeReader>();
        services.AddTransient<IProjectionReplayPlanner, ProjectionReplayPlanner>();
        services.AddTransient<IProjectionLagSampler, ProjectionLagSampler>();
        return services;
    }

    public static IServiceCollection AddProjectionRefreshServices(this IServiceCollection services)
    {
        services.AddSingleton<IProjectionSnapshotReader, ProjectionSnapshotReader>();
        services.AddSingleton<IProjectionCursorReader, ProjectionCursorReader>();
        services.AddScoped<IProjectionRefreshWriter, ProjectionRefreshWriter>();
        services.AddScoped<IProjectionSnapshotWriter, ProjectionSnapshotWriter>();
        services.AddTransient<IProjectionRefreshPlanner, ProjectionRefreshPlanner>();
        services.AddTransient<IProjectionCommandFormatter, ProjectionCommandFormatter>();
        return services;
    }
}

public sealed class ProjectionSnapshotReader : IProjectionSnapshotReader;
public sealed class ProjectionCursorReader : IProjectionCursorReader;
public sealed class ProjectionRefreshWriter : IProjectionRefreshWriter;
public sealed class ProjectionSnapshotWriter : IProjectionSnapshotWriter;
public sealed class ProjectionRefreshPlanner : IProjectionRefreshPlanner;
public sealed class ProjectionCommandFormatter : IProjectionCommandFormatter;
public sealed class ProjectionMetricsSink : IProjectionMetricsSink;
public sealed class ProjectionAuditWriter : IProjectionAuditWriter;
public sealed class ProjectionEnvelopeReader : IProjectionEnvelopeReader;
public sealed class ProjectionReplayPlanner : IProjectionReplayPlanner;
public sealed class ProjectionLagSampler : IProjectionLagSampler;

public interface IProjectionSnapshotReader;
public interface IProjectionCursorReader;
public interface IProjectionRefreshWriter;
public interface IProjectionSnapshotWriter;
public interface IProjectionRefreshPlanner;
public interface IProjectionCommandFormatter;
public interface IProjectionMetricsSink;
public interface IProjectionAuditWriter;
public interface IProjectionEnvelopeReader;
public interface IProjectionReplayPlanner;
public interface IProjectionLagSampler;
