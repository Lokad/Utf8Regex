using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReScalarOneShotTests
{
    [Theory]
    [InlineData("prefix[0-9]+", false)]
    [InlineData("(?P<n>prefix[0-9]+)", false)]
    [InlineData("pre.*post", true)]
    [InlineData("(?P<n>pre.*post)", true)]
    public void PublicValuesTextAndCapturesKeepScalarSuffixAndOffsetSemantics(string pattern, bool full)
    {
        using var source = new CancellationTokenSource();
        foreach (var dotAll in new[] { false, true })
        foreach (var mode in new[] { 0, 1, 2 })
        {
            var options = dotAll ? PythonReCompileOptions.DotAll : PythonReCompileOptions.None;
            var policy = mode == 2 ? new Utf8ResourcePolicy
            {
                MaxWorkSteps = 5_000_000, MaxDepth = 8192,
                MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20,
            } : default;
            var regex = new Utf8PythonRegex(pattern, options,
                mode == 2 ? TimeSpan.FromSeconds(10) : Timeout.InfiniteTimeSpan, policy);
            var token = mode == 0 ? default : source.Token;
            // This ASCII grammar has only unconstrained DotAll repetition over
            // supplementary subjects: the BCL oracle has equivalent extents.
            var oracle = new Regex(@"\G(?:" + pattern.Replace("(?P<", "(?<", StringComparison.Ordinal) + ")" + (full ? @"\z" : ""),
                RegexOptions.CultureInvariant | (dotAll ? RegexOptions.Singleline : RegexOptions.None));
            foreach (var text in new[] { "", "prefix", "prefix123", "prefix123١", "😀prefix123 tail",
                "épre\n😀post", "prepost", "pre😀post", "pre😀postx", "prepostprepost", "prepost\n" })
            {
                var input = Encoding.UTF8.GetBytes(text);
                var firstWidth = text.Length == 0 ? 0 : Rune.GetRuneAt(text, 0).Utf16SequenceLength;
                foreach (var index in new[] { 0, firstWidth, text.Length }.Distinct())
                {
                    var start = Encoding.UTF8.GetByteCount(text.AsSpan(0, index));
                    var expected = oracle.Match(text, index);
                    var actual = full ? regex.FullMatch(input, start, token) : regex.Match(input, start, token);
                    var rendered = full ? regex.FullMatchToString(input, start, token) : regex.MatchToString(input, start, token);
                    Assert.Equal(expected.Success, actual.Success);
                    Assert.Equal(expected.Success ? expected.Value : null, rendered);
                    if (!expected.Success) continue;
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index + expected.Length)), actual.EndOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetBytes(expected.Value), actual.GetValueBytes().ToArray());
                    if (!pattern.StartsWith("(?P<", StringComparison.Ordinal)) continue;
                    var detailed = full ? regex.FullMatchDetailedData(input, start, token) : regex.MatchDetailedData(input, start, token);
                    Assert.True(detailed.Success);
                    Assert.Equal(expected.Groups[1].Value, detailed.Groups[1].ValueText);
                }
            }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LongScalarDiscoveryKeepsOriginalTokenAndDeadline(bool full, bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var pattern = full ? "pre.*post" : "prefix[0-9]+";
        // The interval runner charges per bounded block. Cross the invocation's
        // 64-step diagnostic interval even when it consumes a block per step.
        var input = Encoding.UTF8.GetBytes(full ? "pre" + new string('a', 262144) + "😀post" : "prefix" + new string('1', 262144));
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.DotAll,
            expire ? TimeSpan.FromMilliseconds(200) : Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 5_000_000, MaxDepth = 8192, MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered) return;
            entered = true;
            if (expire) ticks = 250; else source.Cancel();
        });
        try
        {
            void Run() { if (full) _ = regex.FullMatch(input, 0, source.Token); else _ = regex.Match(input, 0, source.Token); }
            if (expire)
            {
                var failure = Assert.Throws<RegexMatchTimeoutException>(Run);
                Assert.Equal(pattern, failure.Pattern);
                Assert.Equal(regex.MatchTimeout, failure.MatchTimeout);
            }
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.True(full ? regex.FullMatch("pre😀post"u8).Success : regex.Match("prefix123"u8).Success);
    }
}
