namespace LokadReplica\\Code.Async;

public static class AsyncPipeline
{
    public static async Task<IReadOnlyList<DispatchEnvelope>> ExecuteAsync(
        IWorkflowRunner workflowRunner,
        CancellationToken cancellationToken)
    {
        await using var scope = workflowRunner.CreateAsyncScope();
        var orders = await workflowRunner.FetchOrdersAsync(cancellationToken).ConfigureAwait(false);
        var envelopes = new List<DispatchEnvelope>(orders.Count);

        foreach (var order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            envelopes.Add(await workflowRunner.BuildEnvelopeAsync(order, cancellationToken).ConfigureAwait(false));
        }

        return envelopes;
    }

    public static async Task<ResultRecord> ExecuteStageAsync(
        IWorkflowRunner workflowRunner,
        CancellationToken cancellationToken)
    {
        var envelopes = await ExecuteAsync(workflowRunner, cancellationToken).ConfigureAwait(false);
        return new ResultRecord(envelopes.Count, completed: true);
    }

    public static async IAsyncEnumerable<DispatchEnvelope> StreamAsync(
        IWorkflowRunner workflowRunner,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var scope = workflowRunner.CreateAsyncScope();
        await foreach (var order in workflowRunner.StreamOrdersAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await workflowRunner.BuildEnvelopeAsync(order, cancellationToken).ConfigureAwait(false);
        }
    }

    public static ValueTask<int> GetValueAsync(IProjectionReader projectionReader, CancellationToken cancellationToken)
    {
        return projectionReader.GetPendingProjectionCountAsync(cancellationToken);
    }
}

public interface IWorkflowRunner
{
    IAsyncDisposable CreateAsyncScope();
    Task<IReadOnlyList<CustomerProfile>> FetchOrdersAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<CustomerProfile> StreamOrdersAsync(CancellationToken cancellationToken);
    Task<DispatchEnvelope> BuildEnvelopeAsync(CustomerProfile order, CancellationToken cancellationToken);
}

public interface IProjectionReader
{
    ValueTask<int> GetPendingProjectionCountAsync(CancellationToken cancellationToken);
}

public readonly record struct ResultRecord(int Count, bool Completed);

