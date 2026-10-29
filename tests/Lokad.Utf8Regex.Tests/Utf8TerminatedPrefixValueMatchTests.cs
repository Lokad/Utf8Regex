using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TerminatedPrefixValueMatchTests
{
    private const string Pattern = @"\G///[^\n]*\n";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000,
        MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Theory]
    [InlineData("", false)] [InlineData("///", false)] [InlineData("///x", false)]
    [InlineData("///\n", true)] [InlineData("///x\r\nrest\n", true)]
    [InlineData("///\0\n///next\n", true)] [InlineData("wrong\n///x\n", false)]
    public void CompleteAsciiRangeUsesTheBoundedOriginalInvocation(string text, bool success)
    {
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled | RegexOptions.Multiline })
        {
            var regex = new Utf8Regex(Pattern, options, Timeout.InfiniteTimeSpan,
                Funded with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            Assert.Equal("AnchoredPrefixUntilByte", regex.Inspection.DebugFallbackDirectFamilyKind);
            var expected = new Regex(Pattern, options).Match(text); var bytes = Encoding.UTF8.GetBytes(text);
            byte[] storage = [0xff, .. bytes, 0xff];
            var diagnostics = Utf8SearchDiagnosticsSession.Start();
            try
            {
                var actual = regex.Match(storage.AsSpan(1, bytes.Length), token.Token);
                Assert.Equal(success, actual.Success); Assert.Equal(expected.Length, actual.LengthInUtf16);
                Assert.Equal(Utf8ExecutionRoute.NativeAsciiAnchoredValidator, diagnostics.ExecutionRoute);
            }
            finally { diagnostics.Complete(); }
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void WholeSubjectLengthBoundsHelperAdmission(int length)
    {
        var bytes = Encoding.ASCII.GetBytes("///" + new string('x', length - 4) + "\n");
        Assert.Equal(length, new Utf8Regex(Pattern).Match(bytes).LengthInBytes);
        var tight = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, Funded with { MaxWorkingBytes = 1 });
        if (length <= 2048) Assert.Equal(length, tight.Match(bytes).LengthInBytes);
        else Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tight.Match(bytes)).ResourceKind);
    }

    [Fact]
    public void OriginsUnicodeCapturesAndStrictValidationKeepTheirSemantics()
    {
        var regex = new Utf8Regex(Pattern);
        var bytes = "😀///é\n///next\n"u8.ToArray();
        Assert.False(regex.Match(bytes).Success);
        var offset = regex.MatchFromUtf16Offset(bytes, 2);
        Assert.Equal((4, 6, 2, 5), (offset.IndexInBytes, offset.LengthInBytes, offset.IndexInUtf16, offset.LengthInUtf16));
        var captured = new Utf8Regex(@"\G(?<prefix>///)(?<body>[^\n]*)\n");
        var input = "///value\n///next\n"u8.ToArray();
        var value = captured.Match(input); var detailed = captured.MatchDetailed(input);
        Assert.Equal("value", detailed.GetGroup("body").GetValueString());
        Assert.Equal(value, captured.MatchFromUtf16Offset(input, 0));
        var cursor = captured.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        _ = captured.Match("///other\n"u8);
        Assert.Equal("value", detailed.GetGroup("body").GetValueString());
        foreach (var pattern in new[] { Pattern, @"\Ghello[^;]*;", @"\G///[^\n]*\nEND" })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase, RegexOptions.RightToLeft })
        foreach (var text in new[] { "///é😀\nrest", "helloé;tail;", "///hi\nEND", "///hi\nwrong" })
        {
            var expected = new Regex(pattern, options).Match(text);
            var actual = new Utf8Regex(pattern, options).Match(Encoding.UTF8.GetBytes(text));
            Assert.Equal((expected.Success, expected.Index, expected.Length), (actual.Success, actual.IndexInUtf16, actual.LengthInUtf16));
        }
        byte[] malformed = [.. input, 0xff]; Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(malformed, token.Token)).CancellationToken);
        var denied = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, Funded with { MaxWorkSteps = 1 });
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
        var regex = new Utf8Regex(Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50), Funded);
        Assert.True(regex.Match("///value\n"u8, token.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("///value\n"u8, token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("///value\n"u8, token.Token)).CancellationToken);
    }
}
