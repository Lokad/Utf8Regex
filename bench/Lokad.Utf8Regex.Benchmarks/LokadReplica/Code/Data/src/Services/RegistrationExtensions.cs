using Microsoft.Extensions.DependencyInjection;

namespace LokadReplica\\Code.Services;

public static class RegistrationExtensions
{
    public static IServiceCollection AddOrderModule(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEnvelopeFactory, EnvelopeFactory>();
        services.AddScoped<IOrderReader, SqlOrderReader>();
        services.AddScoped<IOrderWriter, SqlOrderWriter>();
        services.AddScoped<IProjectionReader, ProjectionReader>();
        services.AddTransient<IEventPublisher, BusEventPublisher>();
        services.AddTransient<IWorkflowRunner, WorkflowRunner>();
        services.AddHttpClient<IInventoryClient, InventoryHttpClient>();
        return services;
    }

    public static IServiceCollection AddDiagnosticModule(this IServiceCollection services)
    {
        services.AddSingleton<IDiagnosticClock, DiagnosticClock>();
        services.AddScoped<IDiagnosticRepository, DiagnosticRepository>();
        services.AddTransient<IDiagnosticEmitter, DiagnosticEmitter>();
        services.AddTransient<IHealthReportFormatter, JsonHealthReportFormatter>();
        return services;
    }
}

public interface IClock;
public interface IEnvelopeFactory;
public interface IOrderReader;
public interface IOrderWriter;
public interface IProjectionReader;
public interface IEventPublisher;
public interface IWorkflowRunner;
public interface IInventoryClient;
public interface IDiagnosticClock;
public interface IDiagnosticRepository;
public interface IDiagnosticEmitter;
public interface IHealthReportFormatter;
public sealed class SystemClock : IClock;
public sealed class EnvelopeFactory : IEnvelopeFactory;
public sealed class SqlOrderReader : IOrderReader;
public sealed class SqlOrderWriter : IOrderWriter;
public sealed class ProjectionReader : IProjectionReader;
public sealed class BusEventPublisher : IEventPublisher;
public sealed class WorkflowRunner : IWorkflowRunner;
public sealed class InventoryHttpClient : IInventoryClient;
public sealed class DiagnosticClock : IDiagnosticClock;
public sealed class DiagnosticRepository : IDiagnosticRepository;
public sealed class DiagnosticEmitter : IDiagnosticEmitter;
public sealed class JsonHealthReportFormatter : IHealthReportFormatter;

