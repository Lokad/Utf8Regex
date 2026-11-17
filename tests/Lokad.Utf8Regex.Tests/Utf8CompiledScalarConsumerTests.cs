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

    private static void CollectReleasedCursorStorage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

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
            var regex = new Utf8Regex(pattern, options);
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
            var regex = new Utf8Regex(pattern, options | mode);
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected != 0, regex.IsMatch(input));
            Assert.Equal(expected != 0, Utf8MatchSequence.First(regex.CreateMatchCursor(input)).Success);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralCompiledCursorOwnsReferencesAndEmptyScalarAdvancement(bool rtl)
    {
        var invocation = ExerciseCompiledCursorCopies(rtl, out var retained);
        CollectReleasedCursorStorage();
        Assert.Equal(retained, invocation.Ledger.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8SliceInvocation ExerciseCompiledCursorCopies(bool rtl, out ulong retained)
    {
        var pattern = rtl ? @"\k<x>(?<x>.)|$" : @"(?<x>.)\k<x>|$";
        var prepared = Prepare(pattern, RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0));
        var input = "é😀😀z"u8.ToArray();
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(prepared, input,
            Utf8Validation.Validate(input), Utf8ExecutionDeadline.Infinite);
        Assert.True(cursor.DebugIsOwnedSlice);
        retained = cursor.DebugInvocationReservedBytes;
        var invocation = cursor.ExportWorkingInvocation();
        var sibling = cursor;
        var ranges = new List<(int, int)>();
        while (cursor.MoveNext())
        {
            Assert.True(cursor.CurrentValueMatch.Success);
            ranges.Add((cursor.CurrentValueMatch.StartOffsetInBytes, (cursor.CurrentValueMatch.EndOffsetInBytes - cursor.CurrentValueMatch.StartOffsetInBytes)));
        }
        Assert.Equal(rtl ? [(11, 0), (2, 8)] : new List<(int, int)> { (2, 8), (11, 0) }, ranges);
        CollectReleasedCursorStorage();
        Assert.Equal(retained, cursor.DebugInvocationReservedBytes);
        Assert.True(sibling.MoveNext());
        Assert.Equal(ranges[0], (sibling.CurrentValueMatch.StartOffsetInBytes, (sibling.CurrentValueMatch.EndOffsetInBytes - sibling.CurrentValueMatch.StartOffsetInBytes)));
        while (sibling.MoveNext()) { }
        // Return only the ledger owner. All cursor and temporary table holders
        // leave this scope, but published credits remain on the invocation.
        return invocation;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralCursorOffsetRetainsFullAssertionContextAndNfaStart(bool nonBacktracking)
    {
        var invocation = ExerciseOffsetCursorCopies(nonBacktracking, out var retained);
        CollectReleasedCursorStorage();
        Assert.Equal(retained, invocation.Ledger.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8SliceInvocation ExerciseOffsetCursorCopies(bool nonBacktracking, out ulong retained)
    {
        var pattern = nonBacktracking ? "😀|z" : @"(?<=é).|z";
        var prepared = Prepare(pattern, RegexOptions.CultureInvariant | (nonBacktracking ? RegexOptions.NonBacktracking : 0));
        var input = "é😀z"u8.ToArray();
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(prepared, input, Utf8Validation.Validate(input), Utf8ExecutionDeadline.Infinite, 2);
        Assert.True(cursor.DebugIsOwnedSlice);
        retained = cursor.DebugInvocationReservedBytes;
        var invocation = cursor.ExportWorkingInvocation();
        var sibling = cursor;
        Assert.True(cursor.MoveNext()); Assert.Equal((2, 4), (cursor.CurrentValueMatch.StartOffsetInBytes, (cursor.CurrentValueMatch.EndOffsetInBytes - cursor.CurrentValueMatch.StartOffsetInBytes)));
        Assert.True(cursor.MoveNext()); Assert.Equal((6, 1), (cursor.CurrentValueMatch.StartOffsetInBytes, (cursor.CurrentValueMatch.EndOffsetInBytes - cursor.CurrentValueMatch.StartOffsetInBytes)));
        Assert.False(cursor.MoveNext());
        CollectReleasedCursorStorage();
        Assert.Equal(retained, cursor.DebugInvocationReservedBytes);
        Assert.True(sibling.MoveNext());
        Assert.Equal((2, 4), (sibling.CurrentValueMatch.StartOffsetInBytes, (sibling.CurrentValueMatch.EndOffsetInBytes - sibling.CurrentValueMatch.StartOffsetInBytes)));
        while (sibling.MoveNext()) { }
        return invocation;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralSplitFactoryUsesScalarDelimitersWithoutDecodedSubject(bool rtl)
    {
        var prepared = Prepare("(?<d>😀)(?=.)", RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0));
        var input = "é😀x😀z"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var cursor = Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(prepared, input,
            validation, int.MaxValue, Utf8ExecutionDeadline.Infinite);
        var pieces = new List<string>();
        while (cursor.MoveNext()) pieces.Add(cursor.Current.GetValueString());
        Assert.Equal(rtl ? new[] { "z", "x", "é" } : ["é", "x", "z"], pieces);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ScalarExecutionSharesBudgetAndRestoresCaptureScratch(int operation)
    {
        var prepared = Prepare(@"(?<x>.)\k<x>");
        var input = "😀😀"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var funded = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(funded.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None, Run(operation, prepared, input, validation, ref funded));
        Assert.Equal(7ul, funded.Ledger.ReservedBytes);
        var limited = new Utf8SliceInvocation(new(funded.Budget.Steps, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.True(limited.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.None, Run(operation, prepared, input, validation, ref limited));
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Run(operation, prepared, input, validation, ref limited));
        Assert.Equal(7ul, limited.Ledger.ReservedBytes);
        var denied = new Utf8SliceInvocation(new(0, 0, 8), Regex.InfiniteMatchTimeout, default);
        Assert.True(denied.Ledger.TryReserve(7));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, Run(operation, prepared, input, validation, ref denied));
        Assert.Equal(7ul, denied.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ScalarExecutionCancellationKeepsEntryTokenAndReleasesScratch(int operation)
    {
        var prepared = Prepare("(?<x>a+)b");
        var input = Encoding.UTF8.GetBytes(new string('a', 2048) + "b");
        var validation = Utf8Validation.Validate(input);
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; source.Cancel(); };
        OperationCanceledException? failure = null;
        try { _ = Run(operation, prepared, input, validation, ref invocation); }
        catch (OperationCanceledException cancelled) { failure = cancelled; }
        Assert.True(entered); Assert.NotNull(failure); Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ScalarOperationsUseThePassedEntryDeadline(int operation)
    {
        const string pattern = @"(?<=é).";
        var prepared = Prepare(pattern);
        var input = "é😀"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1));
        Thread.Sleep(20);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
        {
            if (operation == 0) _ = Utf8PreparedScalarExecutor.IsMatch(prepared, input, validation, deadline);
            else if (operation == 1) _ = Utf8PreparedScalarExecutor.Count(prepared, input, validation, deadline);
            else _ = Utf8PreparedScalarExecutor.Match(prepared, input, validation, deadline);
        });
    }

    private static Utf8ExecutionLimitKind Run(int operation, Utf8PreparedRegex prepared, byte[] input,
        Utf8ValidationResult validation, ref Utf8SliceInvocation invocation)
        => operation == 0 ? Utf8PreparedScalarExecutor.TryIsMatch(prepared, input, validation, ref invocation, out _)
            : operation == 1 ? Utf8PreparedScalarExecutor.TryCount(prepared, input, validation, ref invocation, out _)
            : Utf8PreparedScalarExecutor.TryMatch(prepared, input, validation, ref invocation, out _);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    public void CancellationAtFinalPublicationCannotPublishAResult(int operation, bool hit)
    {
        var prepared = Prepare("a");
        var input = hit ? "a"u8.ToArray() : "z"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var funded = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Run(operation, prepared, input, validation, ref funded));
        var padding = (64 - (funded.Budget.Steps & 63)) & 63;
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, source.Token);
        for (var step = 0ul; step < padding; step++) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        Assert.True(invocation.Ledger.TryReserve(7));
        var entered = false;
        invocation.CheckpointHook = steps => { if (steps == padding + funded.Budget.Steps) { entered = true; source.Cancel(); } };
        OperationCanceledException? failure = null;
        try { _ = Run(operation, prepared, input, validation, ref invocation); }
        catch (OperationCanceledException cancelled) { failure = cancelled; }
        Assert.True(entered); Assert.NotNull(failure); Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(7ul, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("(?<x>.)", "é😀a", "!!!", 3)]
    [InlineData(".*", "😀a", "!!", 2)]
    public void ReverseCursorReplacementEmitsSubjectOrderIncludingTiedEmptyRanges(string pattern, string text, string expected, int count)
    {
        var prepared = Prepare(pattern, RegexOptions.CultureInvariant | RegexOptions.RightToLeft);
        var input = Encoding.UTF8.GetBytes(text);
        var validation = Utf8Validation.Validate(input);
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(prepared, input, validation, Utf8ExecutionDeadline.Infinite);
        Assert.True(cursor.IsRightToLeft);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), Utf8CursorReplaceEngine.Replace(input, "!"u8, ref cursor, out var replaced));
        Assert.Equal(count, replaced);
        cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(prepared, input, validation, Utf8ExecutionDeadline.Infinite);
        var output = new byte[expected.Length];
        Assert.True(Utf8CursorReplaceEngine.TryReplace(input, "!"u8, output, ref cursor, out var written));
        Assert.Equal(output.Length, written); Assert.Equal(Encoding.UTF8.GetBytes(expected), output);
    }
}
