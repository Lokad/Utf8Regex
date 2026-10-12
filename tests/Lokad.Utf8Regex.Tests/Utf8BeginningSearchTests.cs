using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BeginningSearchTests
{
    private static Utf8ResourcePolicy Policy(ulong work)
        => new() { MaxWorkSteps = work, MaxDepth = 1024, MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2047)] [InlineData(2048)]
    [InlineData(2049)] [InlineData(4095)] [InlineData(4096)] [InlineData(4097)]
    public void ScalarLineStartsOffsetsAndChunkTails(int padding)
    {
        var text = new string('x', padding) + "é😀\r\naaa\r\n\na\n";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = mode | RegexOptions.Multiline | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex("^(a+)", options, Regex.InfiniteMatchTimeout, Policy(100000));
            var oracle = new Regex("^(a+)", options);
            Assert.Equal(2, regex.Count(input));
            Assert.True(regex.IsMatch(input));
            var actual = regex.MatchDetailed(input);
            Assert.Equal(oracle.Match(text).Index, actual.IndexInUtf16);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, actual.IndexInUtf16)), actual.IndexInBytes);
            Assert.Equal("aaa", actual.GetGroup(1).GetValueString());
            foreach (var offset in new[] { 0, padding, padding + 3, padding + 5, padding + 6, text.Length })
                Assert.Equal(oracle.Matches(text, offset).Count, regex.CountFromUtf16Offset(input, offset));
            var positions = new List<int>();
            foreach (var match in regex.EnumerateMatches(input)) positions.Add(match.IndexInUtf16);
            Assert.Equal(oracle.Matches(text).Select(match => match.Index).ToArray(), positions);
        }
    }

    [Theory]
    [InlineData("^", "", RegexOptions.Multiline)]
    [InlineData("^", "\n\r\n", RegexOptions.Multiline)]
    [InlineData("^", "a\r\nb\r\nc", RegexOptions.None)]
    [InlineData("\\A(a*)", "aa\naaa", RegexOptions.Multiline)]
    [InlineData("^(a*)", "é\ra\u2028a\u2029a\u0085a\naa\n", RegexOptions.Multiline)]
    [InlineData("^(a+)\\1$", "aaaa\naa\naba\n", RegexOptions.Multiline)]
    [InlineData("^(a+)|b+", "xxbb\naaa", RegexOptions.Multiline)]
    [InlineData("(^)?(a+)", "xxaa\naa", RegexOptions.Multiline)]
    [InlineData("^(a+)", "aaa\naaa", RegexOptions.Multiline | RegexOptions.RightToLeft)]
    [InlineData("^(a+)", "aaa\naaa", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    [InlineData("(?m)^(a+)", "xx\naa", RegexOptions.None)]
    [InlineData("(?m)(?-m:^(a+))", "xx\naa", RegexOptions.None)]
    public void EmptyAbsoluteInlineAndUnqualifiedShapes(string pattern, string text, RegexOptions mode)
    {
        var options = mode | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Policy(100000));
        var oracle = new Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(oracle.Count(text), regex.Count(input));
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        var positions = new List<int>();
        foreach (var match in regex.EnumerateMatches(input)) positions.Add(match.IndexInUtf16);
        Assert.Equal(oracle.Matches(text).Select(match => match.Index).ToArray(), positions);
    }

    [Fact]
    public void FilteringStartsDoesNotConstrainTheMatchBodyOrScalarWidth()
    {
        const string pattern = "^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$";
        var text = "xximport \"absent\"\nimport shared \"é\n😀\" tail\nimport \"b\"\n";
        var input = Encoding.UTF8.GetBytes(text);
        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, Policy(100000));
        Assert.Equal(2, regex.Count(input));
        var actual = regex.MatchDetailed(input);
        Assert.Equal("é\n😀", actual.GetGroup("path").GetValueString());
        Assert.Equal(new Regex(pattern, options).Match(text).Value, actual.GetValueString());
        var scalar = new Utf8Regex("^(.)", options, Regex.InfiniteMatchTimeout, Policy(100000));
        var scalarMatch = scalar.MatchDetailed("xx\n😀x"u8);
        Assert.Equal(2, scalar.Count("xx\n😀x"u8));
        Assert.Equal(1, scalarMatch.LengthInBytes);
        var afterFirst = scalar.MatchDetailedFromUtf16Offset("xx\n😀x"u8, 2);
        Assert.Equal(4, afterFirst.LengthInBytes);
        Assert.Equal("😀", afterFirst.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void OriginalInterruptionCoversLongSearchAfterValidation(bool findFirst, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var input = new byte[320000]; Array.Fill(input, (byte)'x');
        var regex = new Utf8Regex("^(a+)", RegexOptions.CultureInvariant | RegexOptions.Multiline,
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, Policy(1_000_000));
        var probe = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref probe);
        var validationSteps = probe.Budget.Steps;
        var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (triggered || steps <= validationSteps) return;
            triggered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        });
        try
        {
            void Run()
            {
                if (findFirst) _ = regex.Match(input, source.Token);
                else _ = regex.Count(input, source.Token);
            }
            if (expire) Assert.Throws<RegexMatchTimeoutException>(Run);
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(triggered);
        }
        finally { diagnostics.Complete(); }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2047)] [InlineData(2048)]
    [InlineData(2049)] [InlineData(4095)] [InlineData(4096)] [InlineData(4097)]
    public void ExactScanEdgesTrailingLfAndNoLf(int padding)
    {
        var regex = new Utf8Regex("^(a*)", RegexOptions.Multiline, Regex.InfiniteMatchTimeout, Policy(100000));
        foreach (var ending in new[] { "", "\n", "\na" })
        {
            var text = new string('x', padding) + ending;
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex("^(a*)", RegexOptions.Multiline);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Assert.Equal(oracle.Matches(text, padding).Count, regex.CountFromUtf16Offset(input, padding));
            Assert.Equal(oracle.Match(text, padding).Success, regex.MatchFromUtf16Offset(input, padding).Success);
        }
    }

    [Fact]
    public void WorkCutoffCountsBoundedScansAndFailedCallsDoNotPoisonFreshCalls()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 4097) + "\naaa\n");
        ulong low = 1, high = 1000;
        while (low < high)
        {
            var cap = low + (high - low) / 2;
            try { Assert.Equal(1, new Utf8Regex("^(a+)", RegexOptions.Multiline, Regex.InfiniteMatchTimeout, Policy(cap)).Count(input)); high = cap; }
            catch (Utf8ResourceLimitExceededException failure)
            {
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
                Assert.Equal(cap + 1, failure.Requested); low = cap + 1;
            }
        }
        var denied = new Utf8Regex("^(a+)", RegexOptions.Multiline, Regex.InfiniteMatchTimeout, Policy(low - 1));
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input));
        Assert.Equal(1, denied.Count("aaa"u8));
        var admitted = new Utf8Regex("^(a+)", RegexOptions.Multiline, Regex.InfiniteMatchTimeout, Policy(low));
        Assert.Equal(1, admitted.Count(input)); Assert.Equal(1, admitted.Count(input));
    }

    [Fact]
    public void ImpossibleAnchorStillValidatesTheCompleteSubject()
    {
        var regex = new Utf8Regex("^(a+)", RegexOptions.Multiline, Regex.InfiniteMatchTimeout, Policy(100000));
        byte[] input = [.. "aaa\nxx"u8, 0xF0, 0x9F];
        Assert.Throws<ArgumentException>(() => regex.Count(input));
        Assert.Throws<ArgumentException>(() => regex.IsMatch(input));
        Assert.Throws<ArgumentException>(() => regex.Match(input));
    }
}
