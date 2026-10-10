using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCountTests
{
    public static IEnumerable<object[]> ScalarCases()
    {
        (string Pattern, string Subject, int Count)[] rows =
        [
            ("", "", 1), ("", "😀", 2), ("", "é中😀", 4), ("(?:)", "a😀b", 4),
            (".", "a😀b", 3), ("(?s:.)", "a😀\nb", 4),
            (".*", "a😀b", 2), (".*?", "a😀b", 4), (".?", "a😀b", 4),
            (@"[\u0000-\uffff]*", "a😀b", 4), ("[^a]*", "a😀b", 3),
            ("😀*", "a😀b", 4), (@"(.)\1", "😀😀a", 1),
            (@"\G", "a😀b", 1), ("(?<=x).", "x😀a", 1),
            ("😀|a", "a😀b", 2), (@"\w", "😀𐐀é", 2),
        ];
        foreach (var row in rows)
        foreach (var compiled in new[] { false, true })
        foreach (var reverse in new[] { false, true })
        foreach (var live in new[] { false, true })
            yield return [row.Pattern, row.Subject, row.Count, compiled, reverse, live];
    }

    [Theory]
    [MemberData(nameof(ScalarCases))]
    public void CountUsesCompleteScalars(string pattern, string subject, int expected,
        bool compiled, bool reverse, bool live)
    {
        // Mirrored concatenation must execute the capture before its reference.
        if (reverse && pattern == @"(.)\1") pattern = @"\1(.)";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0)
            | (reverse ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(subject);
        using var source = new CancellationTokenSource();
        Assert.Equal(expected, live ? regex.Count(input, source.Token) : regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void EmptyCountFromUtf16StartAcceptsOnlyScalarBoundaries(bool compiled, bool reverse)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0)
            | (reverse ? RegexOptions.RightToLeft : 0);
        var regex = new Utf8Regex("", options);
        var input = Encoding.UTF8.GetBytes("x😀𐐀y");
        int[] starts = [0, 1, 3, 5, 6];
        using var source = new CancellationTokenSource();
        for (var index = 0; index < starts.Length; index++)
        {
            var expected = reverse ? index + 1 : starts.Length - index;
            Assert.Equal(expected, regex.CountFromUtf16Offset(input, starts[index]));
            Assert.Equal(expected, regex.CountFromUtf16Offset(input, starts[index], source.Token));
        }
        foreach (var interior in new[] { 2, 4 })
            Assert.Throws<ArgumentOutOfRangeException>(() => regex.CountFromUtf16Offset(input, interior));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void NativeEmptyCounterAndCursorHaveScalarResults(bool compiled, bool finite)
    {
        var regex = new Utf8Regex("", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var runtime = Assert.IsAssignableFrom<Utf8CompiledEngineRuntime>(typeof(Utf8Regex)
            .GetProperty("_compiledEngineRuntime", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(regex));
        Assert.IsType<Utf8ExactLiteralCompiledEngineRuntime>(runtime);
        var input = Encoding.UTF8.GetBytes("é😀𐐀a");
        var validation = Utf8Validation.Validate(input);
        var deadline = finite ? Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(10)) : Utf8ExecutionDeadline.Infinite;
        Assert.Equal(5, runtime.Count(input, validation, deadline));
        var prepared = Assert.IsType<Lokad.Utf8Regex.Internal.Planning.Utf8PreparedRegex>(typeof(Utf8Regex)
            .GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(regex));
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(prepared, input, validation, deadline);
        int[] bytes = [0, 2, 6, 10, 11];
        int[] units = [0, 1, 3, 5, 6];
        for (var index = 0; index < bytes.Length; index++)
        {
            Assert.True(cursor.MoveNext());
            Assert.True(cursor.CurrentValueMatch.IsByteAligned);
            Assert.Equal(bytes[index], cursor.CurrentValueMatch.IndexInBytes);
            Assert.Equal(units[index], cursor.CurrentValueMatch.IndexInUtf16);
        }
        Assert.False(cursor.MoveNext());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OwnedCountDoesNotAllocateAWholeSubjectCoordinateMap(bool compiled)
    {
        var regex = new Utf8Regex(@"(?s:.)", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀𐐀a", 20000)));
        Assert.Equal(60000, regex.Count(input));
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(60000, regex.Count(input));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0L, 65536L);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void EmptyLiteralPublicOperationsAreScalarAligned(bool compiled, bool reverse)
        => AssertEmptyScalarOperations(compiled, reverse);

    internal static void AssertEmptyScalarOperations(bool compiled, bool reverse)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0)
            | (reverse ? RegexOptions.RightToLeft : 0);
        AssertEmptyScalarOperations(new Utf8Regex("", options), reverse);
    }

    internal static void AssertEmptyScalarOperations(Utf8Regex regex, bool reverse)
    {
        AssertEmptyScalarSubject(regex, reverse, "", "|", [0], ["", ""]);
        AssertEmptyScalarSubject(regex, reverse, "ab", "|a|b|", [0, 1, 2], ["", "a", "b", ""]);
        AssertEmptyScalarSubject(regex, reverse, "é中😀", "|é|中|😀|", [0, 2, 5, 9], ["", "é", "中", "😀", ""]);
        AssertEmptyScalarSubject(regex, reverse, "é😀𐐀a", "|é|😀|𐐀|a|", [0, 2, 6, 10, 11], ["", "é", "😀", "𐐀", "a", ""]);
    }

    private static void AssertEmptyScalarSubject(Utf8Regex regex, bool reverse,
        string subject, string expected, int[] boundaries, string[] expectedPieces)
    {
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(boundaries.Length, regex.Count(input));
        Assert.Equal(reverse ? input.Length : 0, regex.Match(input).IndexInBytes);
        var detailed = regex.MatchDetailed(input);
        Assert.Equal(1, detailed.GroupCount);
        Assert.Equal(reverse ? input.Length : 0, detailed.IndexInBytes);
        Assert.Equal(0, detailed.LengthInBytes);
        var expectedBytes = reverse ? boundaries.Reverse() : boundaries;
        using var source = new CancellationTokenSource();
        foreach (var live in new[] { false, true })
        {
            var cursor = live ? regex.EnumerateMatches(input, source.Token) : regex.EnumerateMatches(input);
            foreach (var position in expectedBytes)
            {
                Assert.True(cursor.MoveNext());
                Assert.True(cursor.Current.IsByteAligned);
                Assert.Equal(position, cursor.Current.IndexInBytes);
                Assert.Equal(0, cursor.Current.LengthInBytes);
            }
            Assert.False(cursor.MoveNext());
        }
        var pieces = new List<string>();
        foreach (var piece in regex.EnumerateSplits(input)) pieces.Add(piece.GetValueString());
        Assert.Equal(reverse ? expectedPieces.Reverse() : expectedPieces, pieces);
        Assert.Equal(expected, regex.ReplaceToString(input, "|"));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(input, "|"u8));
        var destination = new byte[64];
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "|"u8, destination, out var written));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), destination.AsSpan(0, written).ToArray());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void CountKeepsItsLiveTokenThroughScalarDiscovery(bool compiled, bool foldedLiteral)
    {
        var regex = new Utf8Regex(foldedLiteral ? "Ж" : @"(?s:(.)\1*)", RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : 0) | (foldedLiteral ? RegexOptions.IgnoreCase : 0));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(foldedLiteral ? "ж" : "😀", 32768)));
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 256) return;
            entered = true;
            source.Cancel();
        });
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token));
            Assert.True(entered);
            Assert.Equal(source.Token, error.CancellationToken);
        }
        finally { session.Complete(); }
        Assert.Equal(foldedLiteral ? 2 : 1, regex.Count(foldedLiteral ? "жж"u8 : "😀😀"u8));
    }
}
