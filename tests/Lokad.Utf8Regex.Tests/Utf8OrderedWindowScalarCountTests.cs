using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OrderedWindowScalarCountTests
{
    private const string Window = "Holmes.{0,25}Watson|Watson.{0,25}Holmes";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedWindowsAgreeWithFiniteOwnedScalarCounter(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        string[] patterns = [Window, "a.{1,1}b", "a.{1,2}b", "a.{0,3}b", "a.{2,4}b",
            "(?:a|ab).{0,3}z", "foo.{0,3}bar|bar.{0,3}foo", @"a.{0,2}\nb",
            "(a).{0,3}b", @"a\s+.{0,3}b", "a.{0,4096}b"];
        string[] texts = ["", "é😀 a😀b a😀😀b a\rb a\nb a😀x😀b a😀😀😀b",
            "é a😀bz ab😀z a\u2003b a \u2003b a😀\nb a\r\nb",
            "é Holmes😀Watson WatsonéHolmes Holmes\rWatson Holmes\nWatson",
            "é Holmes" + new string('x', 24) + "😀Watson Watson" + new string('x', 25) + "😀Holmes",
            "é fooxbar bar😀foo foo😀😀😀bar foo😀😀😀😀bar"];
        using var cancellation = new CancellationTokenSource();
        foreach (var pattern in patterns)
        {
            var regex = new Utf8Regex(pattern, options);
            var scalar = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, resourcePolicy: new()
            {
                MaxWorkSteps = 1_000_000, MaxDepth = 128,
                MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576
            });
            foreach (var text in texts)
            foreach (var offset in new[] { 0, 15, 31, 63 })
            {
                var input = Encoding.UTF8.GetBytes(new string('x', offset) + text);
                var expected = scalar.Count(input);
                Assert.Equal(expected, regex.Count(input));
                Assert.Equal(expected, regex.Count(input, cancellation.Token));
            }
        }
        Assert.Equal(1, new Utf8Regex("a.{1,1}b", options).Count("a😀b"u8));
        Assert.Equal(0, new Utf8Regex("a.{1,1}b", options).Count("a😀😀b"u8));
    }

    [Fact]
    public void PublicMixedCountUsesExistingWindowCounter()
    {
        var regex = new Utf8Regex(Window);
        var sawWindow = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            sawWindow |= new StackTrace().GetFrames().Any(frame =>
                frame.GetMethod()?.DeclaringType == typeof(AsciiOrderedLiteralWindowExecutor));
        });
        try
        {
            // LF separates pairs so greedy dot cannot join adjacent matches;
            // enough candidates reach a matching checkpoint after validation.
            var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
                "é Holmes😀Watson\nWatson\rHolmes\n", 512)));
            Assert.Equal(1024, regex.Count(input));
            Assert.True(sawWindow);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void MixedCountKeepsWholeSubjectValidationAndOriginalCancellation()
    {
        var regex = new Utf8Regex(Window);
        var malformed = Encoding.UTF8.GetBytes("é HolmesxWatson").Concat(new byte[] { 0xFF }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(cancellation.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count("é HolmesxWatson"u8, cancellation.Token)).CancellationToken);
    }

    [Fact]
    public void MatchingCheckpointCancelsWithTheOriginalToken()
    {
        var regex = new Utf8Regex(Window);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
            "é Holmes😀Watson\nWatson\rHolmes\n", 512)));
        using var cancellation = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (new StackTrace().GetFrames().Any(frame =>
                frame.GetMethod()?.DeclaringType == typeof(AsciiOrderedLiteralWindowExecutor)))
                cancellation.Cancel();
        });
        try
        {
            Assert.Equal(cancellation.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token)).CancellationToken);
        }
        finally { session.Complete(); }
    }
}
