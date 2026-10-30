using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WordRunCountTests
{
    [Theory]
    [InlineData(@"\b\w{10,}\b")]
    [InlineData(@"\w{10,}")]
    [InlineData(@"(?:\b)(?:[\w]){10,}(?:\b)")]
    [InlineData(@"(\b\w{10,}\b)")]
    [InlineData(@"\w+")]
    [InlineData(@"(\w{2,})")]
    public void CompleteWordProofCountsRunsAcrossBlocks(string pattern)
    {
        var text = "short 123456789 1234567890 _abcdEFGHij " + new string('z', 31) + " " + new string('a', 32) + " " + new string('_', 33) + " !";
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase })
        {
            var regex = new Utf8Regex(pattern, options); var input = Encoding.UTF8.GetBytes(text);
            var expected = new Regex(pattern, options).Count(text);
            var diagnostic = regex.CollectCountDiagnostics(input);
            Assert.Equal(expected, diagnostic.Result); Assert.Equal("fallback_direct_ascii_word_bounded", diagnostic.ExecutionRoute);
            Assert.Equal(expected, regex.Count(input)); Assert.Equal(expected, regex.Count(input, CancellationToken.None));
        }
    }

    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(10)]
    public void AsciiMembershipAndMinimumsAgreeWithIndependentRegex(int minimum)
    {
        var random = new Random(713); var characters = new char[517];
        foreach (var bounded in new[] { false, true })
        {
            var repeat = @"\w{" + minimum + ",}"; var pattern = bounded ? @"\b" + repeat + @"\b" : repeat;
            var regex = new Utf8Regex(pattern); var oracle = new Regex(pattern);
            for (var trial = 0; trial < 64; trial++)
            {
                for (var i = 0; i < characters.Length; i++) characters[i] = (char)random.Next(128);
                var text = new string(characters) + " abcdefghijklmnopqrstuvwxyz";
                Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
            }
        }
    }

    [Theory]
    [InlineData(@"\w{10,}?")]
    [InlineData(@"\b\w{10,}?\b")]
    [InlineData(@"\w{0,}")]
    [InlineData(@"\w{10,20}")]
    [InlineData(@"[\w-]{10,}")]
    [InlineData(@"\B\w{10,}\B")]
    [InlineData(@"\b\w{10,}\b!")]
    [InlineData(@"\w{10,2147483647}")]
    public void UnprovedShapesKeepTheirOwnedSelection(string pattern)
    {
        const string text = "abcdefghijklmnopqrstuvwx abcdefghijk-abcdefghijk!";
        var regex = new Utf8Regex(pattern); var diagnostic = regex.CollectCountDiagnostics(Encoding.UTF8.GetBytes(text));
        Assert.Equal(new Regex(pattern).Count(text), diagnostic.Result);
        Assert.NotEqual("fallback_direct_ascii_word_bounded", diagnostic.ExecutionRoute);
    }

    [Theory] [InlineData(RegexOptions.RightToLeft)] [InlineData(RegexOptions.NonBacktracking)] [InlineData(RegexOptions.ECMAScript)]
    public void DirectionAndOtherExecutionContractsRetainOwnedBehavior(RegexOptions options)
    {
        const string pattern = @"\b\w{10,}\b"; const string text = "abc abcdefghijkl abcdefghijklmno";
        var regex = new Utf8Regex(pattern, options); var diagnostic = regex.CollectCountDiagnostics(Encoding.UTF8.GetBytes(text));
        Assert.Equal(new Regex(pattern, options).Count(text), diagnostic.Result);
        Assert.NotEqual("fallback_direct_ascii_word_bounded", diagnostic.ExecutionRoute);
    }

    [Fact]
    public void WiderScalarsRetainScalarMembershipAndOriginalAuthority()
    {
        const string pattern = @"\b\w{10,}\b";
        var texts = new[] { new string('é', 10), string.Concat(Enumerable.Repeat("𐐀", 10)), "😀 " + new string('a', 10) + " 😀" };
        foreach (var text in texts)
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options); var diagnostic = regex.CollectCountDiagnostics(Encoding.UTF8.GetBytes(text));
            Assert.Equal(1, diagnostic.Result); Assert.NotEqual("fallback_direct_ascii_word_bounded", diagnostic.ExecutionRoute);
        }
    }

    [Fact]
    public void UsefulFinitePoliciesAdmitTheConstantSpaceCounter()
    {
        var regex = new Utf8Regex(@"\b\w{10,}\b", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        using var token = new CancellationTokenSource();
        Assert.Equal(2, regex.Count("abcdefghijkl abcdefghijklmno"u8, token.Token));
        Assert.Equal("fallback_direct_ascii_word_bounded", regex.CollectCountDiagnostics("abcdefghijkl"u8).ExecutionRoute);
        var tooLittle = new Utf8Regex(@"\b\w{10,}\b", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => tooLittle.Count(new byte[4096]));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void NativeWorkObservesOriginalCancellationAndDeadline(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var canceled = new CancellationTokenSource();
        var regex = new Utf8Regex(@"\b\w{10,}\b", RegexOptions.None, timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcdefghijkl ", 1024))); var reached = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (Utf8SearchDiagnosticsSession.Current!.ExecutionRoute != Utf8ExecutionRoute.FallbackDirectAsciiWordBounded) return;
            reached = true; if (timeout) ticks = 100; else canceled.Cancel();
        });
        try
        {
            var failure = Record.Exception(() => regex.Count(input, canceled.Token)); Assert.True(reached);
            if (timeout) { var error = Assert.IsType<RegexMatchTimeoutException>(failure); Assert.Empty(error.Input); Assert.Equal(regex.Pattern, error.Pattern); }
            else Assert.Equal(canceled.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void FullSubjectValidationAndPreCancellationRemainAuthoritative()
    {
        var regex = new Utf8Regex(@"\b\w{10,}\b");
        var malformed = Encoding.UTF8.GetBytes("abcdefghijkl ").Concat(new byte[] { 0xf0, 0x9f }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, token.Token)).CancellationToken);
        Assert.Equal(0, regex.Count(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void NativeCounterCannotOutrunFiniteWorkAfterValidation()
    {
        const string pattern = @"\b\w{10,}\b";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcdefghijkl ", 1024))); ulong firstNative = 0;
        var reference = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 });
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (firstNative == 0 && Utf8SearchDiagnosticsSession.Current!.ExecutionRoute == Utf8ExecutionRoute.FallbackDirectAsciiWordBounded) firstNative = steps;
        });
        try { Assert.Equal(1024, reference.Count(input)); } finally { session.Complete(); }
        Assert.True(firstNative > 1);
        var limited = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = firstNative - 1 });
        session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Count(input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
            Assert.Equal(Utf8ExecutionRoute.FallbackDirectAsciiWordBounded, session.ExecutionRoute);
        }
        finally { session.Complete(); }
    }
}
