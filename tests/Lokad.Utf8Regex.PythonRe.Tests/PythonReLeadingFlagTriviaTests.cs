using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Pinned CPython 3.13.2 receipts: artifacts/python-leading-flag-trivia-20261018.
public sealed class PythonReLeadingFlagTriviaTests
{
    [Theory]
    [InlineData("(?#note)(?i)", PythonReCompileOptions.None)]
    [InlineData("(?x) #note\n (?i)", PythonReCompileOptions.None)]
    [InlineData(" #note\n (?i)", PythonReCompileOptions.Verbose)]
    [InlineData("(?x) (?i)", PythonReCompileOptions.None)]
    [InlineData("(?x)(?#note)(?i)", PythonReCompileOptions.None)]
    [InlineData("(?i)(?#note)(?m)", PythonReCompileOptions.None)]
    [InlineData(@"(?#a\)b)(?i)", PythonReCompileOptions.None)]
    [InlineData(@"(?#a\q)(?i)", PythonReCompileOptions.None)]
    public void IgnoredLeadingTriviaKeepsGlobalFlagsAndScalarCaptures(string prefix, PythonReCompileOptions options)
    {
        var pattern = prefix + "(?P<x>😀a)";
        foreach (var bytes in new[] { false, true })
        {
            var policy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxDepth = 128,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            };
            var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options: options, policy: new Utf8RegexPolicy()
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = policy
                }) : new Utf8PythonRegex(pattern, options: options, policy: new Utf8RegexPolicy()
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = policy
                });
            using var cancellation = new CancellationTokenSource();
            Utf8PythonRangeMatch match = default;
            using (var captureCursor1 = regex.CreateMatchCursor(("😀A"u8).ToArray(), 0, cancellation.Token, mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor1.MoveNext(out match);
            }
            Assert.True(match.Success);
            Assert.Equal("😀A", System.Text.Encoding.UTF8.GetString((match.Groups[1].Success ? match.GetGroupMemory(match.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
            Assert.Equal((0, 5), (match.Groups[1].StartOffsetInBytes, match.Groups[1].EndOffsetInBytes));
            Assert.Equal(1, match.LastIndex);
            Assert.Equal(1, regex.Count("😀A"u8, 0, cancellation.Token));
        }
    }

    [Theory]
    [InlineData("a(?#note)+", "aaa")]
    [InlineData("a(?#note){2}", "aa")]
    [InlineData("(a)(?#note){2}", "aa")]
    [InlineData("😀(?#note){2}", "😀😀")]
    [InlineData(@"a(?#escaped\)closer)+", "aaa")]
    [InlineData("(?x)a#continued\\\nb", "a")]
    [InlineData("(?x)a#continued\\\n(?i)b", "a")]
    [InlineData("(?x) #continued\\\n(?i)a", "")]
    [InlineData("[()?#]+", "(?#)")]
    [InlineData(@"\(\?\#", "(?#")]
    public void CommentsAreTriviaRatherThanRepeatableAtoms(string pattern, string text)
    {
        Assert.True(new Utf8PythonRegex(pattern).Match(Encoding.UTF8.GetBytes(text), mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData("(?i) (?x)a", "global flags not at the start of the expression", 5)]
    [InlineData("(?x)\u00a0(?i)a", "global flags not at the start of the expression", 5)]
    [InlineData("a(?i)b", "global flags not at the start of the expression", 1)]
    [InlineData("(?:(?i)a)", "global flags not at the start of the expression", 3)]
    [InlineData("(?#note)*", "nothing to repeat", 8)]
    [InlineData("a*(?#note)?", "multiple repeat", 10)]
    [InlineData("(?#\\", "bad escape (end of pattern)", 3)]
    [InlineData("(?#a\\)", "missing ), unterminated comment", 0)]
    [InlineData("(?x)a#foo\\", "bad escape (end of pattern)", 9)]
    [InlineData("(?x)(?#bad", "missing ), unterminated comment", 4)]
    public void RealAtomsAndMalformedCommentsKeepPythonErrors(string pattern, string message, int position)
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Equal(message, error.Message);
        Assert.Equal(position, error.Position);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData("(?#", ")(?i)a")]
    [InlineData("(?x)#", "\n(?i)a")]
    public void LongIgnoredTriviaStillSpendsConstructionWork(string prefix, string suffix)
    {
        var pattern = prefix + new string('c', 4096) + suffix;
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 128, 128))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
        }
        Assert.True(new Utf8PythonRegex("(?i)a").Match("A"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CommentScansKeepTheOriginalConstructionInterruption(bool lineComment, bool expire)
    {
        var pattern = (lineComment ? "(?x)#" : "(?#") + new string('c', 4096) +
            (lineComment ? "\n(?i)a" : ")(?i)a");
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 100000, 128))
        {
            owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
            owner.Budget.CancellationToken = cancellation.Token;
            owner.Budget.CheckpointHook = steps =>
            {
                if (steps < 64) return;
                entered = true;
                if (expire) ticks = 50;
                else cancellation.Cancel();
            };
            if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                new PythonReParser(pattern).Parse(PythonReCompileOptions.None)).CancellationToken);
            Assert.True(entered);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
        }
        Assert.False(Utf8ConstructionScope.IsActive);
    }
}
