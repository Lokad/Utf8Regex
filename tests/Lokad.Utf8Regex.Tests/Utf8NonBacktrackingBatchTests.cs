using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NonBacktrackingBatchTests
{
    [Fact]
    public void CyclicCountPreservesEmptyScalarProgressUnderABoundedWorkingLimit()
    {
        const int scalarCount = 128;
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", scalarCount)) + "aaaz");
        using var regex = new Utf8Regex("(a?)*z|", RegexOptions.NonBacktracking, new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 128 },
        });

        Assert.Equal(scalarCount + 2, regex.Count(input));
    }

    [Fact]
    public void WideCountPreservesEmptyScalarProgressUnderABoundedWorkingLimit()
    {
        const int scalarCount = 128;
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀", scalarCount)) + new string('a', 72) + "z");
        using var regex = new Utf8Regex("(?<run>a{72})z|", RegexOptions.NonBacktracking, new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1_024 },
        });

        Assert.Equal(scalarCount + 2, regex.Count(input));
    }

    [Fact]
    public void WideRegularStartsPreserveFoldedScalarsAndByteOffsets()
    {
        const int prefixLength = 8_192;
        var input = Encoding.UTF8.GetBytes(new string('!', prefixLength) + "K");
        var options = RegexOptions.NonBacktracking | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        using var regex = new Utf8Regex("(?<long>a{72})z|(?<fold>k+)", options, new()
        {
            ResourcePolicy = new() { MaxDepth = 4096 },
        });

        Assert.False(regex.IsMatch(input.AsSpan(0, prefixLength)));
        Assert.True(regex.IsMatch(input));
        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal(prefixLength, match.StartOffsetInBytes);
        Assert.Equal(input.Length, match.EndOffsetInBytes);
    }

    [Fact]
    public void WideSearchResumesPastFailedStartsUnderAFiniteWorkLimit()
    {
        const int gap = 16_384;
        using var regex = new Utf8Regex("(?<long>a{72})z|(?<short>xy)", RegexOptions.NonBacktracking, new()
        {
            ResourcePolicy = new() { MaxDepth = 4096 },
        });
        var prefix = "éa!" + new string('!', gap) + "😀";
        var input = Encoding.UTF8.GetBytes(prefix + "xy");
        var range = new Utf8CaptureRange(Encoding.UTF8.GetByteCount(prefix), input.Length);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[3];

        Assert.True(regex.IsMatch(input));
        Assert.False(regex.IsMatch(input.AsSpan(0, range.StartOffsetInBytes)));
        Assert.Equal(range, regex.Match(input, 2, captures));
        Assert.False(captures[1].Success);
        Assert.Equal(range, captures[2]);
    }

    [Fact]
    public void ResumedWideSearchRevisitsAssertionsAtTheNewEdge()
    {
        using var regex = new Utf8Regex(@"(?:\b(?<part>X))+\b!|(?<long>Y{72})z", RegexOptions.NonBacktracking);
        var prefix = "X" + new string('q', 16_384) + "!";
        var input = Encoding.UTF8.GetBytes(prefix + "X!");
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[3];

        Assert.Equal(new Utf8CaptureRange(prefix.Length, input.Length), regex.Match(input, captures: captures));
        Assert.Equal(new Utf8CaptureRange(prefix.Length, prefix.Length + 1), captures[1]);
        Assert.False(captures[2].Success);
    }

    [Fact]
    public void TrimmedWideRowsKeepWordContextAndOriginalOffsets()
    {
        const RegexOptions options = RegexOptions.NonBacktracking | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        using var regex = new Utf8Regex(@"(?<first>\Ak+)|(?<joined>\Bk+)|x{72}", options);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        Assert.True(program.Instructions.Length > 64);
        Assert.True(program.NonBacktrackingOrderedStartBytes.Count > 0);
        var prefix = new string('!', 257) + "𐐀";
        var input = Encoding.UTF8.GetBytes(prefix + "K!");
        var start = Encoding.UTF8.GetByteCount(prefix);
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[3];
        Assert.Equal(new Utf8CaptureRange(start, start + 3), regex.Match(input, 257, captures));
        Assert.False(captures[1].Success);
        Assert.Equal(new Utf8CaptureRange(start, start + 3), captures[2]);
        var cursor = regex.EnumerateMatches(input, 257);
        var alias = cursor.GetEnumerator();
        Assert.True(cursor.MoveNext());
        Assert.Equal(new Utf8CaptureRange(start, start + 3), cursor.Current);
        Assert.False(cursor.MoveNext());
        Assert.True(alias.MoveNext());
        Assert.Equal(new Utf8CaptureRange(start, start + 3), alias.Current);
        Assert.Throws<ArgumentOutOfRangeException>(() => { var rejected = regex.EnumerateMatches(input, 258); rejected.MoveNext(); });

        Assert.Equal(prefix + "[K]!", Encoding.UTF8.GetString(regex.Replace(input, "${first}[${joined}]"u8)));
        using var detailed = regex.CreateMatchCursor(input, 257);
        Assert.True(detailed.MoveNext(out var retained));
        Assert.False(detailed.MoveNext(out _));
        Assert.Equal("K"u8.ToArray(), retained.GetGroupMemory(regex.GroupNumberFromUtf8Name("joined"u8)).ToArray());
    }

    [Fact]
    public void SelectedWideCaptureRangeKeepsBothWordNeighborsUnderAWorkingCap()
    {
        var pattern = "(?:" + string.Concat(Enumerable.Repeat("(a)", 160)) + @")z|\B(?<symbol>K+)\B";
        using var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking, new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 32 * 1024 },
        });
        var input = Encoding.UTF8.GetBytes("𐐀KKx" + new string('!', 140));
        var captures = new Utf8CaptureRange[regex.CaptureGroupCount + 1];
        Assert.Equal(new Utf8CaptureRange(4, 10), regex.Match(input, 4, captures));
        Assert.Equal(new Utf8CaptureRange(4, 10), captures[^1]);
        Assert.False(captures[1].Success);
    }

    [Fact]
    public void ProvedWideMissDoesNotNeedViabilityStorage()
    {
        using var regex = new Utf8Regex(@"(?<x>a{72})z", RegexOptions.NonBacktracking, new()
        {
            ResourcePolicy = new() { MaxWorkingBytes = 128 },
        });
        var input = Encoding.UTF8.GetBytes(new string('!', 8192));
        var cursor = regex.EnumerateMatches(input, 16);
        Assert.False(cursor.MoveNext());
        using var detailed = regex.CreateMatchCursor(input);
        Assert.False(detailed.MoveNext(out _));
    }

    [Theory]
    [InlineData(@"(?<x>a?)(?<y>b?)|(?<z>ab)", "ab a b é")]
    [InlineData(@"(?:a(?<x>b?)|(?<y>a?))(?<last>c?)", "abc a acc\n")]
    [InlineData(@"(?:^|\b|$)(?<x>a*)", "aa ab\nb")]
    [InlineData(@"(?<x>a+?)|(?<y>a+)", "aaaa b a")]
    [InlineData(@"(?<x>😀|é|a)+|$", "éa😀\0a😀\n")]
    [InlineData(@"(?:(?<x>[a\d])(?<y>b?))*c|$", "ab1c aa2bc !")]
    [InlineData(@"(?<x>[^ab]+)|(?<y>a)|$", "abé😀\n")]
    [InlineData(@"(?<x>a){0,3}?b|$", "aaaab aab b")]
    [InlineData(@"^(?<x>a*)\b$", "aa")]
    [InlineData(@"^(?<x>a*)\B$", "aa")]
    [InlineData(@"^(?<x>a*)\B$", "")]
    [InlineData(@"(?<x>^|$)\b(?<y>a?)", "a\nb")]
    [InlineData(@"\B(?<x>a*)$", "!aa!")]
    [InlineData(@"\b(?<x>a*)\B", "aa!")]
    public void SharedEpsilonDependenciesPreservePublicPriorityAndLastCaptures(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.NonBacktracking | RegexOptions.Multiline | RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : 0);
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options: options);
            var matches = oracle.Matches(text);
            Assert.Equal(matches.Count, regex.Count(input));
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            Assert.Equal(oracle.Replace(text, "<$0:$1>"), Encoding.UTF8.GetString(regex.Replace(input, "<$0:$1>"u8)));
            using var cursor = regex.CreateMatchCursor(input);
            foreach (Match expected in matches)
            {
                Assert.True(cursor.MoveNext(out var found));
                Assert.Equal((Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index + expected.Length))),
                    (found.Value.StartOffsetInBytes, found.Value.EndOffsetInBytes));
                foreach (var number in oracle.GetGroupNumbers())
                {
                    var wanted = expected.Groups[number];
                    Assert.True(found.TryGetGroup(number, out var actual));
                    Assert.Equal(wanted.Success, actual.Success);
                    var captures = found.GetCaptures(number);
                    Assert.Equal(wanted.Captures.Count, captures.Length);
                    Assert.Equal(Encoding.UTF8.GetBytes(wanted.Value), found.GetGroupMemory(number).ToArray());
                    for (var index = 0; index < captures.Length; index++)
                    {
                        var capture = wanted.Captures[index];
                        Assert.Equal((Encoding.UTF8.GetByteCount(text.AsSpan(0, capture.Index)), Encoding.UTF8.GetByteCount(text.AsSpan(0, capture.Index + capture.Length))),
                            (captures[index].StartOffsetInBytes, captures[index].EndOffsetInBytes));
                    }
                }
            }
            Assert.False(cursor.MoveNext(out _));
        }
    }

    [Theory]
    [InlineData(@"\b(?<x>.)\B|(?<last>.)$", "aé中😀\0z\r\n")]
    [InlineData(@"(?<x>\w+)\b|(?<last>\W)$", "é中a\u0301😀\0z\n")]
    [InlineData(@"^\s*///(?<title>#.*)$", " \n///#é😀\n///not\n \r\n///#中\n")]
    [InlineData(@"(?<x>😀|é|a)+|$", "😀éa\0😀😀\n")]
    [InlineData(@"\B(?<x>.)\b|^$", "")]
    [InlineData(@"\B(?<x>.)\b|^$", "\0😀éa\u0301\n")]
    public void ReverseRowsPreserveBothAdjacentScalars(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.Multiline | RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : 0);
            // Ordinary owned matching is a scalar oracle even for supplementary dots.
            // Only last capture values/ranges are shared with NonBacktracking.
            var ordinary = new Utf8Regex(pattern, options: options);
            var regex = new Utf8Regex(pattern, options: options | RegexOptions.NonBacktracking);
            Assert.Equal(ordinary.Count(input), regex.Count(input));
            Assert.Equal(ordinary.IsMatch(input), regex.IsMatch(input));
            Assert.Equal(Encoding.UTF8.GetString(ordinary.Replace(input, "<$0>"u8)), Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
            using var expected = ordinary.CreateMatchCursor(input);
            using var actual = regex.CreateMatchCursor(input);
            while (expected.MoveNext(out var wanted))
            {
                Assert.True(actual.MoveNext(out var found));
                Assert.Equal(wanted.Value, found.Value);
                foreach (var group in Enumerable.Range(0, ordinary.CaptureGroupCount + 1).Select(ordinary.GroupNumberFromCaptureSlot).ToArray())
                {
                    Assert.True(wanted.TryGetGroup(group, out var left));
                    Assert.True(found.TryGetGroup(group, out var right));
                    Assert.Equal(left, right);
                    Assert.Equal(wanted.GetGroupMemory(group).ToArray(), found.GetGroupMemory(group).ToArray());
                }
            }
            Assert.False(actual.MoveNext(out _));
        }
    }

    [Fact]
    public void PackedWordBoundariesPreservePublicCountsCapturesAndAliases()
    {
        var widths = new HashSet<int>();
        for (var length = 48; length <= 70; length++)
        {
            var literal = new string('a', length);
            var pattern = $"(?<x>{literal})|(?<last>b)$";
            var regex = new Utf8Regex(pattern, options: RegexOptions.NonBacktracking);
            var width = regex.Inspection.AvailableOwnedSliceProgram.Instructions.Length;
            if (width is < 63 or > 65) continue;
            widths.Add(width);
            var oracle = new Regex(pattern, RegexOptions.NonBacktracking);
            foreach (var text in new[] { "", "b", "é" + literal + "😀b", literal + literal + "!" })
            {
                var input = Encoding.UTF8.GetBytes(text);
                Assert.Equal(oracle.Count(text), regex.Count(input));
                Assert.Equal(oracle.Replace(text, "<$0>"), Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
                var matches = oracle.Matches(text);
                var cursor = regex.EnumerateMatches(input);
                var alias = cursor.GetEnumerator();
                var index = 0;
                while (cursor.MoveNext())
                {
                    var expected = matches[index++];
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), cursor.Current.StartOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes));
                }
                Assert.Equal(matches.Count, index);
                Assert.Equal(matches.Count != 0, alias.MoveNext());
                if (matches.Count != 0)
                {
                    var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                    Assert.Equal(matches[0].Groups["x"].Value, System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
                    Assert.Equal(matches[0].Groups["last"].Value, System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(regex.GroupNumberFromUtf8Name("last"u8)).Span));
                }
            }
        }
        Assert.Contains(63, widths);
        Assert.Contains(64, widths);
        Assert.Contains(65, widths);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PersistentScalarTableFundsCompleteOffsetAndBitArrays(bool empty)
    {
        var program = Program("(.)?");
        Assert.InRange(program.Instructions.Length, 1, 64);
        var input = empty ? [] : "😀é"u8.ToArray();
        var invocation = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out var table));
        Assert.NotNull(table);
        Assert.Equal(13UL + (empty ? 96UL : 112UL), invocation.Ledger.ReservedBytes);
        Assert.Equal(0, table.ResolveByteOffset(input, 0));
        if (!empty)
        {
            Assert.Equal(1, table.ResolveByteOffset(input, 4));
            Assert.Equal(2, table.ResolveByteOffset(input, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.ResolveByteOffset(input, 1));
        }
        table.Release(ref invocation);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    private static Utf8SliceProgram Program(string pattern) =>
        new Utf8Regex(pattern, options: RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)            .Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Owned graph missing.");

    [Theory]
    [InlineData("a*b|", false)]
    [InlineData("a*b|", true)]
    [InlineData("(a*)*b|", false)]
    [InlineData("(a*)*b|", true)]
    [InlineData("a*?b|", false)]
    [InlineData("a*?b|", true)]
    [InlineData("(?:a|aa)*?b|", false)]
    [InlineData("(?:a|aa)*?b|", true)]
    [InlineData("a*b", false)]
    [InlineData("a*b", true)]
    public void CountWorkIsLinearAcrossTheWholeBatch(string pattern, bool hit)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var program = Program(pattern);
        ulong previous = 0;
        foreach (var size in new[] { 64, 128, 256, 512, 1024, 2048 })
        {
            var input = Encoding.UTF8.GetBytes(new string('a', size) + (hit ? "b" : ""));
            var invocation = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, default);
            var result = Utf8SliceRunner.CountFromBytes(program, input, 0,
                new int[program.GroupCount], new int[program.GroupCount], ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
            Assert.Equal(hit ? (pattern.EndsWith('|') ? 2 : 1) : (pattern.EndsWith('|') ? size + 1 : 0), result.Count);
            if (previous != 0) Assert.InRange(invocation.Budget.DiagnosticCheckpoints, previous, previous * 2 + 64);
            previous = invocation.Budget.DiagnosticCheckpoints;
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarBatchesPreserveOffsetsSplitAndBothReplacementForms(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var regex = new Utf8Regex("(?<x>😀)*z|", options: options);
        var input = "😀😀"u8.ToArray();
        Assert.Equal(3, regex.Count(input));
        var ranges = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(input)) ranges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.Equal(new[] { (0, 0), (4, 0), (8, 0) }, ranges);
        var fromOffset = new List<int>();
        foreach (var match in regex.EnumerateMatches(input, 4)) fromOffset.Add(match.StartOffsetInBytes);
        Assert.Equal(new[] { 4, 8 }, fromOffset);
        Assert.Throws<ArgumentOutOfRangeException>(() => { var cursor = regex.EnumerateMatches(input, 1); cursor.MoveNext(); });
        Assert.Equal("Q😀Q😀Q", Encoding.UTF8.GetString(regex.Replace(input, "Q"u8)));
        var pieces = new List<string>();
        foreach (var piece in regex.EnumerateSplits(input)) pieces.Add(Encoding.UTF8.GetString(piece.GetValueBytes()));
        Assert.Equal(new[] { "", "😀", "😀", "" }, pieces);
        var shared = new Utf8Regex("(?<x>a(?<x>b)c)", options: options);
        Assert.Equal("<abc>", Encoding.UTF8.GetString(shared.Replace("abc"u8, "<${x}>"u8)));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(shared.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void EveryCountWorkAndStorageCutoffRestoresBorrowedStorage()
    {
        var program = Program("(?<x>.)*z|");
        var input = "😀a"u8.ToArray();
        var baseline = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(3, Utf8SliceRunner.CountFromBytes(program, input, 0, new int[1], new int[1], ref baseline).Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong cap = 13; cap <= baseline.Budget.PeakWorkingBytes; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var result = Utf8SliceRunner.CountFromBytes(program, input, 0, new int[1], new int[1], ref invocation);
            Assert.Equal(cap == baseline.Budget.PeakWorkingBytes ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, result.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void PreparationCancellationReleasesAnUnpublishedTable()
    {
        var program = Program("(a*)*b|");
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        // The first two passes have 128 steps; cancel inside viability filling.
        invocation.CheckpointHook = steps => { if (steps >= 192) source.Cancel(); };
        var error = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.CountFromBytes(program, Encoding.UTF8.GetBytes(new string('a', 64)), 0,
                new int[1], new int[1], ref invocation));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("(?<x>a(?<x>b)c)")]
    [InlineData("a{72}z|(?<x>a(?<x>b)c)")]
    public void EveryCaptureReplayCutoffRestoresWorkingStorage(string pattern)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var program = Program(pattern);
        var baseline = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8NonBacktrackingSearchState.TryCreate(program, "abc"u8, ref baseline, out var measured));
        Assert.NotNull(measured);
        var preparationSteps = baseline.Budget.DiagnosticCheckpoints;
        var boundary = 0;
        var found = measured.FindFirst(program, ref boundary, new int[1], new int[1], ref baseline);
        Assert.True(found.Found);
        var replaySteps = baseline.Budget.DiagnosticCheckpoints - preparationSteps;
        measured.Release(ref baseline);
        for (ulong cap = 13; cap <= baseline.Budget.PeakWorkingBytes; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(0, cap), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var breach = Utf8NonBacktrackingSearchState.TryCreate(program, "abc"u8, ref invocation, out var table);
            if (table is not null)
            {
                boundary = 0;
                var result = table.FindFirst(program, ref boundary, new int[1], new int[1], ref invocation);
                breach = result.Breach;
                table.Release(ref invocation);
            }
            Assert.Equal(cap == baseline.Budget.PeakWorkingBytes ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void FinalReplayCancellationReleasesTheTableAndClearsSlots()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var program = Program("(a)");
        var baseline = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8NonBacktrackingSearchState.TryCreate(program, "a"u8, ref baseline, out var measured));
        Assert.NotNull(measured);
        var boundary = 0;
        var found = measured.FindFirst(program, ref boundary, new int[1], new int[1], ref baseline);
        Assert.True(found.Found);
        measured.Release(ref baseline);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        var padding = (64 - baseline.Budget.DiagnosticCheckpoints % 64) % 64;
        for (ulong step = 0; step < padding; step++) _ = invocation.CheckProgress();
        var finalStep = baseline.Budget.DiagnosticCheckpoints + padding;
        invocation.CheckpointHook = steps => { if (steps == finalStep) source.Cancel(); };
        var starts = new[] { 42 }; var ends = new[] { 43 };
        var error = Assert.Throws<OperationCanceledException>(() =>
        {
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8NonBacktrackingSearchState.TryCreate(program, "a"u8, ref invocation, out var table));
            Assert.NotNull(table);
            try { boundary = 0; _ = table.FindFirst(program, ref boundary, starts, ends, ref invocation); }
            finally { table.Release(ref invocation); }
        });
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(-1, starts[0]); Assert.Equal(-1, ends[0]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicEnumerationWorkGrowsLinearlyAndCopiesKeepTheirPosition(bool compiled)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = new Utf8Regex("a*b|", options: RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0));
        ulong previous = 0;
        foreach (var size in new[] { 64, 128, 256, 512 })
        {
            var cursor = regex.EnumerateMatches(Encoding.UTF8.GetBytes(new string('a', size)));
            var copy = cursor.GetEnumerator();
            var count = 0;
            while (cursor.MoveNext()) count++;
            Assert.Equal(size + 1, count);
            var steps = cursor.DiagnosticCheckpointCount;
            if (previous != 0) Assert.InRange(steps, previous, previous * 2 + 64);
            previous = steps;
            Assert.True(copy.MoveNext());
            Assert.Equal(0, copy.Current.StartOffsetInBytes);
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void AbortedSplitSetupImmediatelyReturnsTheTableLoan()
    {
        var program = Program("(a*)*b|");
        var input = "aaaa"u8.ToArray();
        var measured = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, default);
        var cursor = Utf8OwnedSplitPreparation.Create(program, input, 0, ref measured);
        Assert.True(measured.Ledger.ReservedBytes > 0);
        using var cancellation = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, cancellation.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        invocation.CheckpointHook = _ => cancellation.Cancel();
        for (var step = 0; step < 63; step++) invocation.CheckProgress();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            Utf8OwnedSplitPreparation.Create(program, input, 0, ref invocation)).CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.True(cursor.MoveNext());
    }

    [Fact]
    public void CaptureOpeningsAreReusedAcrossFiniteEmissionCopies()
    {
        var small = Program("(?<x>a){2}");
        var large = Program("(?<x>a){1024}");
        Assert.Equal(1, small.NonBacktrackingCaptureOpeningCount);
        Assert.Equal(small.NonBacktrackingCaptureOpeningCount, large.NonBacktrackingCaptureOpeningCount);
        var nested = Program("(?<x>a(?<x>b)c){16}");
        Assert.Equal(2, nested.NonBacktrackingCaptureOpeningCount);
        var regex = new Utf8Regex("(?<x>a(?<x>b)c){16}", options: RegexOptions.NonBacktracking);
        var result = Utf8MatchSequence.First(regex.CreateMatchCursor(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abc", 16)))));
        Assert.Equal("abc", Encoding.UTF8.GetString(result.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CopiedAndAbandonedCursorsShareOneTableLoan(bool compiled)
    {
        var regex = new Utf8Regex("(?<x>.)*z|", options: RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Owned graph missing.");
        var invocation = new Utf8SliceInvocation(new(0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        var held = ExerciseCopiesAndAbandon(program, "a😀b"u8.ToArray(), ref invocation);
        CollectLeases();
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong ExerciseCopiesAndAbandon(Utf8SliceProgram program, byte[] input, ref Utf8SliceInvocation invocation)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        invocation.ShareStorageForCursor();
        var original = new Utf8OperationMatchCursor(program, input, invocation);
        var held = invocation.Ledger.ReservedBytes;
        FinishCopy(ref original);
        CollectLeases();
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        Assert.True(original.MoveNext());
        Assert.Equal(0, original.CurrentValueMatch.StartOffsetInBytes);
        FinishCopy(ref original);
        CollectLeases();
        Assert.Equal(held, invocation.Ledger.ReservedBytes);
        Assert.True(original.MoveNext());
        Assert.Equal(1, original.CurrentValueMatch.StartOffsetInBytes);
        // Abandon the original; only the numeric invocation charge persists.
        return held;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FinishCopy(ref Utf8OperationMatchCursor original)
    {
        var copy = original;
        while (copy.MoveNext()) { }
    }

    private static void CollectLeases()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }
}
