using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OptionalRepeatTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in new[]
        {
            @"a?a", @"a??a", @"[ab]?[ab]", @"[ab]??[ab]", @"(ab)?b", @"(ab)??b",
            @"(a?)?a", @"(a?)??a", @"((a)?)*b", @"((a)??)+b",
            @"(?<x>a)?(?(x)b|c)", @"(?<x>a)??(?(x)b|c)", @"(?<x>a)?\k<x>b",
            @"(?:(?<x>a)?b|(?<x>ab)?c)", @"(?=(a)?b)a?b", @"(?<=(a)?b)c",
            @"(?>(ab)?a)b", @"(?>(ab)??a)b", @"(?<x>a)(?<-x>b)?(?(x)c|d)",
            @"(?<x>a)(?<-x>b)??(?(x)c|d)", @"((é)?){0,1}b", @"((é)??){0,1}?b",
            @"(?i:(ab)?)b", @"(?:^)?a", @"(?:$)??", @"()?(a)?", @"()??(a)??"
        })
        foreach (var rtl in new[] { false, true })
        foreach (var compiled in new[] { false, true })
            yield return [pattern, RegexOptions.CultureInvariant |
                (rtl ? RegexOptions.RightToLeft : 0) | (compiled ? RegexOptions.Compiled : 0)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void BranchOrderCapturesAndRollbackKeepOverlappingSemantics(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "a", "b", "c", "ab", "aab", "abb", "abc", "ac", "ad", "ébéb", "baabc aab"], options);

    [Theory]
    [InlineData(@"\A[a-z]{1,2}[AZ]?[AZ]\z", "xBZ", false)]
    [InlineData(@"\A[a-z]{1,2}[AZ]?[AZ]\z", "xZ", true)]
    [InlineData(@"\A[a-z]{1,2}[AZ]??[AZ]\z", "xAZ", true)]
    [InlineData(@"\A[a-z]{1,2}[AZ]?é\z", "xé", true)]
    public void ShortAnchoredBooleanProofKeepsOptionalMaskHolesAndEndpoints(string pattern, string text, bool expected)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input));
        using var finite = new Utf8Regex(pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128, MaxWorkingBytes = 4096 }
        });
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(expected, finite.IsMatch(input, cancellation.Token));
    }

    [Theory]
    [InlineData(@"\A(a?)a\z", "a", "", 1)]
    [InlineData(@"\A(\n??)$", "\n", "", 0)]
    [InlineData(@"\A(\n??)\z", "\n", "\n", 1)]
    [InlineData(@"\A(-?)\d*(\.?)\d*$", "-١.٢\n", "-", 6)]
    public void TerminalOptionalKeepsCaptureAndEndPriority(string pattern, string text, string captured, int end)
    {
        using var regex = Utf8Regex.Compile(pattern);
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(text));
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(end, match.Value.EndOffsetInBytes);
        Assert.Equal(captured, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData("///doc")]
    [InlineData("\u2003[|\t/// doc😀\n")]
    public void DisjointAsciiContinuationsPrepareUnicodeWhitespaceRepeats(string text)
    {
        const string pattern = @"^\s*(\[\|\s*)?///\s?(?<doc>.*)$";
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        using var regex = new Utf8Regex(pattern, options, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128, MaxWorkingBytes = 1 << 14 }
        });
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(2, program.Instructions.Count(instruction =>
            instruction.Kind == Utf8SliceInstructionKind.RepeatExit && instruction.RepeatExitRejectsShorterEndpoints));
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern, options).Match(text);
        using var source = new CancellationTokenSource();
        Utf8MatchSequence.AssertRange(regex.Match(input, cancellationToken: source.Token), text, expected.Index, expected.Length);
        Utf8MatchSequence.AssertCaptures(expected,
            Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: source.Token)), input, text);
    }

    [Theory]
    [InlineData(@"^\s*(\u2003)/$", " \u2003/")]
    [InlineData(@"^\s*(/|\u2003)/$", " \u2003/")]
    [InlineData(@"^\s*(\n)$", " \n")]
    public void OverlappingContinuationCanStillRewindUnicodeWhitespace(string pattern, string text)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        Assert.DoesNotContain(program.Instructions, instruction =>
            instruction.Kind == Utf8SliceInstructionKind.RepeatExit && instruction.RepeatExitRejectsShorterEndpoints);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern, RegexOptions.CultureInvariant).Match(text);
        Assert.True(expected.Success);
        Utf8MatchSequence.AssertCaptures(expected, Utf8MatchSequence.First(regex.CreateMatchCursor(input)), input, text);
    }

    [Theory]
    [InlineData(@"\A(?<unit>\d)+\d\z", "1١3")]
    [InlineData(@"\A(?<unit>\d)+\k<unit>\z", "1١١")]
    [InlineData(@"\A(?<unit>[a-z]){2,4}b\z", "Abbb")]
    [InlineData(@"\A([a-z][a-z0-9_]*)$", "AK_19\n")]
    [InlineData(@"(?m)\A([a-z\n]*)$", "A\nbc\n!")]
    [InlineData(@"\A(?<left>[a-f0-9]{2})(?<right>[a-f0-9]{2})$", "AbF0\n")]
    [InlineData(@"\A#?([a-f0-9]{6}|[a-f0-9]{3})$", "#A0F\n")]
    [InlineData(@"\A#?([#a]{2}|[b]{3})$", "#a")]
    [InlineData(@"^///\s?(?<doc>.*)$", "///\ndoc😀")]
    public void ScalarRepeatPreservesEndpointsAndObservedCaptures(string pattern, string text)
    {
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        using var regex = new Utf8Regex(pattern, options, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 1_000, MaxDepth = 64, MaxWorkingBytes = 1 << 16 }
        });
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern, options).Match(text);
        Assert.True(expected.Success);
        using var source = new CancellationTokenSource();
        Utf8MatchSequence.AssertRange(regex.Match(input, cancellationToken: source.Token), text, expected.Index, expected.Length);
        Assert.Equal(1, regex.Count(input, cancellationToken: source.Token));
        Utf8MatchSequence.AssertCaptures(expected,
            Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: source.Token)), input, text);
    }

    [Fact]
    public void UnobservedScalarRepeatCanRewindWithinOneFrame()
    {
        using var regex = new Utf8Regex(@"\A(?<unit>\d)+\d\z", policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 2_000, MaxDepth = 1, MaxWorkingBytes = 1 << 10 }
        });
        var input = Encoding.UTF8.GetBytes(new string('١', 128) + "5");
        using var source = new CancellationTokenSource();
        var match = regex.Match(input, cancellationToken: source.Token);
        Assert.Equal(new Utf8CaptureRange(0, input.Length), match);
    }

    [Theory]
    [InlineData("http/x")]
    [InlineData("😀Http/x é ~bad")]
    [InlineData("😀http/x é ~/x then")]
    [InlineData("😀http/x é ~/x", 3073)]
    [InlineData("😀http/x \n~/x")]
    public void GreedyDotRewindKeepsStarterOrderAndCaptures(string text, int trailingScalars = 0)
    {
        const string pattern = @"\A(?<before>.*)(?<marker>(?:[hH]ttp|~)/x)(?<rest>.*)\z";
        const RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
        text += new string('é', trailingScalars);
        using var regex = new Utf8Regex(pattern, options, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 128, MaxDepth = 16, MaxWorkingBytes = 1 << 14 }
        });
        var exit = Assert.Single(regex.Inspection.AvailableOwnedSliceProgram!.Instructions.Where(instruction =>
            instruction.Kind == Utf8SliceInstructionKind.RepeatExit && instruction.RepeatExitRewindStarts != 0));
        Assert.Equal(1 << 24 | 'H' << 8 | 'h' << 16 | '~', exit.RepeatExitRewindStarts);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern, options).Match(text);
        using var token = new CancellationTokenSource();
        var actual = regex.Match(input, cancellationToken: token.Token);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
            Utf8MatchSequence.AssertRange(actual, text, expected.Index, expected.Length);
        Utf8MatchSequence.AssertCaptures(expected,
            Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: token.Token)), input, text);
    }

    [Theory]
    [InlineData(@"\A(.*)(x|é)\z", "😀é")]
    [InlineData(@"\A(.*)(x|(?=é))é\z", "😀é")]
    public void WiderAndNullableAlternativesKeepScalarRewind(string pattern, string text)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.DoesNotContain(regex.Inspection.AvailableOwnedSliceProgram!.Instructions, instruction =>
            instruction.Kind == Utf8SliceInstructionKind.RepeatExit && instruction.RepeatExitRewindStarts != 0);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern, RegexOptions.CultureInvariant).Match(text);
        Assert.True(expected.Success);
        Utf8MatchSequence.AssertCaptures(expected, Utf8MatchSequence.First(regex.CreateMatchCursor(input)), input, text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScalarOptionalCapturesRetainByteBoundariesAndBorrowedResults(bool lazy)
    {
        var regex = new Utf8Regex("(?<value>😀)?" + (lazy ? "?" : "") + "X");
        var input = Encoding.UTF8.GetBytes("é😀X X中");
        var cursor = regex.CreateMatchCursor(input.AsMemory(2, input.Length - 5));
        using (cursor)
        {
            Assert.True(cursor.MoveNext(out var retained));
            Assert.Equal(0, retained.GetCaptures(0)[0].StartOffsetInBytes);
            Assert.Equal(5, retained.GetCaptures(0)[0].EndOffsetInBytes);
            var valueNumber = regex.GroupNumberFromUtf8Name("value"u8);
            Assert.Equal("😀", Encoding.UTF8.GetString(retained.GetGroupMemory(valueNumber).Span));
            Assert.True(cursor.MoveNext(out var second));
            Assert.Equal("X", Encoding.UTF8.GetString(second.GetValueMemory().Span));
            Assert.True(second.TryGetGroup(valueNumber, out var absent));
            Assert.False(absent.Success);
            Assert.False(cursor.MoveNext(out _));
            Assert.Equal("😀X", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryWorkCutoffKeepsParentLoanAndNoPartialCount(bool lazy)
    {
        var regex = new Utf8Regex("(?<v>(?:é|😀)?" + (lazy ? "?" : "") + ")?X");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.UTF8.GetBytes("éX 😀X X");
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var measured = new Utf8SliceInvocation(new(100000, 64, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(measured.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref measured);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(3, result.Count);
        Assert.Equal(13ul, measured.Ledger.ReservedBytes);
        for (ulong limit = 1; limit < measured.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 64, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps,
                Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation).Breach);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
            var publicRegex = new Utf8Regex(regex.Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = limit,
                    MaxDepth = 64,
                    MaxWorkingBytes = 1 << 20
                }
            });
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => publicRegex.Count(input)).ResourceKind);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionUnwindsOptionalAndOuterRepeat(bool timeout)
    {
        var regex = new Utf8Regex("(?:(?<v>a)?b)+");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("ab", 4096)));
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (timeout) ticks = 25; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(1000000, 65536, 1 << 24), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var error = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0,
                new int[program.GroupCount], new int[program.GroupCount], ref invocation));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(error);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void SingleAtomOptionalKeepsItsConstantSpaceTerminalPath()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1 };
        foreach (var pattern in new[] { "a?", @"\w?", "[é😀]?" })
        {
            var regex = new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = policy
            });
            Assert.Equal(3, regex.Count("a😀"u8));
            using var source = new CancellationTokenSource();
            Assert.Equal(3, regex.Count("a😀"u8, source.Token));
        }
    }

    [Theory]
    [InlineData(@"\A[ab]?[ab]\z", "a")]
    [InlineData(@"\A[ab]??[ab]\z", "aa")]
    public void SingleAtomOptionalCanRetryWithinOneBranchReservation(string pattern, string subject)
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 10_000, MaxDepth = 1, MaxWorkingBytes = 768 };
        using var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant,
            new Utf8RegexPolicy { ResourcePolicy = policy });
        using var token = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(input.Length, regex.Match(input, cancellationToken: token.Token).EndOffsetInBytes);
    }

    [Theory]
    [InlineData(false, "012.")]
    [InlineData(false, "01.")]
    [InlineData(true, "012.")]
    [InlineData(true, "01.")]
    public void RequiredAsciiSuffixDisprovesOneOptionalEndpoint(bool lazy, string subject)
    {
        var pattern = @"\A[01]?" + (lazy ? "?" : "") + @"[0-9][0-9]\.";
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 100, MaxWorkingBytes = 1 }
        });
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.True(Regex.IsMatch(subject, pattern));
        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(1, regex.Count(input, token.Token));
        Assert.Equal(input.Length, regex.Match(input, cancellationToken: token.Token).EndOffsetInBytes);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    public void TerminalAsciiSuffixPreservesOptionalMatchLengthWithoutRewind(bool lazy, int end)
    {
        var pattern = @"\A[01]?" + (lazy ? "?" : "") + "[0-9][0-9]";
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(pattern, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 100, MaxWorkingBytes = 1 }
        });
        Assert.Equal(end, regex.Match("012"u8, cancellationToken: token.Token).EndOffsetInBytes);
    }

    [Theory]
    [InlineData(false, "012X")]
    [InlineData(true, "012X")]
    public void ViableOptionalEndpointsRetainCapturesAndAssertionRetry(bool lazy, string subject)
    {
        var pattern = @"\A(?<opt>[01]?" + (lazy ? "?" : "") + @")[0-9][0-9](?=X)X\z";
        var expected = Regex.Match(subject, pattern);
        Assert.True(expected.Success);
        using var regex = Utf8Regex.Compile(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, subject);
    }

    [Theory]
    [InlineData(@"(?<body>[é😀]?)[é😀]", "😀")]
    [InlineData(@"(?<body>😀??)[é😀]", "")]
    public void OptionalScalarBranchKeepsCapturePriorityAndByteBoundaries(string pattern, string body)
    {
        using var regex = new Utf8Regex(pattern);
        using var token = new CancellationTokenSource();
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor("😀é"u8.ToArray(), cancellationToken: token.Token));
        Assert.True(match.Success);
        Assert.Equal(body, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal(body.Length == 0 ? 4 : 6, match.Value.EndOffsetInBytes);
    }

    [Fact]
    public void LazySubtractionDefersItsWorkUntilTheAtomIsNeeded()
    {
        var atom = "[ab]";
        for (var depth = 0; depth < 64; depth++) atom = $"[ab-{atom}]";
        using var regex = Utf8Regex.Compile(@"\A" + atom + "??[ab]", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy { MaxWorkSteps = 48 }
        });
        Assert.True(regex.IsMatch("a"u8));
    }
}
