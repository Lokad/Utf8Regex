using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CompiledScalarConsumerTests
{
    private static Utf8PreparedRegex Prepare(string pattern, RegexOptions options = RegexOptions.CultureInvariant)
        => Utf8FrontEnd.Compile(pattern, options);

    [Theory]
    [InlineData(".+", "é😀a", false, 1, 0, 7, 0, 4)]
    [InlineData("(?<=é).(?=z)", "é😀z", false, 1, 2, 4, 1, 2)]
    [InlineData("\\p{L}{1,2}", "𐐀é", false, 1, 0, 6, 0, 3)]
    [InlineData("\\p{L}", "𐐀é", false, 2, 0, 4, 0, 2)]
    [InlineData("\\p{L}{0,2}", "𐐀é", false, 2, 0, 6, 0, 3)]
    [InlineData("a[0-9]{2}", "é😀 a12", false, 1, 7, 3, 4, 3)]
    [InlineData("a.b", "é a😀b", false, 1, 3, 6, 2, 4)]
    [InlineData("a.{0,4}b", "xa😀b tail", false, 1, 1, 6, 1, 4)]
    [InlineData("\\b(?:record|struct|class)\\s+[A-Z][A-Za-z0-9_]+", "é😀 class Bravo", true, 1, 7, 11, 4, 11)]
    [InlineData("\\b(?:record|struct|class)\\s+[A-Z][A-Za-z0-9_]+", "é😀 class Bravo", false, 1, 7, 11, 4, 11)]
    public void PublicOperationsKeepScalarByteRangesAndDerivedCoordinates(string pattern,
        string text, bool rtl, int count, int indexBytes, int lengthBytes, int indexUnits, int lengthUnits)
    {
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0);
        var input = Encoding.UTF8.GetBytes(text);
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(count, regex.Count(input));
            var match = regex.Match(input);
            Assert.True(match.Success); Assert.True(match.Success);
            Assert.Equal((indexBytes, lengthBytes), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        }
    }

    [Theory]
    [InlineData("import \"abc\"\n", 1, true)]
    [InlineData("import \"😀é\"\n", 1, false)]
    [InlineData("import \"abc\"\nimport shared \"😀é\"\n", 2, false)]
    public void PublicQuotedLinesKeepCompleteScalarSubjects(string text, int expected, bool isAscii)
    {
        const string pattern = "^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$";
        const RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Multiline;
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(isAscii, Utf8Validation.Validate(input).IsAscii);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options | mode);
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected != 0, regex.IsMatch(input));
            Assert.Equal(expected != 0, Utf8MatchSequence.First(regex.CreateMatchCursor(input)).Success);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralCompiledCursorOwnsReferencesAndEmptyScalarAdvancement(bool rtl)
    {
        var pattern = rtl ? @"\k<x>(?<x>.)|$" : @"(?<x>.)\k<x>|$";
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0),
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 } });
        var input = "é😀😀z"u8.ToArray();
        var cursor = regex.EnumerateMatches(input);
        var sibling = cursor;
        var ranges = new List<(int, int)>();
        while (cursor.MoveNext())
            ranges.Add((cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes));
        Assert.Equal(rtl ? [(11, 0), (2, 8)] : new List<(int, int)> { (2, 8), (11, 0) }, ranges);
        Assert.Equal(cursor.DebugInvocationSteps, sibling.DebugInvocationSteps);
        Assert.True(sibling.MoveNext());
        Assert.Equal(ranges[0], (sibling.Current.StartOffsetInBytes, sibling.Current.EndOffsetInBytes - sibling.Current.StartOffsetInBytes));
        while (sibling.MoveNext()) { }
        Assert.Equal(cursor.DebugInvocationSteps, sibling.DebugInvocationSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralCursorOffsetRetainsFullAssertionContextAndNfaStart(bool nonBacktracking)
    {
        var pattern = nonBacktracking ? "😀|z" : @"(?<=é).|z";
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant | (nonBacktracking ? RegexOptions.NonBacktracking : 0),
            policy: new() { ResourcePolicy = new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 } });
        var input = "é😀z"u8.ToArray();
        var cursor = regex.EnumerateMatches(input, startOffsetInBytes: 2);
        var sibling = cursor;
        Assert.True(cursor.MoveNext());
        Assert.Equal((2, 6), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.True(cursor.MoveNext());
        Assert.Equal((6, 7), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.False(cursor.MoveNext());
        Assert.Equal(cursor.DebugInvocationSteps, sibling.DebugInvocationSteps);
        Assert.True(sibling.MoveNext());
        Assert.Equal((2, 6), (sibling.Current.StartOffsetInBytes, sibling.Current.EndOffsetInBytes));
        while (sibling.MoveNext()) { }
        Assert.Equal(cursor.DebugInvocationSteps, sibling.DebugInvocationSteps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralSplitFactoryUsesScalarDelimitersWithoutDecodedSubject(bool rtl)
    {
        var prepared = Prepare("(?<d>😀)(?=.)", RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0));
        var input = "é😀x😀z"u8.ToArray();
        var cursor = Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(prepared, input, int.MaxValue, Utf8ExecutionDeadline.Infinite);
        var pieces = new List<string>();
        while (cursor.MoveNext()) pieces.Add(Encoding.UTF8.GetString(cursor.Current.GetValueBytes()));
        Assert.Equal(rtl ? new[] { "z", "x", "é" } : ["é", "x", "z"], pieces);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PublicScalarOperationsRespectAndRefundWorkingAllowance(int operation)
    {
        using var regex = new Utf8Regex(@"(?<x>.)\k<x>");
        var input = "😀😀"u8.ToArray();
        var working = new WorkingAccount(1 << 20);
        Assert.True(working.TryReserve(7));
        Assert.Equal(operation == 2 ? 8 : 1, Run(operation, regex, input, working, default));
        Assert.True(working.Peak > 7);
        Assert.Equal(7ul, working.Used);

        var denying = new WorkingAccount(8);
        Assert.True(denying.TryReserve(7));
        Assert.Throws<Utf8MemoryAdmissionException>(() => Run(operation, regex, input, denying, default));
        Assert.Equal(7ul, denying.Used);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public void PublicScalarOperationsKeepEntryInterruptionAuthority(int operation, bool expire)
    {
        using var regex = new Utf8Regex(@"(?<x>.)\k<x>", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(100)
        });
        var input = "😀😀"u8.ToArray();
        var working = new WorkingAccount(1 << 20);
        Assert.True(working.TryReserve(7));
        using var source = new CancellationTokenSource();
        var readCount = 0;
        var interruptAt = int.MaxValue;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++readCount != interruptAt)
                return 0;
            if (expire)
                return 1000;
            source.Cancel();
            return 0;
        });
        Assert.Equal(operation == 2 ? 8 : 1, Run(operation, regex, input, working, source.Token));
        Assert.True(readCount > 1);
        interruptAt = readCount;
        readCount = 0;
        if (expire)
        {
            var failure = Assert.Throws<RegexMatchTimeoutException>(() =>
                Run(operation, regex, input, working, source.Token));
            Assert.Equal(regex.MatchTimeout, failure.MatchTimeout);
        }
        else
        {
            var failure = Assert.Throws<OperationCanceledException>(() =>
                Run(operation, regex, input, working, source.Token));
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        Assert.Equal(7ul, working.Used);
    }

    private static int Run(int operation, Utf8Regex regex, byte[] input, WorkingAccount working, CancellationToken token)
        => operation switch
        {
            0 => regex.IsMatch(input, token, working) ? 1 : 0,
            1 => regex.Count(input, token, working),
            _ => regex.Match(input, cancellationToken: token, workingMemoryAllowance: working).EndOffsetInBytes,
        };

    private sealed class WorkingAccount(ulong capacity) : IUtf8MemoryAllowance
    {
        public ulong Used { get; private set; }
        public ulong Peak { get; private set; }

        public bool TryReserve(ulong bytes)
        {
            if (bytes > capacity - Used) return false;
            Used += bytes;
            Peak = Math.Max(Peak, Used);
            return true;
        }

        public void Release(ulong bytes)
        {
            Assert.True(bytes <= Used);
            Used -= bytes;
        }
    }

    [Theory]
    [InlineData("(?<x>.)", "é😀a", "!!!", 3)]
    [InlineData(".*", "😀a", "!!", 2)]
    public void ReverseCursorReplacementEmitsSubjectOrderIncludingTiedEmptyRanges(string pattern, string text, string expected, int count)
    {
        var prepared = Prepare(pattern, RegexOptions.CultureInvariant | RegexOptions.RightToLeft);
        var input = Encoding.UTF8.GetBytes(text);
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(prepared, input, Utf8ExecutionDeadline.Infinite);
        Assert.True(cursor.IsRightToLeft);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), Utf8CursorReplaceEngine.Replace(input, "!"u8, ref cursor, out var replaced));
        Assert.Equal(count, replaced);
        cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(prepared, input, Utf8ExecutionDeadline.Infinite);
        var output = new byte[expected.Length];
        Assert.True(Utf8CursorReplaceEngine.TryReplace(input, "!"u8, output, ref cursor, out var written));
        Assert.Equal(output.Length, written); Assert.Equal(Encoding.UTF8.GetBytes(expected), output);
    }
}
