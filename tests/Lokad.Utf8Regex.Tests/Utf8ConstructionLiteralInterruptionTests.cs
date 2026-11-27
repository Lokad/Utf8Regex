using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionLiteralInterruptionTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void LiteralPassesObserveOriginalInterruptionAndReleaseOnlyTheirCapacity(bool encoding, bool timeout)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var input = new string('a', 2047) + "😀" + new string('é', 3000);
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var held = owner.Budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        owner.Budget.Deadline = deadline;
        owner.Budget.CancellationToken = cancellation.Token;
        var reads = 0;
        owner.Budget.CheckpointHook = () =>
        {
            if (++reads != (encoding ? 6 : 3)) return;
            if (timeout) ticks = 25; else cancellation.Cancel();
        };

        var failure = Record.Exception(() => owner.Budget.EncodeUtf8(input));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(held, owner.Budget.ReservedBytes);
        if (encoding) Assert.True(owner.Budget.PeakBytes > held);
        else Assert.Equal(held, owner.Budget.PeakBytes); // Refuse before byte acquisition.

        owner.Budget.CheckpointHook = null;
        owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
        owner.Budget.CancellationToken = default;
        var spent = owner.Budget.DiagnosticCheckpoints;
        var recovered = owner.Budget.EncodeUtf8(input);
        Assert.Equal(input, Encoding.UTF8.GetString(recovered));
        Assert.True(owner.Budget.DiagnosticCheckpoints > spent);
        owner.Budget.ReleaseArray(recovered, false);
        Assert.Equal(held, owner.Budget.ReservedBytes);
        owner.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData(0)] [InlineData(2046)] [InlineData(2047)]
    [InlineData(2048)] [InlineData(4095)]
    public void ChunkBoundariesPreserveEncodedScalarsAndCapacity(int prefixLength)
    {
        var input = new string('a', prefixLength) + "😀é" + new string('b', 2050);
        using var owner = Utf8ConstructionScope.Enter();
        var encoded = owner.Budget.EncodeUtf8(input);
        Assert.Equal(Encoding.UTF8.GetBytes(input), encoded);
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(encoded.Length), owner.Budget.ReservedBytes);
    }

    [Fact]
    public void IncompleteScalarAtChunkBoundaryDoesNotAcquireBytes()
    {
        using var owner = Utf8ConstructionScope.Enter();
        Assert.Throws<ArgumentException>(() => owner.Budget.EncodeUtf8(new string('a', 2047) + "\uD83Dx"));
        Assert.Equal(0UL, owner.Budget.PeakBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(2048)]
    public void ShortLiteralsKeepTheirOriginalThreeCheckpoints(int length)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        owner.Budget.CheckpointHook = () => checkpoints++;
        _ = owner.Budget.EncodeUtf8(new string('a', length));
        Assert.Equal(3, checkpoints);
    }

    [Fact]
    public void PublicConstructionKeepsTheCompleteLiteral()
    {
        var pattern = new string('a', 2047) + "😀" + new string('é', 3000);
        var regex = Utf8Regex.Compile(pattern, options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            ConstructionPolicy = Utf8ConstructionPolicy.Default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None);
        var bytes = Encoding.UTF8.GetBytes(pattern);
        Assert.Equal(1, regex.Count(bytes));
        Assert.Equal(2, regex.Count(Encoding.UTF8.GetBytes(pattern + "!" + pattern)));
    }
}
