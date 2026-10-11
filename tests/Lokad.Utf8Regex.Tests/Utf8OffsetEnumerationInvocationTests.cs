using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OffsetEnumerationInvocationTests
{
    private static readonly string[] Patterns = [@"(ab)\1", @"(?<=a+)b", @"(?<x>a)+\k<x>", @"(?:(?<x>a)|b)+",
        @"(a|b)*c", @"(?i:a+)", "abc", "é", "(?:ab|xy)", "(?i:ab|xy)", "a.{0,4}b",
        "(?:get|set)[A-Z][A-Za-z0-9_]*", "ab[0-9]{1,2}d", "^abc", "abc$", @"abc\z", "", "(?:|ab)", @"\G(ab)", @"(?=(.))"];
    private static Utf8PreparedRegex Prepared(Utf8Regex regex) =>
        (Utf8PreparedRegex)typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    public static IEnumerable<object[]> Modes()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var finite in new[] { false, true })
        foreach (var live in new[] { false, true }) yield return [pattern, compiled, rtl, finite, live];
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void EveryFloorRetainsFullOriginEmptyProgressionAndSupplementaryCoordinates(string pattern, bool compiled, bool rtl, bool finite, bool live)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex(pattern, options, finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout);
        var oracle = new Regex(pattern, options);
        using var source = new CancellationTokenSource();
        foreach (var text in new[] { "", "abc abab aaab aabc ab1d getAlpha setBeta abc\n", "é😀 abc abab 中aaab aabc ab12d getAlpha 😀abc", "abc\nabc\r\nabc" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var projection = ProjectFixtureSubject(text);
            for (var floor = 0; floor <= text.Length; floor++)
            {
                var scalarFloor = Array.IndexOf(projection.Utf16Boundaries, floor);
                if (scalarFloor < 0)
                {
                    var failure = Assert.Throws<ArgumentOutOfRangeException>(() =>
                    {
                        _ = live ? regex.EnumerateMatchesFromUtf16Offset(input, floor, source.Token)
                            : regex.EnumerateMatchesFromUtf16Offset(input, floor);
                    });
                    Assert.Equal("utf16Offset", failure.ParamName);
                    continue;
                }
                var cursor = live ? regex.EnumerateMatchesFromUtf16Offset(input, floor, source.Token) : regex.EnumerateMatchesFromUtf16Offset(input, floor);
                foreach (Match expected in oracle.Matches(projection.Text, scalarFloor))
                {
                    Assert.True(cursor.MoveNext());
                    var end = expected.Index + expected.Length;
                    Assert.True(cursor.Current.IsByteAligned);
                    Assert.True(cursor.Current.TryGetByteRange(out var index, out var length));
                    Assert.Equal((projection.ByteBoundaries[expected.Index], projection.ByteBoundaries[end] - projection.ByteBoundaries[expected.Index]),
                        (index, length));
                    Assert.Equal((projection.Utf16Boundaries[expected.Index], projection.Utf16Boundaries[end] - projection.Utf16Boundaries[expected.Index]),
                        (cursor.Current.IndexInUtf16, cursor.Current.LengthInUtf16));
                }
                Assert.False(cursor.MoveNext());
                Assert.False(cursor.MoveNext());
            }
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    // This fixture's grammar has ASCII classes, literals (plus é), dot, captures
    // and assertions, but no Unicode properties, supplementary literals or ranges.
    // Its only supplementary subject character is the non-word emoji. Replacing
    // that scalar by an unused non-word BMP sentinel gives an independent BCL
    // comparator for this restricted grammar, including global empty progression.
    // Do not use this projection for arbitrary Unicode classes or casing.
    private static (string Text, int[] Utf16Boundaries, int[] ByteBoundaries) ProjectFixtureSubject(string text)
    {
        var projected = new StringBuilder();
        var utf16 = new List<int> { 0 };
        var bytes = new List<int> { 0 };
        foreach (var scalar in text.EnumerateRunes())
        {
            Assert.NotEqual(0xe000, scalar.Value);
            if (!scalar.IsBmp) Assert.Equal(0x1f600, scalar.Value);
            projected.Append(scalar.IsBmp ? (char)scalar.Value : '\ue000');
            utf16.Add(utf16[^1] + scalar.Utf16SequenceLength);
            bytes.Add(bytes[^1] + scalar.Utf8SequenceLength);
        }
        return (projected.ToString(), utf16.ToArray(), bytes.ToArray());
    }

    public static IEnumerable<object[]> ScalarOffsetWitnesses()
    {
        // Byte ranges are specified directly, independently of the projection.
        (string Pattern, string Text, int Floor, bool Rtl, int[] Ranges)[] rows =
        [
            ("", "a😀b", 0, false, [0, 0, 1, 0, 5, 0, 6, 0]),
            ("", "a😀b", 3, false, [5, 0, 6, 0]),
            ("", "a😀b", 4, true, [6, 0, 5, 0, 1, 0, 0, 0]),
            ("", "a😀b", 1, true, [1, 0, 0, 0]),
            ("a.{0,4}b", "a😀b", 0, false, [0, 6]),
            ("a.{0,4}b", "a😀b", 4, true, [0, 6]),
            (@"(?=(.))", "a😀b", 0, false, [0, 0, 1, 0, 5, 0]),
            (@"(?=(.))", "a😀b", 4, true, [5, 0, 1, 0, 0, 0]),
            (@"\G(ab)", "😀abab😀", 2, false, [4, 2, 6, 2]),
            (@"\G(ab)", "😀abab😀", 3, false, []),
            (@"(ab)\1", "😀abab😀", 2, false, [4, 4]),
            (@"(?<=a+)b", "😀aaab😀", 2, false, [7, 1]),
            ("^abc", "😀abc", 2, false, []),
            (@"abc\z", "😀abc", 2, false, [4, 3]),
        ];
        foreach (var row in rows)
        foreach (var compiled in new[] { false, true })
            yield return [row.Pattern, row.Text, row.Floor, row.Rtl, row.Ranges, compiled];
    }

    [Theory]
    [MemberData(nameof(ScalarOffsetWitnesses))]
    public void ScalarOffsetResultsHaveDeclaredByteRanges(string pattern, string text, int floor, bool rtl, int[] ranges, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, options);
        using var source = new CancellationTokenSource();
        var expected = Enumerable.Range(0, ranges.Length / 2).Select(index => (ranges[index * 2], ranges[index * 2 + 1])).ToArray();
        var projection = ProjectFixtureSubject(text);
        var scalarFloor = Array.IndexOf(projection.Utf16Boundaries, floor);
        var projected = new Regex(pattern, options).Matches(projection.Text, scalarFloor)
            .Select(match => (projection.ByteBoundaries[match.Index], projection.ByteBoundaries[match.Index + match.Length] - projection.ByteBoundaries[match.Index])).ToArray();
        Assert.Equal(expected, projected);
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            var cursor = regex.EnumerateMatchesFromUtf16Offset(input, floor, token);
            var actual = new List<(int, int)>();
            while (cursor.MoveNext())
            {
                Assert.True(cursor.Current.IsByteAligned);
                actual.Add((cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
            }
            Assert.Equal(expected, actual);
            Assert.False(cursor.MoveNext());
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    public static IEnumerable<object[]> EntryCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var mixed in new[] { false, true }) yield return [pattern, compiled, mixed];
    }

    [Theory]
    [MemberData(nameof(EntryCases))]
    public void EntryCancellationPrecedesReturnAndUsesTheExactCallerToken(string pattern, bool compiled, bool mixed)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0), TimeSpan.FromSeconds(10));
        var input = Encoding.UTF8.GetBytes(new string(mixed ? 'é' : 'x', 200000));
        using var source = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        var canceled = false;
        try
        {
            try { _ = regex.EnumerateMatchesFromUtf16Offset(input, 1, source.Token); }
            catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); canceled = true; }
            Assert.True(canceled);
        }
        finally { session.Complete(); }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("é")]
    [InlineData("(?:ab|xy)")]
    [InlineData("a.{0,4}b")]
    [InlineData("(?:get|set)[A-Z][A-Za-z0-9_]*")]
    [InlineData("ab[0-9]{1,2}d")]
    [InlineData("^abc")]
    [InlineData("abc$")]
    [InlineData(@"abc\z")]
    public void QualifiedForwardCursorsRetainNativeRoutesAndRejectInteriorStarts(string pattern)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
            var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
            var ascii = regex.EnumerateMatchesFromUtf16Offset("abc ab1d getAlpha"u8, 1);
            Assert.False(ascii.DebugIsOwnedSlice);
            var mixed = Encoding.UTF8.GetBytes("é😀 abc ab1d getAlpha");
            var failure = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = regex.EnumerateMatchesFromUtf16Offset(mixed, 2); });
            Assert.Equal("utf16Offset", failure.ParamName);
            var forward = regex.EnumerateMatchesFromUtf16Offset(mixed, 3);
            while (forward.MoveNext()) Assert.True(forward.Current.IsByteAligned);
            var reverse = new Utf8Regex(pattern, options | RegexOptions.RightToLeft).EnumerateMatchesFromUtf16Offset(mixed, 3);
            Assert.True(reverse.DebugIsOwnedSlice);
            using var source = new CancellationTokenSource();
            var token = regex.EnumerateMatchesFromUtf16Offset("abc ab1d getAlpha"u8, 1, source.Token);
            Assert.False(token.DebugIsOwnedSlice);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("(?:|ab)")]
    [InlineData(@"(?=(.))")]
    [InlineData(@"\G(ab)")]
    public void ZeroWidthAndAssertionProgramsRetainOwnedScalarCursors(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var cursor = regex.EnumerateMatchesFromUtf16Offset("abab"u8, 1);
        Assert.True(cursor.DebugIsOwnedSlice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidInputAndOffsetKeepTheirExceptionOrderAndPrecancellation(bool live)
    {
        var regex = new Utf8Regex("(ab)", RegexOptions.CultureInvariant);
        using var source = new CancellationTokenSource();
        var token = live ? source.Token : default;
        byte[] malformed = [0xc3, 0xa9, 0xff];
        Assert.Throws<ArgumentException>(() => { _ = regex.EnumerateMatchesFromUtf16Offset(malformed, -1, token); });
        foreach (var offset in new[] { -1, 2, 4, int.MaxValue })
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = regex.EnumerateMatchesFromUtf16Offset("é😀"u8, offset, token); });
            Assert.Equal("utf16Offset", exception.ParamName);
        }
        source.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => { _ = regex.EnumerateMatchesFromUtf16Offset(malformed, -1, source.Token); });
        Assert.Equal(source.Token, canceled.CancellationToken);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("(?:abc|abab)")]
    [InlineData("^abc")]
    [InlineData("abc$")]
    [InlineData(@"abc\z")]
    [InlineData("a.{0,4}b")]
    [InlineData("(ab)\\1")]
    public void DeadlineAndTokenRemainActiveBetweenMovesAndAfterEof(string pattern)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var input = Encoding.UTF8.GetBytes("abc abab ab1d abc");
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
        var cursor = regex.EnumerateMatchesFromUtf16Offset(input, 0);
        Assert.True(cursor.MoveNext());
        var previous = cursor.Current;
        clock.Advance(250);
        var expired = false;
        try { _ = cursor.MoveNext(); }
        catch (RegexMatchTimeoutException exception)
        {
            Assert.Equal(regex.Pattern, exception.Pattern);
            Assert.Equal(regex.MatchTimeout, exception.MatchTimeout);
            Assert.Equal(Encoding.UTF8.GetString(input), exception.Input);
            expired = true;
        }
        Assert.True(expired);
        Assert.Equal(previous, cursor.Current);
        using var source = new CancellationTokenSource();
        var live = new Utf8Regex(pattern, RegexOptions.CultureInvariant).EnumerateMatchesFromUtf16Offset(input, 0, source.Token);
        while (live.MoveNext()) { }
        previous = live.Current;
        source.Cancel();
        var canceled = false;
        try { _ = live.MoveNext(); }
        catch (OperationCanceledException exception) { Assert.Equal(source.Token, exception.CancellationToken); canceled = true; }
        Assert.True(canceled);
        Assert.Equal(previous, live.Current);
    }

    [Theory]
    [InlineData((int)Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, false)]
    [InlineData((int)Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, true)]
    [InlineData((int)Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix, false)]
    [InlineData((int)Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix, true)]
    [InlineData((int)Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffixStrictEnd, false)]
    [InlineData((int)Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffixStrictEnd, true)]
    public void EveryAnchoredVerificationAndProjectionCutoffKeepsFloorResultAndBorrowedStorage(int kindValue, bool mixed)
    {
        var kind = (Utf8FallbackDirectFamilyKind)kindValue;
        var literal = Encoding.UTF8.GetBytes(new string('a', 4097));
        var prefix = kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix;
        var text = prefix ? Encoding.UTF8.GetString(literal) + (mixed ? "é😀" : "xxx") : (mixed ? "é😀" + new string('x', 8193) : new string('x', 8193)) + Encoding.UTF8.GetString(literal) + (kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix ? "\n" : "");
        var input = Encoding.UTF8.GetBytes(text);
        var validation = Utf8Validation.Validate(input);
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.True(baseline.Ledger.TryReserve(7));
        var original = new Utf8OperationMatchCursor(input, literal, kind, validation, baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, original.TryMoveNextAnchoredLiteral(out var found));
        Assert.True(found);
        var expected = new Regex((prefix ? "^" : "") + new string('a', 4097) + (prefix ? "" : kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix ? "$" : "\\z")).Match(text);
        Assert.Equal((expected.Index, expected.Length), (original.CurrentValueMatch.IndexInUtf16, original.CurrentValueMatch.LengthInUtf16));
        for (ulong cap = 1; cap < original.DebugInvocationSteps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            var cursor = new Utf8OperationMatchCursor(input, literal, kind, validation, invocation);
            var previous = cursor.CurrentValueMatch;
            var floor = cursor.DebugConsumed;
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, cursor.TryMoveNextAnchoredLiteral(out found));
            Assert.False(found);
            Assert.Equal(previous, cursor.CurrentValueMatch);
            Assert.Equal(floor, cursor.DebugConsumed);
            Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
        }
        Assert.False(original.MoveNext());
        Assert.False(original.MoveNext());
        Assert.Equal(7UL, original.DebugInvocationReservedBytes);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("é")]
    [InlineData("(?:ab|xy)")]
    [InlineData("a.{0,4}b")]
    [InlineData("(?:get|set)[A-Z][A-Za-z0-9_]*")]
    [InlineData("ab[0-9]{1,2}d")]
    public void NativeFactoryKeepsValidationWorkAndBorrowedAdmission(string pattern)
    {
        var prepared = Prepared(new Utf8Regex(pattern, RegexOptions.CultureInvariant));
        var input = Encoding.UTF8.GetBytes(new string('x', 8193) + "abc ab1d getAlpha é");
        // Structural/window lanes require ASCII; Unicode literals retain mixed projection.
        if (pattern.StartsWith("a.", StringComparison.Ordinal) || pattern.StartsWith("(?:get", StringComparison.Ordinal))
            input = Encoding.UTF8.GetBytes(new string('x', 8193) + "abc ab1d getAlpha");
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var validation = Utf8Validation.Validate(input, ref invocation);
        Assert.True(Utf8CompiledOperationCursorFactory.CanCreateInvocationMatchCursor(prepared, validation, ref invocation));
        var steps = invocation.Budget.Steps;
        var cursor = Utf8CompiledOperationCursorFactory.CreateInvocationMatchCursor(prepared, input, validation, invocation, 8193, 8193);
        Assert.True(cursor.DebugInvocationSteps >= steps);
        Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
        Assert.False(cursor.DebugIsOwnedSlice);
        while (cursor.MoveNext()) { }
        Assert.True(cursor.DebugInvocationSteps > steps);
        Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
    }
}
