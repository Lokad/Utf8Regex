using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2OptimizerAdmissionTests
{
    [Fact]
    public void MajorFiniteLanguageExpansionAdmitsBeforeAllocation()
    {
        var (request, parsed) = Parse();
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 49, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 37;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Optimize(request, parsed));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        // Reject the major finite-language buffer before uncontrolled growth.
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - allocated, 0, 64 * 1024);
        Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(37, parent[0]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExpansionRetainsOriginalCancellationAndDeadline(bool timeout)
    {
        var (request, parsed) = Parse();
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 37;
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
        var failure = Record.Exception(() => Optimize(request, parsed));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(4, checkpoints);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(37, parent[0]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExpansionSharesWorkAndDepthAdmission(bool depth)
    {
        var (request, parsed) = Parse();
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue,
            depth ? ulong.MaxValue : 1, depth ? 1ul : ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Optimize(request, parsed));
        Assert.Equal(depth ? Utf8ResourceLimitKind.ConstructionDepth : Utf8ResourceLimitKind.ConstructionWorkSteps,
            failure.ResourceKind);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.WorkSteps > 0);
    }

    [Theory]
    [InlineData("(?<left>a)(b)", "ab ab", "ab", "a")]
    [InlineData(@"(?<left>a)\k<left>", "aa aa", "aa", "a")]
    [InlineData(@"(\Q😀#[] a\E)(b)", "😀#[] ab 😀#[] ab", "😀#[] ab", "😀#[] a")]
    public void FundedTextAndByteFactoriesKeepCaptureAndFiniteControlBehavior(string pattern, string subject,
        string match, string capture)
    {
        foreach (var bytes in new[] { false, true })
        {
            var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 256,
                MaxWorkingBytes = 16 * 1024 * 1024, MaxResultBytes = 16 * 1024 * 1024 };
            var construction = new Utf8ConstructionPolicy { MaxBytes = 16 * 1024 * 1024 };
            var regex = bytes
                ? Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(pattern), default, default,
                    Timeout.InfiniteTimeSpan, policy, construction, Timeout.InfiniteTimeSpan, default)
                : Utf8Pcre2Regex.Compile(pattern, default, default, Timeout.InfiniteTimeSpan,
                    policy, construction, Timeout.InfiniteTimeSpan, default);
            var input = Encoding.UTF8.GetBytes(subject);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(2, regex.Count(input));
            var detailed = regex.MatchDetailed(input);
            Assert.Equal(match, detailed.Value.GetValueString());
            Assert.Equal(capture, detailed.GetGroup(1).GetValueString());
        }
    }

    private static (Pcre2CompileRequest Request, Pcre2CompiledBacktrackingOutcome Parsed) Parse()
    {
        var pattern = string.Concat(Enumerable.Repeat("(a)", 1024));
        var request = new Pcre2CompileRequest(pattern, default, default, default, Timeout.InfiniteTimeSpan);
        return (request, Pcre2BacktrackingCompiler.Compile(request)!);
    }

    private static void Optimize(Pcre2CompileRequest request, Pcre2CompiledBacktrackingOutcome parsed)
    {
        _ = Pcre2FiniteLiteralLanguageAnalyzer.TryCompile(parsed.Root, request);
    }
}
