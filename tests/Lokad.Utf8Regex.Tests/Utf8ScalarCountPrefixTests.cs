using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCountPrefixTests
{
    [Theory]
    [InlineData("(ab|cd)+", "xxabcd yy cdcd ab xx")]
    [InlineData("(a|ab)+b", "aaaab abab aaac ab")]
    [InlineData("(ab|a)+b", "aabab ab aaac")]
    [InlineData("(ab|cd)+?", "abcdabcd cd")]
    [InlineData("abc[0-9]+", "abcX abc12 abc3 abc")]
    [InlineData("(abc)\\1", "abcX abcabc abc")]
    [InlineData("(?=abc)abc", "xxabc ab abc")]
    [InlineData("(?<=x)(abc|def)", "abc xabc xdef def")]
    [InlineData("^abc[0-9]+", "xxabc1\nabc2\nabcX\nabc3")]
    [InlineData("(ab|cd)*", "xxabcd yy")]
    [InlineData("(?i:ab|cd)+", "ABcd xx abCD")]
    [InlineData("\\G(ab|cd)+", "xxabcd ab")]
    [InlineData("a|\\Gb", "a xa")]
    [InlineData("a\\G|b", "b xb")]
    [InlineData("(?:a\\G)+|b", "b xb")]
    [InlineData("\\G|b", "b xb")]
    [InlineData("😀|\\Gb", "😀 x😀")]
    [InlineData("a\\G|😀", "😀 x😀")]
    public void PublicCountKeepsPrefixVerificationAndDeclines(string pattern, string subject)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.Multiline, RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var expected = new Regex(pattern, options).Count(subject);
            var unlimited = new Utf8Regex(pattern, options: options);
            var finite = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new()
                {
                    MaxDepth = 8192,
                    MaxWorkingBytes = 1048576,
                    MaxResultBytes = 1048576
                }
            });
            var storage = Encoding.UTF8.GetBytes("unused" + subject + "unused");
            var input = storage.AsSpan(6, Encoding.UTF8.GetByteCount(subject));
            Assert.Equal(expected, unlimited.Count(input));
            Assert.Equal(expected, finite.Count(input));
        }
    }

    [Fact]
    public void ExactPrefixCountKeepsWordBoundariesAndWiderRepeatEndpoints()
    {
        const string pattern = @"\bpublic\s+async\b";
        var subject = new string('x', 2044) + " public async | épublic async | public asyncx | public static | " +
            "public\u2003async | public" + new string(' ', 16) + "async | public";
        var input = Encoding.UTF8.GetBytes(subject);
        const RegexOptions options = RegexOptions.CultureInvariant;
        Assert.Equal(3, new Regex(pattern, options).Count(subject));
        using var ordinary = new Utf8Regex(pattern, options);
        using var funded = new Utf8Regex(pattern, options, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 128, MaxWorkingBytes = 1 << 16 }
        });
        using var source = new CancellationTokenSource();
        Assert.Equal(3, ordinary.Count(input));
        Assert.Equal(3, funded.Count(input, source.Token));
    }

    [Fact]
    public void ExactFamilyCountAdmitsEmptyRepeatsBeforeDisjointFollowers()
    {
        const string pattern = @"\b(?:LogError|LogWarning|LogInformation)\s*\(";
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        var text = new string(' ', 2044) + "LogError( | LogInformation \t( | LogError\u2003( | LogWarning" +
            new string(' ', 16) + "( | éLogWarning( | XLogError( | LogInformation[ | LogError";
        var storage = Encoding.UTF8.GetBytes("unused" + text + "(unused");
        var input = storage.AsSpan(6, Encoding.UTF8.GetByteCount(text));
        using var ordinary = new Utf8Regex(pattern, options);
        using var finite = new Utf8Regex(pattern, options, new()
        {
            ResourcePolicy = new() { MaxDepth = 128, MaxWorkingBytes = 1 << 16 }
        });
        using var source = new CancellationTokenSource();

        Assert.Equal(4, new Regex(pattern, options).Count(text));
        Assert.Equal(4, ordinary.Count(input));
        Assert.Equal(4, finite.Count(input, source.Token));
    }

    [Theory]
    [InlineData("*", 4)]
    [InlineData("+", 2)]
    public void TerminalAsciiWordRepeatStopsAtWideScalarsAndHonorsItsMinimum(string quantifier, int expectedCount)
    {
        var pattern = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]" + quantifier;
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        var first = "class A" + new string('x', 4096);
        var bytes = Encoding.UTF8.GetBytes(first + "é class Z\u3000 record C123😀 struct Q");
        using var ordinary = new Utf8Regex(pattern, options);
        using var finite = new Utf8Regex(pattern, options, new()
        {
            ResourcePolicy = new() { MaxDepth = 128, MaxWorkingBytes = 1 << 16 }
        });
        using var source = new CancellationTokenSource();
        Assert.Equal(expectedCount, ordinary.Count(bytes));
        Assert.Equal(expectedCount, finite.Count(bytes, source.Token));
        Assert.Equal(new Utf8CaptureRange(0, first.Length), finite.Match(bytes, cancellationToken: source.Token));
    }

    [Fact]
    public void MultilineLiteralPrefixKeepsAnchorsAndCaptureReads()
    {
        const string pattern = @"^import\s+(?<path>[^=\r\n]+)=\k<path>\r?$";
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.Multiline;
        // The second match's LF-plus-prefix crosses the first charged scan block.
        var text = "import x=x\n" + new string(' ', 2044) + "\n" +
            "import\u2003α=α\r\nimports x=x\nimportx y=y\nitem q=q\nimport y=z\nimport\talpha=alpha";
        var storage = Encoding.UTF8.GetBytes("unused" + text + "unused");
        var input = storage.AsSpan(6, storage.Length - 12);
        using var ordinary = new Utf8Regex(pattern, options);
        using var finite = new Utf8Regex(pattern, options, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 128, MaxWorkingBytes = 1 << 16 }
        });
        using var source = new CancellationTokenSource();

        Assert.Equal(3, ordinary.Count(input));
        Assert.Equal(3, finite.Count(input, source.Token));
    }

    [Fact]
    public void ExactFamilyFollowerSkipsDecoysWithinFiniteWorkAllowance()
    {
        const string pattern = @"\b(?:public|private|internal)\s+class\b";
        var decoys = "// épublic class | " + string.Concat(Enumerable.Repeat("public static | internal record | ", 128)) +
            string.Concat(Enumerable.Repeat("sealed class | ", 8));
        using var regex = new Utf8Regex(pattern, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxDepth = 4096 }
        });
        using var source = new CancellationTokenSource();

        var miss = Encoding.UTF8.GetBytes(decoys);
        var hit = Encoding.UTF8.GetBytes(decoys + "private class");
        Assert.Equal(0, regex.Count(miss, source.Token));
        Assert.Equal(1, regex.Count(hit, source.Token));
        Assert.False(regex.IsMatch(miss, source.Token));
        Assert.True(regex.IsMatch(hit, source.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RtlUnanchoredAlternativeRetainsScalarCapturesAndValidation(bool compiled)
    {
        var options = RegexOptions.RightToLeft | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex("a\\G|(?<x>😀)", options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxDepth = 128,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            }
        });
        var storage = "prefix😀 x😀tail"u8.ToArray();
        var input = storage.AsSpan(6, 10);
        using var source = new CancellationTokenSource();
        var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(((global::System.ReadOnlySpan<byte>)input).ToArray(), cancellationToken: source.Token));
        Assert.True(retained.Success);
        Assert.Equal(6, retained.Value.StartOffsetInBytes);
        Assert.Equal(4, (retained.Value.EndOffsetInBytes - retained.Value.StartOffsetInBytes));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
        Assert.Equal(2, regex.Count(input, source.Token));
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count("😀 x😀"u8, source.Token)).CancellationToken);
        Assert.Equal(1, regex.Count("😀"u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MandatoryInitialRtlContiguousAnchorKeepsBoundedStop(bool compiled)
    {
        var regex = new Utf8Regex("a\\G", options: RegexOptions.RightToLeft | (compiled ? RegexOptions.Compiled : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes(new string('x', 32768));
        var invocation = new Utf8SliceInvocation(new(64, 8192, 1), Timeout.InfiniteTimeSpan, default);
        var result = Utf8SliceRunner.CountFromBytes(program, input, input.Length,
            new int[program.GroupCount], new int[program.GroupCount], ref invocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(0, result.Count);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RtlGapUsesOriginalCancellationAndDeadline(bool expire)
    {
        const string pattern = "a\\G|b";
        var input = Encoding.UTF8.GetBytes("b" + new string('x', 32768) + "b");
        var regex = new Utf8Regex(pattern, options: RegexOptions.RightToLeft, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new()
            {
                MaxDepth = 128,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            }
        });
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= 64) return;
            entered = true;
            if (expire) ticks = 50;
            else source.Cancel();
        });
        try
        {
            if (expire)
                Assert.Equal(pattern, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input)).Pattern);
            else
                Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
        ticks = 0;
        Assert.Equal(2, regex.Count(input));
        var limited = new Utf8Regex(pattern, options: RegexOptions.RightToLeft, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxDepth = 128,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            }
        });
    }

    [Theory]
    [InlineData("(😀|ab)+", "é😀ab xx 😀😀", 2)]
    [InlineData("(é|😀)+?", "é😀 xx 😀", 3)]
    [InlineData("😀x+", "é😀xxx 😀x 😀", 2)]
    public void UnicodePrefixesKeepWholeScalarCoordinates(string pattern, string subject, int expected)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(subject)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongCandidateMissPollsOriginalInvocationAndReleasesScratch(bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var regex = new Utf8Regex("(ab|cd)+", options: RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var input = Encoding.UTF8.GetBytes(new string('x', 200000));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited,
            expire ? TimeSpan.FromMilliseconds(200) : Timeout.InfiniteTimeSpan, source.Token);
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) clock.Advance(250); else source.Cancel(); };
        Assert.True(invocation.Ledger.TryReserve(13));
        void Run() =>
            Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount], new int[program.GroupCount], ref invocation, regex.Inspection.SearchPlan);
        if (expire) Assert.Throws<Utf8ExecutionDeadlineExpiredException>(Run);
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
        Assert.True(entered);
        Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        Assert.Equal(0, regex.Count(input));
    }
}
