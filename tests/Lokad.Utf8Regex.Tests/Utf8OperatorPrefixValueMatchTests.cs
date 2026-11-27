using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8OperatorPrefixValueMatchTests
{
    private const string Pattern = @"\G[=+\-^*/.*<>~!&|?]+";
    private static Utf8ResourcePolicy Policy => new() { MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Fact]
    public void AsciiValuesUseTheExistingInvocationWithoutCaptureWorkspace()
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Pattern, options: mode | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy with
                {
                    MaxDepth = 1,
                    MaxWorkingBytes = 1,
                    MaxResultBytes = 1
                }
            });
            foreach (var text in new[] { "", "word+=", " +", "+", "==!=rest", "+-^*/.<>~!&|?=tail", "=\0tail", "=\r\n+", "\n++" })
            {
                var expected = new Regex(Pattern, mode | RegexOptions.CultureInvariant).Match(text);
                var input = Encoding.ASCII.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
                var value = regex.Match(storage.AsSpan(1, input.Length), cancellationToken: token.Token);
                Assert.Equal(expected.Success, value.Success);
                if (value.Success)
                    Assert.Equal((0, System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length))), (value.StartOffsetInBytes, (value.EndOffsetInBytes - value.StartOffsetInBytes)));
                Assert.Equal(value, regex.Match(input));
            }
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void LongSubjectsRetainTightWorkingAdmission(int length)
    {
        var input = Encoding.ASCII.GetBytes(new string('+', length));
        Assert.Equal(length, (new Utf8Regex(Pattern).Match(input).EndOffsetInBytes - new Utf8Regex(Pattern).Match(input).StartOffsetInBytes));
        var tight = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Policy with
            {
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        Assert.Equal(length, (tight.Match(input).EndOffsetInBytes - tight.Match(input).StartOffsetInBytes));
    }

    [Fact]
    public void UnicodeOffsetsCapturesAndProgressionKeepTheirSemantics()
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase, RegexOptions.RightToLeft })
        foreach (var text in new[] { "+=rest", "+=😀", "😀+=", "a+=", "==\n+" })
        {
            var expected = new Regex(Pattern, mode | RegexOptions.CultureInvariant).Match(text);
            var regex = new Utf8Regex(Pattern, options: mode | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Policy
            });
            var input = Encoding.UTF8.GetBytes(text); var actual = regex.Match(input, cancellationToken: token.Token);
            Assert.Equal(expected.Success, actual.Success);
            if (actual.Success)
                    Assert.Equal((Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length))), (actual.StartOffsetInBytes, (actual.EndOffsetInBytes - actual.StartOffsetInBytes)));
            }
        var offsetRegex = new Utf8Regex(Pattern);
        var offsetInput = "😀x+=rest"u8.ToArray();
        var selected = offsetRegex.Match(offsetInput, 5, []);
        Assert.Equal((5, 2), (selected.StartOffsetInBytes, selected.EndOffsetInBytes - selected.StartOffsetInBytes));
        Assert.False(offsetRegex.Match(offsetInput, 4, []).Success);
        var captured = new Utf8Regex(@"\G(?<op>[=+\-^*/.*<>~!&|?]+)");
        var bytes = "+=-x"u8.ToArray(); var value = captured.Match(bytes); var detailed = Utf8MatchSequence.First(captured.CreateMatchCursor(bytes));
        var cursor = captured.EnumerateMatches(bytes); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        _ = captured.Match("++"u8);
        Assert.Equal("+=-", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(captured.GroupNumberFromUtf8Name("op"u8)).Span));
        Assert.Equal(1, captured.Count(bytes));
        var nonBacktracking = new Utf8Regex(Pattern.Replace(@"\G", @"\A"), options: RegexOptions.NonBacktracking, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Policy
        });
        Assert.Equal(3, (nonBacktracking.Match(bytes, cancellationToken: token.Token).EndOffsetInBytes - nonBacktracking.Match(bytes, cancellationToken: token.Token).StartOffsetInBytes));
    }

    [Fact]
    public void PreCanceledMatchKeepsTheOriginalTokenOnHitsAndMisses()
    {
        var regex = new Utf8Regex(Pattern);
        foreach (var text in new[] { "+=", "wrong" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(input, cancellationToken: canceled.Token)).CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationChecksTheOriginalTokenAndDeadline(bool expire)
    {
        var reads = 0; var trigger = int.MaxValue; long ticks = 0;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == trigger) { if (expire) ticks = 100; else token.Cancel(); } return ticks; });
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50),
            ResourcePolicy = Policy
        });
        Assert.True(regex.Match("+=!rest"u8, cancellationToken: token.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("+=!rest"u8, cancellationToken: token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("+=!rest"u8, cancellationToken: token.Token)).CancellationToken);
    }
}
