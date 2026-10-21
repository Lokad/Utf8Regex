using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2LexicalAdmissionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void LexicalGroupsCannotAllocatePastTheParentByteAllowance(int entry)
    {
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 49, ulong.MaxValue, 1);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 37;
        var pattern = new string('(', 100_000) + "a" + new string(')', 100_000);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Validate(entry, pattern));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(37, parent[0]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicCompilationBoundsLexicalDepthBeforeSemanticParsing(bool bytes)
    {
        var pattern = new string('(', 100_000) + "a" + new string(')', 100_000);
        var policy = new Utf8ConstructionPolicy { MaxDepth = 1 };
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            _ = bytes
                ? Utf8Pcre2Regex.Compile(System.Text.Encoding.UTF8.GetBytes(pattern), default, default,
                    Timeout.InfiniteTimeSpan, default, policy, Timeout.InfiniteTimeSpan, default)
                : Utf8Pcre2Regex.Compile(pattern, default, default,
                    Timeout.InfiniteTimeSpan, default, policy, Timeout.InfiniteTimeSpan, default);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(2ul, failure.Requested);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void LongPlainScansObserveOriginalAuthorityAfterEntry(int entry, bool timeout)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
        owner.Budget.Deadline = deadline;
        owner.Budget.CancellationToken = cancellation.Token;
        var checkpoints = 0;
        owner.Budget.CheckpointHook = _ =>
        {
            if (++checkpoints != 4) return;
            if (timeout) ticks = 30000; else cancellation.Cancel();
        };
        var failure = Record.Exception(() => Validate(entry, new string('a', 200_001)));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(4, checkpoints);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void FundedLexicalScopesPreserveTheEnclosingOwner()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 37;
        var pattern = string.Concat(Enumerable.Repeat("(?x:", 32)) + "a" + new string(')', 32);
        Validate(2, pattern);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(37, parent[0]);
        Assert.True(new Utf8Pcre2Regex(pattern).IsMatch("a"u8));
    }

    private static void Validate(int entry, string pattern)
    {
        switch (entry)
        {
            case 0: _ = Pcre2PatternCodeScan.ContainsCodeText(pattern, "(?C", default, default); break;
            case 1: Pcre2CaptureNameValidator.Validate(pattern, default, default); break;
            default: Pcre2CompileValidator.ValidatePattern(pattern, default, default); break;
        }
    }
}
