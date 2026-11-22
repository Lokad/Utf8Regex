using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8LeadingAsciiCandidateTests
{
    [Theory]
    [InlineData("a_b@northwind-control.net\n", true)]
    [InlineData("a_b@northwind-control.eu.int", true)]
    [InlineData("a_b@northwind-control.n", false)]
    public void AnchoredClassRunsProveBothOutcomesUnderAFiniteWorkLimit(string text, bool expected)
    {
        const string pattern = @"^([a-z_]+)@([a-z-]+\.)+[a-z]{2,12}$";
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 4, MaxDepth = 1, MaxWorkingBytes = 1 }
        });
        using var source = new CancellationTokenSource();
        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(text), source.Token));
    }

    [Fact]
    public void AnchoredCounterlessLoopSpendsTheSharedProofAllowance()
    {
        using var regex = new Utf8Regex(@"\A([a-z_]+)@(?:q[ab]|[xy][cz])+!\z", RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 4 }
        });
        using var source = new CancellationTokenSource();
        Assert.True(regex.IsMatch("a_b@qaxc!"u8, source.Token));
        Assert.False(regex.IsMatch("a_b@qaxb!"u8, source.Token));
        var longer = Encoding.UTF8.GetBytes("a_b@" + string.Concat(Enumerable.Repeat("qa", 24)) + "!");
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(longer, source.Token));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
    }

    [Theory]
    [InlineData(0)] [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void LongerStraightPrefixMissAvoidsUnneededVmStorage(int padding)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', padding) + "é😀# noX # nOQ # n");
        byte[] storage = [0xff, .. input, 0xff];
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(StraightPrefixPattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded with
                {
                    MaxWorkingBytes = 1,
                    MaxResultBytes = 1
                }
            });
            Assert.Equal(0, regex.Count(storage.AsSpan(1, input.Length), cancellation.Token));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(StraightPrefixPattern, options:                     options, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = Regex.InfiniteMatchTimeout,
                    ResourcePolicy = Funded with
                    {
                        MaxWorkSteps = 1
                    }
                }).Count(input)).ResourceKind);
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count("# noqa: A123"u8)).ResourceKind);
        }
    }

    [Theory]
    [InlineData(@"(?<head># [Nn][Oo][Qq][Aa])(?::\s?([A-Z]+[0-9]+))?")]
    [InlineData(@"# [Nn][Oo][Qq][Aa](?<n>[0-9]+)\k<n>")]
    [InlineData(@"# (?:noqa|notes)")]
    [InlineData(@"# n?oqa")]
    [InlineData(@"# n{1,2}oqa")]
    [InlineData(@"# (?=noqa)noqa")]
    [InlineData(@"# (?i:noqa)")]
    [InlineData(@"# (?i:k)elvin")]
    [InlineData(@"# ^noqa")]
    public void StraightPrefixFilterKeepsCaptureAndControlFlowSemantics(string pattern)
    {
        const string text = "é😀# noX # n # noqa: A123 # nOQa1212 # notes # oqa # nnoqa # Kelvin\n# noqa";
        var input = Encoding.UTF8.GetBytes(text);
        byte[] storage = [0xff, .. input, 0xff];
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
        {
            var oracle = new Regex(pattern, options | RegexOptions.CultureInvariant);
            var regex = new Utf8Regex(pattern, options: options | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded
            });
            var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            using var cancellation = new CancellationTokenSource();
            foreach (var start in new[] { 0, 3, 4, text.Length })
                Assert.Equal(oracle.Matches(text, start).Count,
                    Utf8MatchSequence.Count(regex.EnumerateMatches(storage.AsSpan(1, input.Length), Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), cancellation.Token)));
            Assert.Equal(oracle.Count(text), regex.Count(storage.AsSpan(1, input.Length), cancellation.Token));
            Assert.Equal(oracle.Match(text).Value, System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        }
        var nonBacktracking = new Utf8Regex(StraightPrefixPattern, options: RegexOptions.NonBacktracking, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        Assert.Equal(new Regex(StraightPrefixPattern, RegexOptions.NonBacktracking).Count(text),
            nonBacktracking.Count(input));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LongerFalsePrefixesKeepOriginalCancellationAndDeadline(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("# noX ", 20000)) + "# noqa");
        const ulong threshold = 64;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(StraightPrefixPattern, options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        Exception? failure = null;
        try { regex.Count(input, cancellation.Token); }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }

    [Theory]
    [InlineData("^import\\s+\"[^\"]*\"", "é😀 import \"hidden\"\nimport \"α😀\"\nx import \"skip\"\nimport\u2003\"a\nb\"")]
    [InlineData(@"\A[A-Z][a-z]+", "é😀Ab\nCd")]
    [InlineData(@"^([A-Z][a-z]+)", "Aa\nxx Bb\nCc\n😀Dd")]
    [InlineData(@"(?:^)(?<w>[A-Z])[a-z]", "Aa\nBb\nx Cc")]
    [InlineData(@"\G[A-Z][a-z]", "AaBb!Cc")]
    [InlineData(@"^a^b", "ab\na\nb\na^b")]
    [InlineData(@"\zX", "X\nX")]
    [InlineData(@"\n^b", "a\nb\nb")]
    [InlineData(@"\n\Ab", "\nb")]
    [InlineData(@"^(?:[A-Z][a-z]){1,2}$", "Aa\nAaBb\nAaBbCc\néAa\n")]
    public void AnchoredPrefixesKeepFullContextAndZeroWidthChecks(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.Multiline, RegexOptions.Multiline | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options | RegexOptions.CultureInvariant);
            var regex = new Utf8Regex(pattern, options: options | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded
            });
            var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            using var live = new CancellationTokenSource();
            foreach (var start in new[] { 0, 1, Math.Min(2, text.Length), text.Length })
            {
                if (start > 0 && start < text.Length && char.IsSurrogatePair(text, start - 1))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, start - 1)) + 1)));
                    Assert.Throws<ArgumentOutOfRangeException>(() => Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, start - 1)) + 1, live.Token)));
                    continue;
                }
                var expected = oracle.Matches(text, start).Count;
                Assert.Equal(expected, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, start)))));
                Assert.Equal(expected, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), live.Token)));
            }
            Assert.Equal(oracle.Count(text), regex.Count(input, live.Token));
            Assert.Equal(oracle.Match(text).Value, System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        }
    }

    [Theory]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,4}")]
    [InlineData(@"(?:(?<w>[A-Z][a-z]+)\s*){2,4}")]
    [InlineData(@"(?:(?:[A-Z][a-z]+){1,2}\s*){2,4}")]
    [InlineData(@"(?:A[0-9]+\s*){2,4}")]
    [InlineData(@"[A-Z]+(?<last>[a-z])")]
    [InlineData(@"(?<w>[A-Z]+)[a-z]\k<w>")]
    [InlineData(@"[A-CX-Z]+[a-z]")]
    [InlineData(@"[^A-Z]+[a-z]")]
    [InlineData(@"[\p{Lu}]+[a-z]")]
    [InlineData(@"[A-Z]*[a-z]")]
    [InlineData(@"(?:[A-Z]?a){1,4}")]
    [InlineData(@"(?<=x)[A-Z]+a")]
    [InlineData(@"[A-Z][a-z]")]
    [InlineData(@"(?:[A-Z][a-z]?){2,4}")]
    [InlineData(@"(?:[A-Z]{1,2}[a-z]){2,4}")]
    [InlineData(@"(?:[A-Z](?:[a-z]|[0-9])){2,4}")]
    [InlineData(@"(?<x>[A-Z])(?<y>[a-z]+)\k<y>")]
    [InlineData(@"(?:(?i:[A-Z])[a-z]+){2,4}")]
    public void ByteStartEnumerationPreservesScalarOracleCapturesAndFloors(string pattern)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded
            });
            var oracle = Utf8ScalarCaptureFixture.Project(new Regex(pattern, options));
            foreach (var tail in new[] { "", "Ab", "Ab Cd", "Ab Cd Ef Gh Ij Kl", "Ab\u2003Cd", "Éa Βa",
                "ABaB", "xAa", "A1 A2 A3", "aab", "DAa XBza", "Aa😀Bb Cc\u00a0Dd" })
            {
                var text = "é😀x" + tail + "😀é";
                var input = Encoding.UTF8.GetBytes(text);
                var projected = new Utf8ScalarCaptureFixture.Subject(text);
                byte[] storage = [0xff, .. input, 0xff];
                using var live = new CancellationTokenSource();
                foreach (var start in new[] { 0, 1, 3, 4, text.Length })
                {
                    var expected = oracle.Count(projected.Projected, Array.IndexOf(projected.Units, start));
                    Assert.Equal(expected, Utf8MatchSequence.Count(regex.EnumerateMatches(storage.AsSpan(1, input.Length), Encoding.UTF8.GetByteCount(text.AsSpan(0, start)))));
                    Assert.Equal(expected, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), live.Token)));
                }
            }
        }
        var captured = new Utf8Regex(@"(?<w>[A-Z]+)[a-z]\k<w>", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        Assert.Equal(1, captured.Count("éABaB😀"u8));
        Assert.Equal("B", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(captured.CreateMatchCursor("éABaB😀"u8.ToArray())).GetGroupMemory(captured.GroupNumberFromUtf8Name("w"u8)).Span));
    }

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4095)]
    public void WindowTailsAndUnicodeSeparatorsRemainScalar(int prefixLength)
    {
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){2,4}", options: RegexOptions.Compiled, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        var input = Encoding.UTF8.GetBytes(new string('x', prefixLength) + "😀Ab\u2003Cd😀Ef Gh");
        using var live = new CancellationTokenSource();
        Assert.Equal(2, regex.Count(input));
        Assert.Equal(2, regex.Count(input, live.Token));
        Assert.Equal(1, regex.Count("Ab\u2003Cd"u8));
        Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes("😀" + new string('x', prefixLength - 4) + "Ab Cd"), live.Token));
    }

    [Fact]
    public void RejectedSecondScalarsAvoidVmStorageButStillConsumeBoundedWork()
    {
        const string pattern = @"(?:[A-Z][a-z]+\s*){2,4}";
        var input = Encoding.UTF8.GetBytes(new string('A', (1 << 20)) + "😀A");
        Assert.Equal(0, new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with
            {
                MaxWorkingBytes = 1
            }
        }).Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(pattern, options:                 RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded with
                {
                    MaxWorkSteps = 4096
                }
            }).Count(input)).ResourceKind);
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    [InlineData(RegexOptions.IgnoreCase)]
    [InlineData(RegexOptions.ECMAScript)]
    public void OtherOptionsKeepTheirOwnScalarExecution(RegexOptions options)
    {
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){2,4}", options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        var input = "Ab\u2003Cd😀ab cd Ef Gh Ij Kl"u8;
        var cursor = regex.EnumerateMatches(input);
        var expected = 0;
        while (cursor.MoveNext()) expected++;
        Assert.Equal(expected, regex.Count(input));
    }

    [Fact]
    public void SkippedStartsAdmitTheStorageActuallyUsed()
    {
        const string pattern = @"(?:[A-Z][a-z]+\s*){2,4}";
        var input = Encoding.UTF8.GetBytes(new string('x', (1 << 20)) + "😀Ab Cd");
        Assert.Equal(1, new Utf8Regex(pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with
            {
                MaxWorkSteps = 4096
            }
        }).Count(input));
        // The external complete-repeat plan is retired. Both root forms now
        // admit the scalar verifier storage after the independent start filter.
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(pattern, options:                 RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded with
                {
                    MaxWorkingBytes = 1
                }
            }).Count(input)).ResourceKind);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex("(?<x>" + pattern + ")", options:                 RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded with
                {
                    MaxWorkingBytes = 1
                }
            }).Count(input)).ResourceKind);
        var anchored = new Utf8Regex("(?<x>^" + pattern + ")", options: RegexOptions.Multiline, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded with
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(0, anchored.Count(Encoding.UTF8.GetBytes(new string('x', 8192) + "😀")));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => anchored.Count("xx\nAb Cd"u8)).ResourceKind);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex(pattern, options:                 RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Funded with
                {
                    MaxWorkSteps = 1
                }
            }).Count(input)).ResourceKind);
    }

    [Theory]
    [InlineData(@"(?<w>[A-Z]+)[a-z]\k<w>")]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,4}")]
    [InlineData(@"(?m)^(?<w>[A-Z])[a-z]+")]
    public void EveryWorkCutoffPreservesTheOriginalScratchLoan(string pattern)
    {
        var regex = new Utf8Regex(pattern);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = "éABaB😀\nAb CdA"u8.ToArray();
        var complete = new Utf8SliceInvocation(new(100000, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(complete.Ledger.TryReserve(13));
        var outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
            new int[program.GroupCount], ref complete);
        Assert.Equal(Utf8ExecutionLimitKind.None, outcome.Breach);
        Assert.Equal(1, outcome.Count);
        Assert.Equal(13UL, complete.Ledger.ReservedBytes);
        for (ulong cap = 1; cap <= complete.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            outcome = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[program.GroupCount],
                new int[program.GroupCount], ref invocation);
            Assert.Equal(cap < complete.Budget.Steps ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, outcome.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void BoundedRangeSearchKeepsOriginalCancellationAndDeadline(bool expire, bool firstOnly, bool anchored)
    {
        var input = Encoding.UTF8.GetBytes(new string(firstOnly ? 'A' : 'x', (1 << 20)) + (anchored ? "\nAb Cd😀" : "😀Ab Cd"));
        const ulong threshold = 64;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Regex(anchored ? @"^(?:[A-Z][a-z]+\s*){2,4}" : @"(?:[A-Z][a-z]+\s*){2,4}", options:             RegexOptions.Compiled | (anchored ? RegexOptions.Multiline : RegexOptions.None), policy: new Utf8RegexPolicy
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout,
            ResourcePolicy = Funded
        });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        Exception? failure = null;
        try { regex.Count(input, cancellation.Token); }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }

    [Fact]
    public void FinalRangeMissCannotPublishAfterTheLastDeadlinePollCancelsTheToken()
    {
        var reads = 0;
        var cancelAt = int.MaxValue;
        using var cancellation = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == cancelAt) cancellation.Cancel();
            return 0;
        });
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){2,4}", options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1),
            ResourcePolicy = Funded
        });
        var input = Encoding.UTF8.GetBytes("😀Ab Cd" + new string('!', 8192));
        Assert.Equal(1, regex.Count(input, cancellation.Token));
        Assert.True(reads > 1);
        cancelAt = reads;
        reads = 0;
        Assert.Equal(cancellation.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token)).CancellationToken);
        Assert.Equal(cancelAt, reads);
    }

    [Theory]
    [InlineData(@"(?<part>25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.[0-9]{2}")]
    [InlineData(@"(?<part>25[0-5]|2[0-4][0-9]|[01]??[0-9][0-9])\.[0-9]{2}")]
    [InlineData(@"(?<part>25[0-5]|É[A-Z]|[01]?[0-9][0-9])\.[0-9]{2}")]
    [InlineData(@"(?<part>[01]?[0-9][0-9]|)\.[0-9]{2}")]
    [InlineData(@"(?<part>25[0-5]\.|[01]?[0-9][0-9]X)[0-9]{2}")]
    public void BranchStartsKeepAlternationPriorityAndByteCaptures(string pattern)
    {
        var prefix = new string('!', 256) + "00.99 é😀 ";
        var text = prefix + "256.78 234.34 ÉB.12 125.56 .72 17X92 17.92";
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern).Matches(text, prefix.Length);
        using var regex = Utf8Regex.Compile(pattern, policy: new() { ResourcePolicy = Funded });
        var from = Encoding.UTF8.GetByteCount(prefix);
        using var live = new CancellationTokenSource();
        using var cursor = regex.CreateMatchCursor(input, from, live.Token);
        foreach (Match expected in oracle)
        {
            Assert.True(cursor.MoveNext(out var match));
            Assert.Equal(expected.Value, Encoding.UTF8.GetString(match.GetValueMemory().Span));
            Assert.Equal(expected.Groups["part"].Value, Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), match.Value.StartOffsetInBytes);
        }
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(new Regex(pattern).Count(text), regex.Count(input, live.Token));
    }

    [Theory]
    [InlineData(false, 512u)]
    [InlineData(true, 6000u)]
    public void BranchedStartSkipsRejectedPrefixesUnderAFiniteWorkLimit(bool falseDigitStarts, uint work)
    {
        const string octet = @"(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        var prefix = falseDigitStarts ? string.Concat(Enumerable.Repeat("1x", 2048)) : new string('x', 32763);
        var input = Encoding.UTF8.GetBytes(prefix + "😀 123.17.24.25");
        using var regex = Utf8Regex.Compile(@"(?:" + octet + @"\.){3}" + octet, policy: new()
        {
            ResourcePolicy = Funded with { MaxWorkSteps = work }
        });
        using var live = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input, live.Token));
        Assert.True(regex.IsMatch(input, live.Token));
    }

    [Theory]
    [InlineData("(?<tag>AX|BY)[A-Z0-7]{4}|(?<word>[a-zA-Z0-9+/]{8})")]
    [InlineData("(?<word>[a-zA-Z0-9+/]{8})|(?<tag>AX|BY)[A-Z0-7]{4}")]
    [InlineData("(?<tag>AX|BY)[A-Z0-7]{4}|(?<word>[a-zA-Z0-9+/]{8})|(?<other>é.)")]
    public void BranchedPrefixRejectionKeepsOrderedSurvivorsAndCaptures(string alternatives)
    {
        var pattern = """(?:(?<open>'|")(?:""" + alternatives + """)(?:'|"))+""";
        const string prefix = "'AX1234' 😀 ";
        const string suffix = "'AXz!' x 'AXPLUS12' \"BY2345\" x 'éq' x 'AX0000";
        var text = prefix + suffix;
        var input = Encoding.UTF8.GetBytes(text);
        var start = Encoding.UTF8.GetByteCount(prefix);
        var bcl = new Regex(pattern);
        var expected = bcl.Matches(text, prefix.Length);
        using var regex = Utf8Regex.Compile(pattern, policy: new() { ResourcePolicy = Funded });
        using var token = new CancellationTokenSource();
        using var cursor = regex.CreateMatchCursor(input, start, token.Token);
        foreach (Match match in expected)
        {
            Assert.True(cursor.MoveNext(out var actual));
            Assert.Equal(match.Value, Encoding.UTF8.GetString(actual.GetValueMemory().Span));
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)), actual.Value.StartOffsetInBytes);
            for (var group = 1; group < match.Groups.Count; group++)
                Assert.Equal(match.Groups[group].Value, Encoding.UTF8.GetString(actual.GetGroupMemory(group).Span));
        }
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(bcl.Count(text), regex.Count(input, token.Token));
    }

    [Fact]
    public void ImpossibleBranchedPrefixesStayWithinFiniteWork()
    {
        const string pattern = """(?:(?<open>'|")(?:(?<tag>AX|BY)[A-Z0-7]{4}|(?<word>[a-zA-Z0-9+/]{8}))(?:'|"))+""";
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("'AXz!'😀", 512)));
        using var regex = Utf8Regex.Compile(pattern, policy: new()
        {
            ResourcePolicy = Funded with { MaxDepth = 1, MaxWorkSteps = 5000 }
        });
        using var token = new CancellationTokenSource();
        Assert.Equal(0, regex.Count(input, token.Token));
        Assert.False(regex.IsMatch(input, token.Token));
    }

    private const string StraightPrefixPattern = @"(?:# [Nn][Oo][Qq][Aa])(?::\s?(([A-Z]+[0-9]+(?:[,\s]+)?)+))?";

    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 4000000, MaxDepth = 4096, MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 };
}
