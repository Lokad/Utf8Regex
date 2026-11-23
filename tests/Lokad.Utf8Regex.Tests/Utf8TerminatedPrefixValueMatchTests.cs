using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TerminatedPrefixValueMatchTests
{
    private const string Pattern = @"\G///[^\n]*\n";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Theory]
    [InlineData("", false)]
    [InlineData("///", false)]
    [InlineData("///x", false)]
    [InlineData("///\n", true)]
    [InlineData("///x\r\nrest\n", true)]
    [InlineData("///\0\n///next\n", true)]
    [InlineData("wrong\n///x\n", false)]
    public void CompleteAsciiRangeUsesTheBoundedOriginalInvocation(string text, bool success)
    {
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled | RegexOptions.Multiline })
        {
            var regex = new Utf8Regex(Pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded with
                {
                    MaxDepth = 1,
                    MaxWorkingBytes = 1,
                    MaxResultBytes = 1
                }
            });
            var expected = new Regex(Pattern, options).Match(text); var bytes = Encoding.UTF8.GetBytes(text);
            byte[] storage = [0xff, .. bytes, 0xff];
            var diagnostics = Utf8SearchDiagnosticsSession.Start();
            try
            {
                var actual = regex.Match(storage.AsSpan(1, bytes.Length), cancellationToken: token.Token);
                Assert.Equal(success, actual.Success); Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length)), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            }
            finally { diagnostics.Complete(); }
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void LongSubjectsRetainTightWorkingAdmission(int length)
    {
        var bytes = Encoding.ASCII.GetBytes("///" + new string('x', length - 4) + "\n");
        Assert.Equal(length, (new Utf8Regex(Pattern).Match(bytes).EndOffsetInBytes - new Utf8Regex(Pattern).Match(bytes).StartOffsetInBytes));
        var tight = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Funded with
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(length, (tight.Match(bytes).EndOffsetInBytes - tight.Match(bytes).StartOffsetInBytes));
    }

    [Fact]
    public void OriginsAndUnicodeCapturesKeepTheirByteRanges()
    {
        var regex = new Utf8Regex(Pattern);
        var bytes = "😀///é\n///next\n"u8.ToArray();
        Assert.False(regex.Match(bytes).Success);
        var offset = regex.Match(bytes, Encoding.UTF8.GetByteCount(Encoding.UTF8.GetString(bytes).AsSpan(0, 2)), []);
        Assert.Equal((4, 6), (offset.StartOffsetInBytes, offset.EndOffsetInBytes - offset.StartOffsetInBytes));
        var captured = new Utf8Regex(@"\G(?<prefix>///)(?<body>[^\n]*)\n");
        var input = "///value\n///next\n"u8.ToArray();
        var value = captured.Match(input); var detailed = Utf8MatchSequence.First(captured.CreateMatchCursor(input));
        Assert.Equal("value", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(captured.GroupNumberFromUtf8Name("body"u8)).Span));
        Utf8MatchSequence.EqualValue(value, captured.Match(input, 0, []));
        var cursor = captured.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        _ = captured.Match("///other\n"u8);
        Assert.Equal("value", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(captured.GroupNumberFromUtf8Name("body"u8)).Span));
        foreach (var pattern in new[] { Pattern, @"\Ghello[^;]*;", @"\G///[^\n]*\nEND" })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase, RegexOptions.RightToLeft })
        foreach (var text in new[] { "///é😀\nrest", "helloé;tail;", "///hi\nEND", "///hi\nwrong" })
        {
            var expected = new Regex(pattern, options).Match(text);
            var actual = new Utf8Regex(pattern, options: options).Match(Encoding.UTF8.GetBytes(text));
                    Assert.Equal((expected.Success, System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length))), (actual.Success, actual.StartOffsetInBytes, (actual.EndOffsetInBytes - actual.StartOffsetInBytes)));
                }
        using var token = new CancellationTokenSource(); token.Cancel();
        var denied = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Funded with
            {
                MaxWorkSteps = 1
            }
        });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Match(input));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationObservesTheOriginalTokenAndDeadline(bool expire)
    {
        var reads = 0; var trigger = int.MaxValue; long ticks = 0;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == trigger) { if (expire) ticks = 100; else token.Cancel(); }
            return ticks;
        });
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50),
            ResourcePolicy = Funded
        });
        Assert.True(regex.Match("///value\n"u8, cancellationToken: token.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("///value\n"u8, cancellationToken: token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("///value\n"u8, cancellationToken: token.Token)).CancellationToken);
    }
}
