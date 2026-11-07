using System.Buffers;
using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LiteralGlobalOperationTests
{
    [Fact]
    public void ControlledByteOnlyCountDenialPrecedesUnvisitedMalformedTail()
    {
        var regex = new Utf8Pcre2Regex("é", default, default,
            new() { MatchLimit = 1 }, Timeout.InfiniteTimeSpan);
        var bytes = Encoding.UTF8.GetBytes(new string('x', 8192) + "é");
        bytes[^1] = 0xff;
        var failure = Assert.Throws<Pcre2MatchException>(() => regex.Count(bytes));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind);
        using var token = new CancellationTokenSource();
        token.Cancel();
        var cancelled = Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, 0, default, token.Token));
        Assert.Equal(token.Token, cancelled.CancellationToken);
    }

    [Theory]
    [InlineData("needle")]
    [InlineData("é")]
    [InlineData("😀")]
    [InlineData("夏洛克·福尔摩斯")]
    public void ControlledByteOnlyCountKeepsScalarOffsetsAndWholeInputErrors(string pattern)
    {
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, default, default,
            new() { MatchLimit = 100000, DepthLimit = 1, HeapLimitInBytes = 1, ResultLimitInBytes = 1 },
            Timeout.InfiniteTimeSpan);
        foreach (var padding in new[] { 1023, 1024, 1025, 2025, 2047, 2048 })
        {
            var input = Encoding.UTF8.GetBytes("😀" + new string('x', padding) + pattern + " " + pattern);
            Assert.Equal(2, regex.Count(input, 4, Pcre2MatchOptions.None, token.Token));
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(input, 2, default, token.Token));
            Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff], 2, default, token.Token));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ByteOnlyCountKeepsCumulativeWorkDenialAndBorrowedCapacity(bool longNeedle)
    {
        var pattern = longNeedle ? new string('a', 2050) + "é" : "é";
        var regex = new Utf8Pcre2Regex(pattern);
        var program = Assert.IsType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Count).Program;
        var bytes = Encoding.UTF8.GetBytes(new string('x', 4095) + pattern + " " + pattern);
        var required = Execute(100000, expectDenial: false);
        Assert.True(required > 1);
        Execute(required - 1, expectDenial: true);

        ulong Execute(ulong work, bool expectDenial)
        {
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 1, 64, 1),
                Timeout.InfiniteTimeSpan, CancellationToken.None);
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(17));
            try
            {
                Assert.Equal(Utf8ExecutionLimitKind.None, Utf8Validation.TryValidateOnly(bytes, ref invocation));
                var breach = program.TryCountNonOverlapping(bytes, ref invocation, out var count);
                Assert.Equal(expectDenial ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None, breach);
                Assert.Equal(expectDenial ? 0 : 2, count);
                if (expectDenial)
                    Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, invocation.TryChargeInputStep());
                Assert.Equal(17UL, invocation.Ledger.ReservedBytes);
                return invocation.Budget.Steps;
            }
            finally { invocation.Ledger.Release(17); Assert.Equal(0UL, invocation.Ledger.ReservedBytes); }
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void ByteOnlyValidationAndSearchRetainOriginalInterruption(bool duringSearch, bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("é", default, default,
            new() { MatchLimit = 100000, DepthLimit = 1, HeapLimitInBytes = 1, ResultLimitInBytes = 1 },
            TimeSpan.FromMilliseconds(20));
        var bytes = Encoding.UTF8.GetBytes(new string('x', 262144) + "é");
        var checkpoints = 0;
        // Validation consumes 131 bounded steps. The first search checkpoint
        // after that is 192; the existing hook runs every 64 charged steps.
        var session = Utf8SearchDiagnosticsSession.Start(step =>
        {
            if (step < (duringSearch ? 192UL : 64UL)) return;
            checkpoints++;
            if (cancel) token.Cancel(); else ticks = 25;
        });
        Exception? failure = null;
        try
        {
            regex.Count(bytes, 0, Pcre2MatchOptions.None, token.Token);
        }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(checkpoints > 0);
        if (cancel) Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

    [Theory]
    [InlineData("needle")]
    [InlineData("é")]
    [InlineData("😀")]
    [InlineData("夏洛克·福尔摩斯")]
    public void DirectCountNeedsNoDiscardedCoordinatesButKeepsFullValidation(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        foreach (var padding in new[] { 1023, 1024, 1025, 4096 })
        {
            var text = "😀" + new string('x', padding) + pattern + " " + pattern;
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(2, regex.Count(input, 4));
            var invalidOffset = Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(input, 2));
            Assert.Equal("startOffsetInBytes", invalidOffset.ParamName);
            foreach (var offset in new[] { -1, 2, input.Length + 1 })
                Assert.Throws<ArgumentException>(() => regex.Count([.. input, 0xff], offset));
            var matches = regex.EnumerateMatches(input, 4);
            Assert.True(matches.MoveNext());
            Assert.Equal(4 + padding, matches.Current.StartOffsetInBytes);
            Assert.Equal(2 + padding, matches.Current.StartOffsetInUtf16);
            var first = matches.Current;
            Assert.True(matches.MoveNext());
            Assert.Equal(2 + padding, first.StartOffsetInUtf16);
        }
    }

    [Fact]
    public void LiteralGlobalOperationsShareTheGenericProgram()
    {
        var regex = new Utf8Pcre2Regex("é");
        var input = Encoding.UTF8.GetBytes("xéé😀é");

        Assert.Equal(3, regex.Count(input));
        var bytes = new List<(int Start, int End)>();
        var utf16 = new List<(int Start, int End)>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            var match = enumerator.Current;
            bytes.Add((match.StartOffsetInBytes, match.EndOffsetInBytes));
            utf16.Add((match.StartOffsetInUtf16, match.EndOffsetInUtf16));
        }

        Assert.Equal([(1, 3), (3, 5), (9, 11)], bytes);
        Assert.Equal([(1, 2), (2, 3), (5, 6)], utf16);
        Assert.IsType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Count);
        Assert.IsType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Enumerate);
        Assert.IsType<Pcre2LiteralDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
    }

    [Theory]
    [InlineData("a", "aaaa", 0, 4)]
    [InlineData("aa", "aaa", 0, 1)]
    [InlineData("é", "xéé", 1, 2)]
    [InlineData("abc", "abcabc", 1, 1)]
    public void LiteralCountPreservesNonOverlappingUnicodeAndStartSemantics(
        string pattern,
        string input,
        int startOffsetInBytes,
        int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(input), startOffsetInBytes));
    }

    [Fact]
    public void DirectLiteralCountHandlesLongUnicodeInputAfterStartOffset()
    {
        var regex = new Utf8Pcre2Regex("é");
        var input = Encoding.UTF8.GetBytes("x" + string.Concat(Enumerable.Repeat("é", 1024)));

        Assert.Equal(1024, regex.Count(input, 1));
    }

    [Fact]
    public void EmptyLiteralProgressesAtScalarsWithoutDuplicates()
    {
        var regex = new Utf8Pcre2Regex(string.Empty);
        var input = "😀x"u8;

        Assert.Equal(3, regex.Count(input));
        Assert.Equal(0, regex.Count(input, 0, Pcre2MatchOptions.NotEmpty));

        var byteStarts = new List<int>();
        var utf16Starts = new List<int>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            byteStarts.Add(enumerator.Current.StartOffsetInBytes);
            utf16Starts.Add(enumerator.Current.StartOffsetInUtf16);
        }

        Assert.Equal([0, 4, 5], byteStarts);
        Assert.Equal([0, 2, 3], utf16Starts);
    }

    [Fact]
    public void EmptyLiteralTreatsConfiguredCrlfAsOneProgressionUnit()
    {
        var regex = new Utf8Pcre2Regex(
            string.Empty,
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Crlf },
            default(Utf8Pcre2ExecutionLimits),
            Utf8Pcre2Regex.DefaultMatchTimeout);

        var starts = new List<int>();
        var enumerator = regex.EnumerateMatches("\r\nx"u8);
        while (enumerator.MoveNext())
        {
            starts.Add(enumerator.Current.StartOffsetInBytes);
        }

        Assert.Equal([0, 2, 3], starts);
    }

    [Fact]
    public void MatchManyStopsAfterOneLookahead()
    {
        var regex = new Utf8Pcre2Regex("a");
        Span<Utf8Pcre2MatchData> one = stackalloc Utf8Pcre2MatchData[1];

        var written = regex.MatchMany("a a"u8, one, out var isMore);

        Assert.Equal(1, written);
        Assert.True(isMore);
        Assert.Equal(0, one[0].StartOffsetInBytes);

        Span<Utf8Pcre2MatchData> all = stackalloc Utf8Pcre2MatchData[2];
        written = regex.MatchMany("a a"u8, all, out isMore);
        Assert.Equal(2, written);
        Assert.False(isMore);
        Assert.Equal(2, all[1].StartOffsetInBytes);

        written = regex.MatchMany("a a"u8, Span<Utf8Pcre2MatchData>.Empty, out isMore);
        Assert.Equal(0, written);
        Assert.True(isMore);
    }

    [Fact]
    public void LiteralReplacementUsesByteSegmentsAndGlobalMatches()
    {
        var regex = new Utf8Pcre2Regex("abc");

        Assert.Equal("x<abc><abc>", regex.ReplaceToString("xabcabc"u8, "<$0>"));
        Assert.Equal(
            "[abc][abc]",
            regex.ReplaceToString(
                "xabcabc"u8,
                "[$&]",
                substitutionOptions: Pcre2SubstitutionOptions.SubstituteReplacementOnly));
        Assert.Equal(
            "x$0$0",
            regex.ReplaceToString(
                "xabcabc"u8,
                "$0",
                substitutionOptions: Pcre2SubstitutionOptions.SubstituteLiteral));
    }

    [Fact]
    public void LiteralTryReplaceIsTransactionalAndReportsOverflowLength()
    {
        var regex = new Utf8Pcre2Regex("abc");
        Span<byte> destination = stackalloc byte[6];
        "stays!"u8.CopyTo(destination);

        var status = regex.TryReplace(
            "abcabc"u8,
            "long"u8,
            destination,
            out var bytesWritten,
            substitutionOptions: Pcre2SubstitutionOptions.SubstituteOverflowLength);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(8, bytesWritten);
        Assert.True(destination.SequenceEqual("stays!"u8));
    }

    [Fact]
    public void LiteralEvaluatorUsesTheProducedMatchWithoutRerunning()
    {
        var regex = new Utf8Pcre2Regex("é");
        var calls = 0;
        var result = regex.Replace(
            "xéé"u8,
            calls,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
            {
                state++;
                writer.AppendAsciiByte((byte)'[');
                writer.Append(match.Value.GetValueBytes());
                writer.AppendAsciiByte((byte)']');
            });

        Assert.True(result.AsSpan().SequenceEqual("x[é][é]"u8));
    }

    [Fact]
    public void LiteralProbeSupportsFullPartialAndNoMatchResults()
    {
        var regex = new Utf8Pcre2Regex("café");

        var full = regex.Probe("xxcafé"u8, Pcre2PartialMode.Soft);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, full.Kind);
        Assert.Equal(2, full.Value.StartOffsetInBytes);

        var partial = regex.Probe("xxca"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal(2, partial.Value.StartOffsetInBytes);
        Assert.Equal(4, partial.Value.EndOffsetInBytes);

        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("xyz"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal(
            Utf8Pcre2ProbeKind.PartialMatch,
            new Utf8Pcre2Regex(@"\Aabc").Probe("ab"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Fact]
    public void WarmedLiteralCountAndEnumerationAllocateNoObjectsPerResult()
    {
        var regex = new Utf8Pcre2Regex("a");
        var input = Encoding.UTF8.GetBytes(new string('a', 128));
        _ = regex.Count(input);
        var warm = regex.EnumerateMatches(input);
        while (warm.MoveNext())
        {
            _ = warm.Current.StartOffsetInBytes;
        }

        // Warm complete batches before the steady-state allocation sample;
        // a single Count leaves this fixture dependent on test ordering/tiering.
        for (var iteration = 0; iteration < 256; iteration++)
        {
            _ = regex.Count(input);
            var enumerator = regex.EnumerateMatches(input);
            while (enumerator.MoveNext()) _ = enumerator.Current.StartOffsetInBytes;
        }

        var sum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            sum += regex.Count(input);
            var enumerator = regex.EnumerateMatches(input);
            while (enumerator.MoveNext())
            {
                sum += enumerator.Current.StartOffsetInBytes;
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sum > 0);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void WarmedLiteralFamilyCountAndEnumerationAllocateNoObjectsPerResult()
    {
        var regex = new Utf8Pcre2Regex("alpha|amber|bravo");
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("alpha amber bravo ", 128)));
        _ = regex.Count(input);
        var warm = regex.EnumerateMatches(input);
        while (warm.MoveNext())
        {
            _ = warm.Current.StartOffsetInUtf16;
        }

        var sum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            sum += regex.Count(input);
            var enumerator = regex.EnumerateMatches(input);
            while (enumerator.MoveNext())
            {
                sum += enumerator.Current.StartOffsetInUtf16;
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sum > 0);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void WarmedWordBoundaryCandidateSearchAllocatesNoObjects()
    {
        var regex = new Utf8Pcre2Regex(@"\b\w{10,}\b");
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("short ", 128)));
        _ = regex.Count(input);

        var sum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            sum += regex.Count(input);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, sum);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void LiteralFamilyCursorSupportsConcurrentRegexReuse()
    {
        var regex = new Utf8Pcre2Regex("café|naïve|😀");
        var input = "x café naïve 😀 café"u8.ToArray();

        Parallel.For(0, 256, _ =>
        {
            Assert.Equal(4, regex.Count(input));
            var count = 0;
            var enumerator = regex.EnumerateMatches(input);
            while (enumerator.MoveNext())
            {
                Assert.True(enumerator.Current.HasUtf16Projection);
                count++;
            }

            Assert.Equal(4, count);
            Assert.Equal("x <café> <naïve> <😀> <café>", regex.ReplaceToString(input, "<$0>"));
        });
    }

    [Fact]
    [Trait("Category", "SourceOnly")]
    public void GenericGlobalDriverHasNoCompletePatternDispatchOrNullableArrayAssertion()
    {
        var root = FindRepositoryDirectory();
        var architecture = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Lokad.Utf8Regex.Pcre2",
            "Pcre2ExecutionArchitecture.cs"));
        var cursorStart = architecture.IndexOf("internal ref struct Pcre2DirectGlobalMatchCursor", StringComparison.Ordinal);
        Assert.True(cursorStart >= 0);
        var cursorSource = architecture[cursorStart..];
        Assert.DoesNotContain("Pattern switch", cursorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Pattern ==", cursorSource, StringComparison.Ordinal);

        var coreTypes = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Lokad.Utf8Regex.Pcre2",
            "Utf8Pcre2CoreTypes.cs"));
        Assert.DoesNotContain("_matches!", coreTypes, StringComparison.Ordinal);
    }

    private static string FindRepositoryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Lokad.Utf8Regex.Pcre2")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
