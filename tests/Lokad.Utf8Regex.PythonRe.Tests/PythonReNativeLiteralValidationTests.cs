using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using CoreRegex = Lokad.Utf8Regex.Utf8Regex;

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

    public static IEnumerable<object[]> MalformedSubjects()
    {
        var needle = Encoding.UTF8.GetBytes("Шерлок");
        yield return new object[] { new byte[] { 0xff }.Concat(needle).ToArray(), 0 };
        yield return new object[] { new byte[] { 0xc2 }.Concat(needle).ToArray(), 0 };
        yield return new object[] { needle.Append((byte)0xff).ToArray(), needle.Length };
        yield return new object[] { new byte[] { 0xc0, 0x80 }, 0 };
        yield return new object[] { new byte[] { 0xed, 0xa0, 0x80 }, 0 };
        yield return new object[] { new byte[] { 0xf0, 0x9f, 0x98 }, 0 };
        yield return new object[] { Encoding.UTF8.GetBytes("é😀").Concat(needle).Append((byte)0xff).ToArray(), 6 + needle.Length };
        yield return new object[] { Enumerable.Repeat((byte)'x', 8193).Append((byte)0xff).ToArray(), 8193 };
    }

    [Theory]
    [MemberData(nameof(MalformedSubjects))]
    public void EveryNativeOutcomeRejectsTheWholeMalformedSubject(byte[] input, int offset)
    {
        var regex = new Utf8PythonRegex("Шерлок");
        for (var operation = 0; operation < 6; operation++)
        {
            var error = Assert.Throws<ArgumentException>(() => Run(regex, input, operation, default));
            Assert.Contains($"byte offset {offset}.", error.Message);
        }
        Assert.True(regex.Search("Шерлок"u8).Success);
    }

    [Fact]
    public void BorrowedInvocationPreservesCompleteHitMetadataAndParentLoan()
    {
        var regex = new CoreRegex("é😀");
        var input = Encoding.UTF8.GetBytes("中é😀尾");
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Assert.True(regex.FindLiteralForResult(input, 0, ref invocation, out var start, out var end, out var validation));
        Assert.Equal((3, 9), (start, end));
        Assert.Equal((input.Length, 5, false, true),
            (validation.ByteLength, validation.Utf16Length, validation.IsAscii, validation.ContainsSupplementaryScalars));
        Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Fact]
    public void ControlledWorkDenialStillPrecedesMalformedInputAndReleasesNoParentLoan()
    {
        var regex = new CoreRegex("Шерлок");
        var invocation = new Utf8SliceInvocation(new(1, 1, 7, 1), Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        Exception? failure = null;
        try { _ = regex.FindLiteralForResult([0xff], 0, ref invocation, out _, out _, out _); }
        catch (Exception error) { failure = error; }
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, invocation.Budget.LimitBreach);
        Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Fact]
    public void OriginalTokenPrecedesMalformedInputAndFreshCallsRecover()
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

    [Fact]
    public void DiagnosticHooksKeepValidationAheadOfDiscovery()
    {
        var regex = new CoreRegex("Шерлок");
        var input = new byte[] { 0xff }.Concat(Enumerable.Repeat((byte)'x', 200000))
            .Concat(Encoding.UTF8.GetBytes("Шерлок")).ToArray();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; throw new ApplicationException("Discovery ran before validation."); };
        Exception? failure = null;
        try { _ = regex.FindLiteralForResult(input, 0, ref invocation, out _, out _, out _); }
        catch (Exception error) { failure = error; }
        Assert.Contains("byte offset 0.", Assert.IsType<ArgumentException>(failure).Message);
        Assert.False(entered);
        Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Theory]
    [InlineData(4)] [InlineData(5)]
    public void ControlledDeadlineStillPrecedesMalformedTail(int operation)
    {
        var regex = new Utf8PythonRegex("Шерлок", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1),
            new() { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Enumerable.Repeat((byte)'x', 65536).Append((byte)0xff).ToArray();
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++reads < 6 ? 0 : 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => Run(regex, input, operation, default));
        Assert.Equal(regex.Pattern, error.Pattern);
        Assert.Empty(error.Input);
        Assert.True(reads >= 6);
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
