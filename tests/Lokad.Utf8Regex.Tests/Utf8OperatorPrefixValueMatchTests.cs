using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8OperatorPrefixValueMatchTests
{
    private const string Pattern = @"\G[=+\-^*/.*<>~!&|?]+";
    private static Utf8ResourcePolicy Policy => new() { MaxWorkSteps = 1000000,
        MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Fact]
    public void AsciiValuesUseTheExistingInvocationWithoutCaptureWorkspace()
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Pattern, mode | RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan,
                Policy with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            Assert.Equal("AnchoredOperatorRun", regex.Inspection.DebugFallbackDirectFamilyKind);
            foreach (var text in new[] { "", "word+=", " +", "+", "==!=rest", "+-^*/.<>~!&|?=tail", "=\0tail", "=\r\n+", "\n++" })
            {
                var expected = new Regex(Pattern, mode | RegexOptions.CultureInvariant).Match(text);
                var input = Encoding.ASCII.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    var value = regex.Match(storage.AsSpan(1, input.Length), token.Token);
                    Assert.Equal(expected.Success, value.Success);
                    if (value.Success)
                        Assert.Equal((0, expected.Length, 0, expected.Length),
                            (value.IndexInBytes, value.LengthInBytes, value.IndexInUtf16, value.LengthInUtf16));
                    Assert.Equal(Utf8ExecutionRoute.NativeAsciiAnchoredValidator, session.ExecutionRoute);
                    Assert.Equal(value, regex.Match(input));
                }
                finally { session.Complete(); }
            }
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void LongSubjectsRetainTightWorkingAdmission(int length)
    {
        var input = Encoding.ASCII.GetBytes(new string('+', length));
        Assert.Equal(length, new Utf8Regex(Pattern).Match(input).LengthInBytes);
        var tight = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            Policy with { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(length, tight.Match(input).LengthInBytes);
    }

    [Fact]
    public void UnicodeOffsetsCapturesAndProgressionKeepTheirSemantics()
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase, RegexOptions.RightToLeft })
        foreach (var text in new[] { "+=rest", "+=😀", "😀+=", "a+=", "==\n+" })
        {
            var expected = new Regex(Pattern, mode | RegexOptions.CultureInvariant).Match(text);
            var regex = new Utf8Regex(Pattern, mode | RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan, Policy);
            var input = Encoding.UTF8.GetBytes(text); var actual = regex.Match(input, token.Token);
            Assert.Equal(expected.Success, actual.Success);
            if (actual.Success)
                Assert.Equal((expected.Index, expected.Length, Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), expected.Length),
                    (actual.IndexInUtf16, actual.LengthInUtf16, actual.IndexInBytes, actual.LengthInBytes));
        }
        var offsetRegex = new Utf8Regex(Pattern);
        var offsetInput = "😀x+=rest"u8.ToArray();
        var selected = offsetRegex.MatchFromUtf16Offset(offsetInput, 3);
        Assert.Equal((5, 2, 3, 2), (selected.IndexInBytes, selected.LengthInBytes, selected.IndexInUtf16, selected.LengthInUtf16));
        Assert.False(offsetRegex.MatchFromUtf16Offset(offsetInput, 2).Success);
        var captured = new Utf8Regex(@"\G(?<op>[=+\-^*/.*<>~!&|?]+)");
        var bytes = "+=-x"u8.ToArray(); var value = captured.Match(bytes); var detailed = captured.MatchDetailed(bytes);
        var cursor = captured.EnumerateMatches(bytes); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        _ = captured.Match("++"u8);
        Assert.Equal("+=-", detailed.GetGroup("op").GetValueString());
        Assert.Equal(1, captured.Count(bytes));
        var nonBacktracking = new Utf8Regex(Pattern.Replace(@"\G", @"\A"), RegexOptions.NonBacktracking, Timeout.InfiniteTimeSpan, Policy);
        Assert.Equal(3, nonBacktracking.Match(bytes, token.Token).LengthInBytes);
    }

    [Fact]
    public void LargeReverseFallbackStillEnforcesItsWorkLimit()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.RightToLeft, Timeout.InfiniteTimeSpan,
            Policy with { MaxWorkSteps = 1000 });
        var input = Encoding.ASCII.GetBytes(new string('+', 2049));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input)).ResourceKind);
    }

    [Fact]
    public void MalformedTailsAndOriginalEntryCancellationStayStrict()
    {
        var regex = new Utf8Regex(Pattern);
        foreach (var head in new[] { "+=", "wrong" })
        {
            byte[] malformed = [.. Encoding.UTF8.GetBytes(head), 0xff];
            Assert.Throws<ArgumentException>(() => regex.Match(malformed));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(malformed, canceled.Token)).CancellationToken);
        }
    }

    [Theory]
    [InlineData("+=!rest", true)] [InlineData("wrong", false)]
    public void WorkRefusalCannotPublishSuccessOrNoMatch(string text, bool success)
    {
        var input = Encoding.ASCII.GetBytes(text); var admitted = false; var denied = false;
        for (ulong work = 1; work < 100; work++)
        {
            var regex = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
                Policy with { MaxWorkSteps = work, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            try { Assert.Equal(success, regex.Match(input).Success); admitted = true; break; }
            catch (Utf8ResourceLimitExceededException error) { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); denied = true; }
        }
        Assert.True(denied); Assert.True(admitted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationChecksTheOriginalTokenAndDeadline(bool expire)
    {
        var reads = 0; var trigger = int.MaxValue; long ticks = 0;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == trigger) { if (expire) ticks = 100; else token.Cancel(); } return ticks; });
        var regex = new Utf8Regex(Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50), Policy);
        Assert.True(regex.Match("+=!rest"u8, token.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("+=!rest"u8, token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("+=!rest"u8, token.Token)).CancellationToken);
    }
}
