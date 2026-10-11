using System.Text;
using System.Text.RegularExpressions;
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
    [InlineData((int)Utf8CompiledEngineKind.FallbackRegex, ".+", "é😀a", false, 1, 0, 7, 0, 4)]
    [InlineData((int)Utf8CompiledEngineKind.FallbackRegex, "(?<=é).(?=z)", "é😀z", false, 1, 2, 4, 1, 2)]
    [InlineData((int)Utf8CompiledEngineKind.FallbackRegex, "\\p{L}{1,2}", "𐐀é", false, 1, 0, 6, 0, 3)]
    [InlineData((int)Utf8CompiledEngineKind.FallbackRegex, "\\p{L}", "𐐀é", false, 2, 0, 4, 0, 2)]
    [InlineData((int)Utf8CompiledEngineKind.FallbackRegex, "\\p{L}{0,2}", "𐐀é", false, 2, 0, 6, 0, 3)]
    [InlineData((int)Utf8CompiledEngineKind.SimplePatternInterpreter, "a[0-9]{2}", "é😀 a12", false, 1, 7, 3, 4, 3)]
    [InlineData((int)Utf8CompiledEngineKind.SimplePatternInterpreter, "a.b", "é a😀b", false, 1, 3, 6, 2, 4)]
    [InlineData((int)Utf8CompiledEngineKind.StructuralLinearAutomaton, "a.{0,4}b", "xa😀b tail", false, 1, 1, 6, 1, 4)]
    [InlineData((int)Utf8CompiledEngineKind.StructuralFamily, "\\b(?:record|struct|class)\\s+[A-Z][A-Za-z0-9_]+", "é😀 class Bravo", true, 1, 7, 11, 4, 11)]
    [InlineData((int)Utf8CompiledEngineKind.StructuralLinearAutomaton, "\\b(?:record|struct|class)\\s+[A-Z][A-Za-z0-9_]+", "é😀 class Bravo", true, 1, 7, 11, 4, 11)]
    [InlineData((int)Utf8CompiledEngineKind.StructuralFamily, "\\b(?:record|struct|class)\\s+[A-Z][A-Za-z0-9_]+", "é😀 class Bravo", false, 1, 7, 11, 4, 11)]
    public void InternalCompiledConsumersKeepScalarByteRangesAndDerivedCoordinates(int kind, string pattern,
        string text, bool rtl, int count, int indexBytes, int lengthBytes, int indexUnits, int lengthUnits)
    {
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0);
        var prepared = Prepare(pattern, options);
        var verifier = Utf8VerifierRuntime.Create(prepared);
        var runtime = Utf8CompiledRuntimeFactory.Create(new((Utf8CompiledEngineKind)kind), prepared, verifier, options);
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var validation in new[] { Utf8Validation.Validate(input), default })
        {
            Assert.True(runtime.IsMatch(input, validation, Utf8ExecutionDeadline.Infinite));
            Assert.Equal(count, runtime.Count(input, validation, Utf8ExecutionDeadline.Infinite));
            var match = runtime.Match(input, validation, Utf8ExecutionDeadline.Infinite);
            Assert.True(match.Success); Assert.True(match.IsByteAligned);
            Assert.Equal((indexBytes, lengthBytes, indexUnits, lengthUnits),
                (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16));
        }
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
        if (pattern == @"\p{L}{1,2}")
        {
            Assert.True(Utf8UnicodeLetterBoundedRepeatExecutor.TryCountLetters(input, 1, 2, out var letters));
            Assert.Equal(count, letters);
        }
        if (pattern == @"\p{L}{0,2}")
            Assert.False(Utf8UnicodeLetterBoundedRepeatExecutor.TryCountLetters(input, 0, 2, out _));
        var actualValidation = Utf8Validation.Validate(input);
        if (prepared.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow && !rtl)
        {
            Assert.True(Utf8StructuralLinearCompiledRouter.TryIsMatch(prepared, verifier, false, null, null,
                default, input, actualValidation, Utf8ExecutionDeadline.Infinite, out var found));
            Assert.True(found);
            Assert.True(Utf8StructuralLinearCompiledRouter.TryCount(prepared, verifier, false, null, null,
                default, input, actualValidation, Utf8ExecutionDeadline.Infinite, out var routedCount));
            Assert.Equal(count, routedCount);
            Assert.True(Utf8StructuralLinearCompiledRouter.TryMatch(prepared, verifier, false, null, null,
                default, input, actualValidation, Utf8ExecutionDeadline.Infinite, out var routedMatch));
            Assert.Equal((indexBytes, lengthBytes, indexUnits, lengthUnits),
                (routedMatch.IndexInBytes, routedMatch.LengthInBytes, routedMatch.IndexInUtf16, routedMatch.LengthInUtf16));
        }
        if (kind == (int)Utf8CompiledEngineKind.StructuralFamily && rtl)
        {
            var linear = new Utf8AsciiStructuralFamilyLinearRuntime(prepared);
            Assert.True(linear.IsMatch(input, actualValidation, verifier, Utf8ExecutionDeadline.Infinite));
            Assert.Equal(count, linear.Count(input, actualValidation, verifier, Utf8ExecutionDeadline.Infinite));
            Assert.Equal(lengthBytes, linear.Match(input, actualValidation, verifier, Utf8ExecutionDeadline.Infinite).LengthInBytes);
        }
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
    }

    [Theory]
    [InlineData("import \"abc\"\n", 1, true)]
    [InlineData("import \"😀é\"\n", 1, false)]
    [InlineData("import \"abc\"\nimport shared \"😀é\"\n", 2, false)]
    public void QuotedLineCounterUsesQualifiedGrammarForCompleteScalarSubjects(string text, int expected, bool isAscii)
    {
        const string pattern = "^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$";
        const RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Multiline;
        var prepared = Prepare(pattern, options);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount, prepared.FallbackDirectFamily.Kind);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(isAscii, Utf8Validation.Validate(input).IsAscii);
        Assert.True(Utf8QuotedLineSegmentExecutor.TryCountViaLineWalk(input,
            prepared.FallbackDirectFamily.LiteralUtf8, prepared.FallbackDirectFamily.SecondaryLiteralUtf8, out var nativeCount));
        Assert.Equal(expected, nativeCount);
        var verifier = Utf8VerifierRuntime.Create(prepared);
        var runtime = Utf8CompiledRuntimeFactory.Create(new(Utf8CompiledEngineKind.FallbackRegex), prepared, verifier, options);
        Assert.Equal(expected, runtime.Count(input, Utf8Validation.Validate(input), Utf8ExecutionDeadline.Infinite));
        Assert.Equal(expected, runtime.Count(input, default, Utf8ExecutionDeadline.Infinite));
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralCompiledCursorOwnsReferencesAndEmptyScalarAdvancement(bool rtl)
    {
        var pattern = rtl ? @"\k<x>(?<x>.)|$" : @"(?<x>.)\k<x>|$";
        var prepared = Prepare(pattern, RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0));
        var input = "é😀😀z"u8.ToArray();
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(prepared, input,
            Utf8Validation.Validate(input), Utf8ExecutionDeadline.Infinite);
        Assert.True(cursor.DebugIsOwnedSlice);
        var retained = cursor.DebugInvocationReservedBytes;
        var sibling = cursor;
        var ranges = new List<(int, int)>();
        while (cursor.MoveNext())
        {
            Assert.True(cursor.CurrentValueMatch.IsByteAligned);
            ranges.Add((cursor.CurrentValueMatch.IndexInBytes, cursor.CurrentValueMatch.LengthInBytes));
        }
        Assert.Equal(rtl ? [(11, 0), (2, 8)] : new List<(int, int)> { (2, 8), (11, 0) }, ranges);
        CollectReleasedCursorStorage();
        Assert.Equal(retained, cursor.DebugInvocationReservedBytes);
        Assert.True(sibling.MoveNext());
        Assert.Equal(ranges[0], (sibling.CurrentValueMatch.IndexInBytes, sibling.CurrentValueMatch.LengthInBytes));
        while (sibling.MoveNext()) { }
        // Both holders have dropped their tables. Collection may now return
        // the loans; a reservation surviving EOF is not a lifetime guarantee.
        CollectReleasedCursorStorage();
        Assert.Equal(0UL, sibling.DebugInvocationReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralCursorOffsetRetainsFullAssertionContextAndNfaStart(bool nonBacktracking)
    {
        var pattern = nonBacktracking ? "😀|z" : @"(?<=é).|z";
        var prepared = Prepare(pattern, RegexOptions.CultureInvariant | (nonBacktracking ? RegexOptions.NonBacktracking : 0));
        var input = "é😀z"u8.ToArray();
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(prepared, input,
            Utf8Validation.Validate(input), Utf8ExecutionDeadline.Infinite, 2, 1);
        Assert.True(cursor.DebugIsOwnedSlice);
        var retained = cursor.DebugInvocationReservedBytes;
        var sibling = cursor;
        Assert.True(cursor.MoveNext()); Assert.Equal((2, 4), (cursor.CurrentValueMatch.IndexInBytes, cursor.CurrentValueMatch.LengthInBytes));
        Assert.True(cursor.MoveNext()); Assert.Equal((6, 1), (cursor.CurrentValueMatch.IndexInBytes, cursor.CurrentValueMatch.LengthInBytes));
        Assert.False(cursor.MoveNext());
        CollectReleasedCursorStorage();
        Assert.Equal(retained, cursor.DebugInvocationReservedBytes);
        Assert.True(sibling.MoveNext());
        Assert.Equal((2, 4), (sibling.CurrentValueMatch.IndexInBytes, sibling.CurrentValueMatch.LengthInBytes));
        while (sibling.MoveNext()) { }
        CollectReleasedCursorStorage();
        Assert.Equal(0UL, sibling.DebugInvocationReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralSplitFactoryUsesScalarDelimitersWithoutDecodedSubject(bool rtl)
    {
        var prepared = Prepare("(?<d>😀)(?=.)", RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : 0));
        var input = "é😀x😀z"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var cursor = Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(prepared, input,
            validation, validation.Utf16Length, int.MaxValue, Utf8ExecutionDeadline.Infinite);
        var pieces = new List<string>();
        while (cursor.MoveNext()) { Assert.True(cursor.Current.IsByteAligned); pieces.Add(cursor.Current.GetValueString()); }
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
    public void CompiledOperationsUseThePassedEntryDeadline(int operation)
    {
        const string pattern = @"(?<=é).";
        var prepared = Prepare(pattern);
        var verifier = Utf8VerifierRuntime.Create(prepared);
        var runtime = Utf8CompiledRuntimeFactory.Create(new(Utf8CompiledEngineKind.FallbackRegex), prepared, verifier, RegexOptions.CultureInvariant);
        var input = "é😀"u8.ToArray();
        var validation = Utf8Validation.Validate(input);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1));
        Thread.Sleep(20);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
        {
            if (operation == 0) _ = runtime.IsMatch(input, validation, deadline);
            else if (operation == 1) _ = runtime.Count(input, validation, deadline);
            else _ = runtime.Match(input, validation, deadline);
        });
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(verifier);
    }

    private static Utf8ExecutionLimitKind Run(int operation, Utf8PreparedRegex prepared, byte[] input,
        Utf8ValidationResult validation, ref Utf8SliceInvocation invocation)
        => operation == 0 ? Utf8PreparedScalarExecutor.TryIsMatch(prepared, input, validation, ref invocation, out _)
            : operation == 1 ? Utf8PreparedScalarExecutor.TryCount(prepared, input, validation, ref invocation, out _)
            : Utf8PreparedScalarExecutor.TryMatch(prepared, input, validation, ref invocation, out _);

    [Theory]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
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
