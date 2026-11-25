using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// Also run this fixture with IsDynamicCodeSupported disabled in the test host's
// runtimeconfig. No alternate library build or execution mode is required.
public sealed class Utf8DynamicCodeCapabilityTests
{
    [Theory]
    [InlineData(@"\G[=+^]+", "😀x+=^é", 5, 8)]
    [InlineData(@"\G-?[0-9]+(\.[0-9]+)?(e[+-]?[0-9]+)?", "😀x-12.34e-5!", 5, 14)]
    [InlineData(@"\G-?[0-9]+(\.[0-9]+)?(e[+-]?[0-9]+)?", "😀x12e-", 5, 7)]
    public void EmittedContiguousRangeStartsAtTheAdmittedByteOrigin(string pattern, string text, int start, int end)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(text);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var dynamicCode = RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
        Assert.Equal(dynamicCode, program.EmittedMatch is not null);
        if (program.EmittedMatch is { } proof) Assert.Equal(end - start, proof(bytes.AsSpan(start)));
        Assert.Equal(new Utf8CaptureRange(start, end), regex.Match(bytes, start));
        Assert.False(regex.Match(bytes, start - 1).Success);
        using var source = new CancellationTokenSource();
        Assert.Equal(new Utf8CaptureRange(start, end), regex.Match(bytes, start, cancellationToken: source.Token));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(129)]
    [InlineData(254)]
    [InlineData(255)]
    [InlineData(257)]
    public void ContiguousRepeatKeepsTheClippedExtentAcrossTheProofCutoff(int width)
    {
        using var regex = new Utf8Regex(@"\G[=+^]+", RegexOptions.Compiled);
        var text = Encoding.UTF8.GetBytes("é" + new string('=', width + 1) + "x+=");
        var end = 2 + width;
        var slots = new Utf8CaptureRange[1];
        Assert.Equal(new Utf8CaptureRange(2, end), regex.Match(text, 2, slots, endOffsetInBytes: end));
        Assert.Equal(new Utf8CaptureRange(2, end), slots[0]);
        Assert.Equal(new Utf8CaptureRange(2, end + 1), regex.Match(text, 2));
        Assert.False(regex.Match(text, end + 1).Success);
    }

    [Fact]
    public void LongerContiguousRepeatKeepsWideCaseAliases()
    {
        using var regex = new Utf8Regex(@"\G[k]+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var text = Encoding.UTF8.GetBytes("é" + new string('k', 48) + "Kk!");
        var expected = new Utf8CaptureRange(2, text.Length - 1);
        Assert.Equal(expected, regex.Match(text, 2));
        using var source = new CancellationTokenSource();
        Assert.Equal(expected, regex.Match(text, 2, cancellationToken: source.Token));
    }

    [Fact]
    public void ContiguousRepeatWithARealRetryUsesTheInterpreter()
    {
        using var regex = new Utf8Regex(@"\G[ab]+ab\z", RegexOptions.Compiled);
        Assert.Null(regex.Inspection.AvailableOwnedSliceProgram.EmittedMatch);
        Assert.Equal(new Utf8CaptureRange(2, 5), regex.Match("éaab"u8, 2));
    }

    [Fact]
    public void OrdinaryContiguousRangeTiersUpFromUncontrolledCalls()
    {
        using var regex = new Utf8Regex(@"\G[=+^]+");
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var dynamicCode = RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
        Assert.Equal(dynamicCode, program.ObservesProofWork);
        var bytes = Encoding.UTF8.GetBytes("é" + new string('+', 32));
        var expected = new Utf8CaptureRange(2, bytes.Length);
        using var source = new CancellationTokenSource();
        for (var index = 0; index < 256; index++)
            Assert.Equal(expected, regex.Match(bytes, 2, cancellationToken: source.Token));
        Assert.Null(program.TieredProof);
        for (var index = 0; index < 32_768 && program.ObservesProofWork; index++)
            Assert.Equal(expected, regex.Match(bytes, 2));
        Assert.Equal(dynamicCode, program.TieredProof is not null);
        Assert.Equal(expected, regex.Match(bytes, 2));
    }

    [Theory]
    [InlineData(@"\A(?<unit>[1A-Za-z]{2})(?:[QX]?[XYZ]|[uV][0-9])$", "a1X\n", 3)]
    [InlineData(@"\A(?<unit>[1A-Za-z]{2})(?:[QX]?[XYZ]|[uV][0-9])$", "a1u4\n", 4)]
    [InlineData(@"\A(?<unit>[1A-Za-z]{2})[QX]??[XYZ]", "a1XX", 3)]
    [InlineData(@"\A(?<unit>[1A-Za-z]{2})[!07]\z", "A17", 3)]
    public void EmittedAbsoluteProgramKeepsOrderedEndpointsAndCaptures(string pattern, string text, int end)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);
        var bytes = Encoding.UTF8.GetBytes(text);
        var expected = new Utf8CaptureRange(0, end);
        Assert.True(regex.IsMatch(bytes));
        Assert.Equal(expected, regex.Match(bytes));
        Assert.False(regex.IsMatch("éaXX"u8));
        Assert.False(regex.IsMatch("a1é"u8));
        Assert.False(regex.IsMatch("a1w"u8));
        Assert.False(regex.IsMatch("a1\u0018"u8));
        var slots = new Utf8CaptureRange[2];
        Assert.Equal(expected, regex.Match(bytes, captures: slots));
        Assert.Equal(new Utf8CaptureRange(0, 2), slots[1]);
        using var cursor = regex.CreateMatchCursor(bytes);
        Assert.True(cursor.MoveNext(out var retained));
        Assert.Equal(expected, retained.Value);
        Assert.Equal(slots[1], Assert.Single(retained.GetCaptures(1).ToArray()));
    }

    [Theory]
    [InlineData("#Ab10E3", 7)]
    [InlineData("aF09B2\n", 6)]
    [InlineData("#aF9\n", 4)]
    [InlineData("FF0", 3)]
    [InlineData("A123", -1)]
    [InlineData("aF09B2\n\n", -1)]
    [InlineData("Aé3", -1)]
    [InlineData("ａf9", -1)]
    [InlineData("a𝟙3", -1)]
    public void EmittedFoldedFixedRepeatsKeepOrderedEndpointsAndCaptures(string text, int end)
    {
        using var regex = new Utf8Regex("^#?([a-f0-9]{6}|[a-f0-9]{3})$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        var input = Encoding.UTF8.GetBytes(text);
        var success = end >= 0;
        Assert.Equal(success, regex.IsMatch(input));
        Assert.Equal(success ? 1 : 0, regex.Count(input));
        var match = regex.Match(input);
        Assert.Equal(success, match.Success);
        using var cursor = regex.CreateMatchCursor(input);
        Assert.Equal(success, cursor.MoveNext(out var retained));
        if (!success) return;

        var expected = new Utf8CaptureRange(0, end);
        Assert.Equal(expected, match);
        Assert.Equal(expected, retained.Value);
        Assert.Equal(new Utf8CaptureRange(input[0] == '#' ? 1 : 0, end),
            Assert.Single(retained.GetCaptures(1).ToArray()));
    }

    [Fact]
    public void OrdinaryAbsoluteProofTiersUpOnceForUncontrolledCalls()
    {
        const string pattern = "^#?([a-f0-9]{6}|[a-f0-9]{3})$";
        using var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var dynamicCode = RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
        Assert.Equal(dynamicCode, program.ObservesProofWork);
        Assert.Null(program.EmittedMatch);
        Assert.Null(program.TieredProof);

        using var source = new CancellationTokenSource();
        var bytes = "#ab10e3"u8.ToArray();
        for (var i = 0; i < 100_000; i++)
        {
            if (!regex.IsMatch(bytes, source.Token)) throw new InvalidOperationException("Expected a match.");
        }
        Assert.Null(program.TieredProof);

        Parallel.For(0, 100_000, i =>
        {
            var expected = i % 2 == 0;
            if (regex.IsMatch(expected ? bytes : "#aF09B2\n\n"u8) != expected)
                throw new InvalidOperationException("The shared proof changed the match result.");
        });
        var tiered = program.TieredProof;
        Assert.Equal(dynamicCode, tiered is not null);
        Assert.False(program.ObservesProofWork);
        Assert.Equal(new Utf8CaptureRange(0, 4), regex.Match("#aF9\n"u8));
        Assert.Equal(new Utf8CaptureRange(0, 7), regex.Match(bytes));
        Assert.False(regex.IsMatch("Aé3"u8));
        Assert.False(regex.IsMatch(new byte[33]));
        using var cursor = regex.CreateMatchCursor(bytes);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(new Utf8CaptureRange(1, 7), Assert.Single(match.GetCaptures(1).ToArray()));
        Assert.Same(tiered, program.TieredProof);

        source.Cancel();
        Assert.Equal(source.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch(bytes, source.Token)).CancellationToken);
        using var limited = new Utf8Regex(pattern, RegexOptions.IgnoreCase,
            new() { ResourcePolicy = new() { MaxWorkSteps = 5 } });
        Assert.False(limited.Inspection.AvailableOwnedSliceProgram.ObservesProofWork);
        using var controlled = new Utf8Regex(pattern, RegexOptions.IgnoreCase,
            new() { ConstructionPolicy = new() { MaxWorkSteps = 1_000_000 } });
        Assert.False(controlled.Inspection.AvailableOwnedSliceProgram.ObservesProofWork);
    }

    [Theory]
    [InlineData(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])",
        "012.200.033.199", "012.200.033.1AA", 0)]
    [InlineData(@"(\d{4}[- ]){3}\d{3,4}", "1234-5678-1234-456", "1234-5678-1234-45X", 3)]
    [InlineData(@"(\d{4}[- ]){3}\d{3,4}", "𝟙2٣4-5678-1234-𝟜5٦", "1234-5678-1234-45X", 3)]
    public void OrdinaryCandidateProofTiersUpAndKeepsByteRanges(string pattern, string value, string miss, int captures)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var dynamicCode = RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
        Assert.Equal(dynamicCode, program.ObservesProofWork);
        Assert.Null(program.TieredProof);

        var hit = Encoding.UTF8.GetBytes(value);
        var rejected = Encoding.UTF8.GetBytes(miss);
        const string prefix = "😀é ";
        var later = Encoding.UTF8.GetBytes(prefix + miss + " " + value);
        var start = Encoding.UTF8.GetByteCount(prefix + miss + " ");
        var expected = new Utf8CaptureRange(start, later.Length);
        Assert.Equal(expected, regex.Match(later));
        for (var i = 0; i < 131_072 && program.ObservesProofWork; i++)
        {
            if (!regex.IsMatch(later)) throw new InvalidOperationException("Expected a later match.");
        }

        Assert.Equal(dynamicCode, program.TieredProof is not null);
        Assert.True(regex.IsMatch(hit));
        Assert.False(regex.IsMatch(rejected));
        Assert.Equal(expected, regex.Match(later));
        Assert.Equal(2, regex.Count(Encoding.UTF8.GetBytes(value + " " + value)));
        using var cursor = regex.CreateMatchCursor(later);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(expected, match.Value);
        if (captures != 0) Assert.Equal(captures, match.GetCaptures(1).ToArray().Length);
    }

    [Theory]
    [InlineData(@"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])",
        "012.200.033.199", "012.200.033.1AA")]
    [InlineData(@"(\d{4}[- ]){3}\d{3,4}", "𝟙2٣4-5678-1234-𝟜5٦", "1234-5678-1234-45X")]
    public void OrdinaryCountTiersUpFromItsOwnCandidateWork(string pattern, string value, string miss)
    {
        const int expected = 256;
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat($"{miss}!é😀 {value};", expected)));
        using var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var dynamicCode = RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
        Assert.Null(program.TieredProof);

        using var source = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input, source.Token));
        Assert.Null(program.TieredProof);
        for (var i = 0; i < 256 && program.ObservesProofWork; i++)
        {
            if (regex.Count(input) != expected) throw new InvalidOperationException("The count changed during tier-up.");
        }
        Assert.Equal(dynamicCode, program.TieredProof is not null);
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expected, regex.Count(input, source.Token));
        Assert.Equal(expected, regex.Count(input.AsSpan(0, input.Length - 1)));
        source.Cancel();
        Assert.Equal(source.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
    }

    [Fact]
    public void OrdinaryHistoryCursorTiersUpAfterPublishingInterpretedCaptures()
    {
        using var regex = new Utf8Regex(@"(\d{4}[- ]){3}\d{3,4}");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var dynamicCode = RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
        Assert.Equal(dynamicCode, program.ObservesProofWork);
        Assert.Null(program.TieredProof);

        var warmup = (Utf8SliceProgram.ProofTierThreshold + program.Instructions.Length - 1) / program.Instructions.Length;
        const string record = "𝟙2٣4-5678-1234-𝟜5٦!";
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(record, warmup + 1)));
        var width = Encoding.UTF8.GetByteCount(record);
        using var source = new CancellationTokenSource();
        using (var controlled = regex.CreateMatchCursor(bytes, cancellationToken: source.Token))
        {
            for (var i = 0; i < warmup; i++)
                if (!controlled.MoveNext(out _)) throw new InvalidOperationException("Missing controlled match.");
        }
        Assert.Null(program.TieredProof);

        using var cursor = regex.CreateMatchCursor(bytes);
        Assert.True(cursor.MoveNext(out var first));
        for (var i = 1; i < warmup - 1; i++)
            if (!cursor.MoveNext(out _)) throw new InvalidOperationException("Missing interpreted match.");
        Assert.Null(program.TieredProof);
        Assert.True(cursor.MoveNext(out var boundary));
        var proof = program.TieredProof;
        Assert.Equal(dynamicCode, proof is not null);
        Assert.True(cursor.MoveNext(out var last));
        Assert.False(cursor.MoveNext(out _));

        Utf8CaptureRange[] ranges = [new(0, 9), new(9, 14), new(14, 19)];
        Assert.Equal(ranges, first.GetCaptures(1).ToArray());
        AssertHistory(boundary, (warmup - 1) * width);
        AssertHistory(last, warmup * width);
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
        Assert.Equal(first.Value, regex.Match(bytes, captures: slots));
        Assert.Equal(ranges[^1], slots[1]);
        Assert.Same(proof, program.TieredProof);

        void AssertHistory(Utf8RangeMatch match, int start)
        {
            Assert.Equal(new Utf8CaptureRange(start, start + width - 1), match.Value);
            Assert.Equal(ranges.Select(range => new Utf8CaptureRange(start + range.StartOffsetInBytes,
                start + range.EndOffsetInBytes)), match.GetCaptures(1).ToArray());
        }
    }

    [Fact]
    public void FixedDecimalFollowerCountPreservesOrderedScalarProgress()
    {
        using var regex = new Utf8Regex(@"\d{2}[.-]\d{2}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("012.3456-78!٧٨.𝟡٠𝟜١-٢3!123.X!", 32)) + "0000-45");
        using var source = new CancellationTokenSource();
        Assert.Equal(129, regex.Count(input));
        Assert.Equal(129, regex.Count(input, source.Token));
        Assert.Equal(128, regex.Count(input.AsSpan(0, input.Length - 1)));
        Assert.Equal(1, regex.Count("0000-45"u8));
    }

    [Fact]
    public void FixedDecimalFollowerSearchPublishesLaterCapturesAndRetainsTheirHistory()
    {
        using var regex = new Utf8Regex(@"(\d{4}[- ]){3}\d{3,4}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var prefix = string.Concat(Enumerable.Repeat("1234-5678-1234-45X!a ", 16));
        var item = "𝟙2٣4-5678-1234-𝟜5٦";
        var bytes = Encoding.UTF8.GetBytes(prefix + item + "!" + prefix + item);
        var first = Encoding.UTF8.GetByteCount(prefix);
        var last = first + Encoding.UTF8.GetByteCount(item);
        var firstClose = first + Encoding.UTF8.GetByteCount("𝟙2٣4-");
        var expected = new Utf8CaptureRange(first, last);
        var expectedHistory = new Utf8CaptureRange[] { new(first, firstClose), new(firstClose, firstClose + 5), new(firstClose + 5, firstClose + 10) };
        var slots = new Utf8CaptureRange[2];
        Assert.True(regex.IsMatch(bytes));
        Assert.Equal(expected, regex.Match(bytes));
        Assert.Equal(new Utf8CaptureRange(last + 1 + first, bytes.Length), regex.Match(bytes, startOffsetInBytes: last + 1));
        Assert.False(regex.IsMatch(bytes.AsSpan(0, last - 2)));
        Assert.Equal(expected, regex.Match(bytes, captures: slots));
        Assert.Equal(expectedHistory[^1], slots[1]);
        using var source = new CancellationTokenSource();
        Assert.Equal(expected, regex.Match(bytes, captures: slots, cancellationToken: source.Token));
        Assert.False(regex.Match(bytes, captures: slots, endOffsetInBytes: last - 2).Success);

        Utf8RangeMatch retained;
        using (var cursor = regex.CreateMatchCursor(bytes))
        {
            Assert.True(cursor.MoveNext(out retained));
            Assert.True(cursor.MoveNext(out var later));
            Assert.Equal(new Utf8CaptureRange(last + 1 + first, bytes.Length), later.Value);
            Assert.False(cursor.MoveNext(out _));
        }
        Assert.Equal(expected, retained.Value);
        Assert.Equal(expectedHistory, retained.GetCaptures(1).ToArray());
    }

    [Fact]
    public void EmittedHistoryPublishesSparseGroupsAndLeavesRejectedOutputAlone()
    {
        using var regex = new Utf8Regex(@"(?<7>(?<2>\d{2}[.-])(?<9>\d{2}))", RegexOptions.Compiled);
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var dynamicCode = RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled;
        Assert.Equal(dynamicCode, program.EmittedHistoryOffsets is not null);
        var bytes = "é!𝟙2-٣4!56.78"u8.ToArray();
        var expected = new[] { (2, 3, 9), (7, 3, 12), (9, 9, 12) };
        using var source = new CancellationTokenSource();
        using var cursor = regex.CreateMatchCursor(bytes, startOffsetInBytes: 2);
        Assert.True(cursor.MoveNext(out var first));
        // A per-advance token selects the controlled path on the same cursor.
        Assert.True(cursor.MoveNext(out var second, source.Token));
        Assert.Equal(new Utf8CaptureRange(13, 18), second.Value);
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal([0, 2, 7, 9], first.GroupNumbers.ToArray());
        Assert.Equal(new Utf8CaptureRange(3, 12), first.Value);
        foreach (var (number, start, end) in expected)
            Assert.Equal(new Utf8CaptureRange(start, end), Assert.Single(first.GetCaptures(number).ToArray()));

        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[5];
        var sentinel = new Utf8CaptureRange(42, 43);
        slots.Fill(sentinel);
        Assert.Equal(first.Value, regex.Match(bytes, startOffsetInBytes: 2, captures: slots[..2], endOffsetInBytes: 12));
        Assert.Equal(new Utf8CaptureRange(3, 9), slots[1]);
        Assert.Equal(sentinel, slots[2]);
        Assert.Equal(first.Value, regex.Match(bytes, captures: slots));
        for (var slot = 1; slot <= expected.Length; slot++)
            Assert.Equal(new Utf8CaptureRange(expected[slot - 1].Item2, expected[slot - 1].Item3), slots[slot]);
        Assert.Equal(sentinel, slots[^1]);
        Assert.False(regex.Match(bytes, captures: slots, endOffsetInBytes: 11).Success);
        foreach (var slot in slots[..^1]) Assert.Equal(default, slot);
        Assert.Equal(sentinel, slots[^1]);

        if (program.EmittedMatch is { } proof)
        {
            Span<Utf8CaptureRange> storage = stackalloc Utf8CaptureRange[3];
            storage.Fill(sentinel);
            Assert.Equal(-1, proof("𝟙2-X4"u8, storage));
            foreach (var capture in storage) Assert.Equal(sentinel, capture);
            Assert.Equal(9, proof(bytes.AsSpan(3), storage));
            for (var slot = 0; slot < expected.Length; slot++)
                Assert.Equal(new Utf8CaptureRange(expected[slot].Item2 - 3, expected[slot].Item3 - 3), storage[slot]);
        }
    }

    [Theory]
    [InlineData("a12", 1)]
    [InlineData("Ab1 2", 2)]
    [InlineData("aX", 0)]
    [InlineData("Abc12", -1)]
    [InlineData("Aé12", -1)]
    [InlineData("a1  2", -1)]
    public void EmittedSingleEndpointRepeatKeepsTheOriginalForkOrigin(string text, int letters)
    {
        using var regex = new Utf8Regex(@"\A(?:(?<letters>[A-Za-z]{1,2})[0-9] ?[0-9]|[A-Za-z]X)\z",
            RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(letters >= 0, regex.IsMatch(input));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.Equal(letters >= 0, cursor.MoveNext(out var match));
        if (letters < 0) return;
        Assert.Equal(new Utf8CaptureRange(0, input.Length), match.Value);
        if (letters == 0) Assert.Empty(match.GetCaptures(1).ToArray());
        else Assert.Equal(new Utf8CaptureRange(0, letters), Assert.Single(match.GetCaptures(1).ToArray()));
    }

    [Theory]
    [InlineData("255", 0, 3, 1)]
    [InlineData("é😀:00 200 255 099", 7, 9, 4)]
    [InlineData("2é255", 3, 6, 1)]
    [InlineData("!1é2😀", -1, -1, 0)]
    public void EmittedCandidateProgramReturnsAbsoluteByteOffsets(string text, int start, int end, int count)
    {
        using var regex = new Utf8Regex(@"(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])", RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(count > 0, regex.IsMatch(input));
        Assert.Equal(count, regex.Count(input));
        var match = regex.Match(input);
        Assert.Equal(count > 0, match.Success);
        if (match.Success) Assert.Equal(new Utf8CaptureRange(start, end), match);
    }

    [Theory]
    [InlineData("012.200.033.199", 0, 15)]
    [InlineData("é😀:3256.33.44.0559", 9, 21)]
    [InlineData("2é:01.02.03.04", 4, 15)]
    [InlineData("012.200.033.1AA", -1, -1)]
    [InlineData("25é.01.02.03", -1, -1)]
    public void EmittedDelimitedAlternativesKeepCandidateOrder(string text, int start, int end)
    {
        const string octet = "(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        using var regex = new Utf8Regex(@"(?:" + octet + @"\.){3}" + octet, RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(start >= 0, regex.IsMatch(input));
        Assert.Equal(start < 0 ? 0 : 1, regex.Count(input));
        var match = regex.Match(input);
        Assert.Equal(start >= 0, match.Success);
        if (match.Success) Assert.Equal(new Utf8CaptureRange(start, end), match);
    }

    [Fact]
    public void EmittedDecimalRunsKeepByteRangesAndRepeatedCaptures()
    {
        using var regex = new Utf8Regex(@"(\d{4}[- ]){3}\d{3,4}", RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        const string prefix = "😀:";
        const string first = "𝟙2٣4-";
        const string value = first + "5678-1234-𝟜5٦";
        var input = Encoding.UTF8.GetBytes(prefix + value + "!");
        var start = Encoding.UTF8.GetByteCount(prefix);
        var end = start + Encoding.UTF8.GetByteCount(value);
        var firstEnd = start + Encoding.UTF8.GetByteCount(first);
        var expected = new Utf8CaptureRange(start, end);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(expected, regex.Match(input));
        Assert.Equal(1, regex.Count(input));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(expected, match.Value);
        Assert.Equal(new Utf8CaptureRange[]
        {
            new(start, firstEnd), new(firstEnd, firstEnd + 5), new(firstEnd + 5, firstEnd + 10)
        }, match.GetCaptures(1).ToArray());
    }

    [Fact]
    public void EmittedUnboundedDecimalRunsRespectTheShortExtent()
    {
        using var regex = new Utf8Regex(@"^[-+]?\d*\.?\d*$", RegexOptions.Compiled);
        var program = Assert.IsType<Utf8SliceProgram>(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled,
            program.EmittedMatch is not null);

        Assert.Equal(new Utf8CaptureRange(0, 0), regex.Match(""u8));
        Assert.Equal(new Utf8CaptureRange(0, 1), regex.Match("+"u8));
        Assert.Equal(new Utf8CaptureRange(0, 2), regex.Match("+."u8));
        Assert.Equal(new Utf8CaptureRange(0, 8), regex.Match("-3.14159\n"u8));
        Assert.Equal(new Utf8CaptureRange(0, 7), regex.Match("𝟛.١\n"u8));
        Assert.False(regex.IsMatch("+.1.2"u8));
        Assert.False(regex.IsMatch("٠.²"u8));

        foreach (var length in new[] { 31, 32, 33, 512 })
        {
            var digits = new string('1', length);
            Assert.Equal(new Utf8CaptureRange(0, length), regex.Match(Encoding.UTF8.GetBytes(digits + "\n")));
            Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(digits + "x")));
        }
    }

    [Theory]
    [InlineData("abc|xyz", "é😀 xyz abc", 2)]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "é😀 class Bravo", 1)]
    [InlineData("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", "2026-10-26", 1)]
    [InlineData("^ab[0-9]{2}$", "ab12", 1)]
    [InlineData("(?<x>😀)+\\k<x>", "é😀😀", 1)]
    [InlineData("(?<x>.)", "é😀", 2)]
    public void PublicResultsAndControlsDoNotRequireEmission(string pattern, string text, int count)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        {
            if (pattern.Contains("\\k") && mode is RegexOptions.NonBacktracking or RegexOptions.RightToLeft) continue;
            var options = mode | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(options, regex.Options);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(count, regex.Count(input));
            var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.True(match.Success);
            Assert.Equal(Encoding.UTF8.GetString(match.GetGroupMemory(0).Span), System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span));
            if (pattern.Contains("?<x>"))
                Assert.Equal(pattern == "(?<x>.)" && mode != RegexOptions.RightToLeft ? "é" : "😀",
                    System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.Equal(canceled.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Count(input, canceled.Token)).CancellationToken);
            var finite = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = 1_000_000,
                    MaxDepth = 2048,
                    MaxWorkingBytes = 1_048_576,
                    MaxResultBytes = 1_048_576
                }
            });
            Assert.Equal(count, finite.Count(input));
        }
    }

}
