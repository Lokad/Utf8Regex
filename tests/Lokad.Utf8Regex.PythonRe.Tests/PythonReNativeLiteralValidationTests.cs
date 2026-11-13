using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativeLiteralValidationTests
{
    [Theory]
    [InlineData("Шерлок", "xx Шерлок zz")]
    [InlineData("Шерлок", "Ватсон Ватсон")]
    [InlineData("Шерлок", "Шерлок")]
    [InlineData("é😀", "中é😀尾")]
    [InlineData("😀", "😀😀")]
    [InlineData("😀", "é中")]
    [InlineData("中", "é😀中")]
    [InlineData("é", "é")]
    [InlineData("é", "")]
    [InlineData("needle", "xneedle")]
    [InlineData("", "é😀")]
    public void ValueTextAndFullMatchKeepScalarEndpoints(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var start = text.IndexOf(pattern, StringComparison.Ordinal);
        foreach (var mode in new[] { 0, 1, 2, 3 })
        {
            var policy = mode == 1 ? new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000, MaxDepth = 128,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
            } : default;
            using var live = new CancellationTokenSource();
            var token = mode == 2 ? live.Token : default;
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None,
                mode == 3 ? TimeSpan.FromSeconds(10) : Timeout.InfiniteTimeSpan, policy);
            Check(regex.Search(input, 0, token), pattern, text, start);
            Assert.Equal(start >= 0 ? pattern : null, regex.SearchToString(input, 0, token));
            Check(regex.Match(input, 0, token), pattern, text, start == 0 ? 0 : -1);
            Assert.Equal(start == 0 ? pattern : null, regex.MatchToString(input, 0, token));
            var full = text == pattern;
            Check(regex.FullMatch(input, 0, token), pattern, text, full ? 0 : -1);
            Assert.Equal(full ? pattern : null, regex.FullMatchToString(input, 0, token));
        }
    }

    [Fact]
    public void BorrowedValueProjectionPreservesHitCoordinatesAndParentLoan()
    {
        var regex = new Utf8PythonRegex("é😀");
        var input = Encoding.UTF8.GetBytes("中é😀尾");
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var value = regex.MaterializeNativeMatchValue(input, 0, 0, ref invocation);
        Assert.True(value.Success);
        Assert.Equal((3, 9, 1, 4),
            (value.StartOffsetInBytes, value.EndOffsetInBytes, value.StartOffsetInUtf16, value.EndOffsetInUtf16));
        Assert.Equal("é😀", value.GetValueString());
        Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Fact]
    public void ControlledWorkDenialReleasesNoParentLoan()
    {
        var regex = new Utf8PythonRegex("Шерлок");
        var invocation = new Utf8SliceInvocation(new(1, 1, 7, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Exception? failure = null;
        try { _ = regex.MaterializeNativeMatchValue([0xff], 0, 0, ref invocation); }
        catch (Exception error) { failure = error; }
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, invocation.Budget.LimitBreach);
        Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Fact]
    public void OriginalTokenAndFreshCallsRemainIndependent()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var regex = new Utf8PythonRegex("Шерлок");
        for (var operation = 0; operation < 6; operation++)
        {
            var error = Assert.Throws<OperationCanceledException>(() => Run(regex, [0xff], operation, cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
        }
        Assert.Equal("Шерлок", regex.SearchToString("xx Шерлок"u8));
    }

    [Fact]
    public void BorrowedViewsAndDetailedSnapshotsSurviveLaterDiscovery()
    {
        var regex = new Utf8PythonRegex("é😀");
        var input = Encoding.UTF8.GetBytes("中é😀尾");
        var first = regex.Search(input);
        var snapshot = regex.SearchDetailedData(input);
        Assert.False(regex.Search("Ватсон"u8).Success);
        Assert.Equal(1, regex.Count(input));
        Assert.Equal((3, 9, 1, 4),
            (first.StartOffsetInBytes, first.EndOffsetInBytes, first.StartOffsetInUtf16, first.EndOffsetInUtf16));
        Assert.Equal("é😀", first.GetValueString());
        Assert.Equal("é😀", snapshot.Value.ValueText);
    }

    [Theory]
    [InlineData(4)] [InlineData(5)]
    public void ImmediateMissStillChecksTheOriginalDeadline(int operation)
    {
        var regex = new Utf8PythonRegex("Шерлок", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1),
            new() { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = "absent"u8.ToArray();
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads < 2 ? 0 : 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, input, operation, default));
        Assert.Equal(regex.Pattern, error.Pattern);
        Assert.Empty(error.Input);
        Assert.True(reads >= 2);
    }

    private static void Check(Utf8PythonValueMatch match, string pattern, string text, int start)
    {
        Assert.Equal(start >= 0, match.Success);
        if (start < 0) return;
        Assert.Equal((start, start + pattern.Length), (match.StartOffsetInUtf16, match.EndOffsetInUtf16));
        var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
        Assert.Equal((byteStart, byteStart + Encoding.UTF8.GetByteCount(pattern)),
            (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.Equal(pattern, match.GetValueString());
    }

    private static void Run(Utf8PythonRegex regex, byte[] input, int operation, CancellationToken token)
    {
        switch (operation)
        {
            case 0: _ = regex.Search(input, 0, token); break;
            case 1: _ = regex.SearchToString(input, 0, token); break;
            case 2: _ = regex.Match(input, 0, token); break;
            case 3: _ = regex.MatchToString(input, 0, token); break;
            case 4: _ = regex.FullMatch(input, 0, token); break;
            default: _ = regex.FullMatchToString(input, 0, token); break;
        }
    }
}
