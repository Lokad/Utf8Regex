using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CursorCopyWorkTests
{
    private static Utf8PreparedRegex Prepared(Utf8Regex regex) =>
        (Utf8PreparedRegex)typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    private static Utf8SliceProgram Program(Utf8Regex regex) =>
        (Utf8SliceProgram)typeof(Utf8Regex).GetField("_unitSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    public static IEnumerable<object[]> Routes()
    {
        (string Pattern, string Text)[] rows = [
            ("target", "é😀 target target"), ("(?i:target)", "é😀 TARGET target"),
            ("é", "é😀 é"), ("(?:target|needle)", "é😀 target needle target"),
            ("(?i:target|needle)", "é😀 TARGET needle target"),
            ("ab[0-9]{1,2}d", "é😀 ab1d ab12d"),
            ("(?:get|set)[A-Z][A-Za-z0-9_]*", "é😀 getAlpha setBeta"),
            ("a.{0,4}b", "é😀 a😀b a中b"), ("^target", "target é😀"),
            (@"target\z", "é😀 target"), (@"(ab)\1", "é😀 abab abab"),
            (@"(?=(.))", "a😀b"), ("", "a😀b")];
        foreach (var row in rows)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [rtl && row.Pattern == @"(ab)\1" ? @"\1(ab)" : row.Pattern,
                row.Text, compiled, rtl];
    }

    private static RegexOptions Options(bool compiled, bool rtl) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);

    [Theory]
    [InlineData(64UL, 0U, 0UL, false)]
    [InlineData(0UL, 7U, 0UL, false)]
    [InlineData(0UL, 0U, 64UL, false)]
    [InlineData(0UL, 0U, 0UL, true)]
    public void ControlledScopesShareCounterUpdatesAndSeparateInvocationsRemainIndependent(ulong work, uint depth, ulong bytes, bool deadline)
    {
        var timeout = deadline ? TimeSpan.FromHours(1) : Regex.InfiniteMatchTimeout;
        var original = new Utf8SliceInvocation(new(work, depth, bytes), timeout, default);
        var copy = original;
        Assert.Equal(Utf8ExecutionLimitKind.None, original.TryChargeInputStep());
        Assert.Equal(1UL, copy.Budget.Steps);
        Assert.Equal(Utf8ExecutionLimitKind.None, copy.Budget.TryNoteDepth(7));
        Assert.Equal(7U, original.Budget.PeakDepth);
        Assert.Equal(Utf8ExecutionLimitKind.None, original.Budget.TryNoteWorkingBytes(64));
        Assert.Equal(64UL, copy.Budget.PeakWorkingBytes);
        var independent = new Utf8SliceInvocation(new(work, depth, bytes), timeout, default);
        Assert.Equal(0UL, independent.Budget.Steps);
        Assert.Equal(0U, independent.Budget.PeakDepth);
        Assert.Equal(0UL, independent.Budget.PeakWorkingBytes);
    }

    [Fact]
    public void UnlimitedNoneTokenInvocationMetadataDoesNotAllocate()
    {
        for (var i = 0; i < 32; i++)
        {
            var warm = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
            Assert.Equal(Utf8ExecutionLimitKind.None, warm.TryChargeInputStep());
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        ulong steps = 0;
        for (var i = 0; i < 512; i++)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
            _ = invocation.TryChargeInputStep();
            steps += invocation.Budget.Steps;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(512UL, steps);
        Assert.Equal(0L, allocated);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void PublicCopiesKeepIndependentPositionsAndOneCumulativeWorkCounter(string pattern, string text, bool compiled, bool rtl)
    {
        var regex = new Utf8Regex(pattern, Options(compiled, rtl));
        using var source = new CancellationTokenSource();
        var original = regex.EnumerateMatches(Encoding.UTF8.GetBytes(text), source.Token);
        var copy = original.GetEnumerator();
        var entrySteps = copy.DebugInvocationSteps;
        var expected = new Regex(pattern, Options(compiled, rtl)).Matches(text);
        Assert.NotEmpty(expected.Cast<Match>());
        Assert.True(original.MoveNext());
        Assert.True(original.DebugInvocationSteps > entrySteps);
        Assert.Equal(original.DebugInvocationSteps, copy.DebugInvocationSteps);
        Assert.True(copy.MoveNext());
        Assert.Equal(original.Current, copy.Current);
        Assert.Equal(original.DebugInvocationSteps, copy.DebugInvocationSteps);
        var retained = copy.Current;
        var index = 1;
        while (original.MoveNext())
        {
            Assert.Equal((expected[index].Index, expected[index].Length),
                (original.Current.IndexInUtf16, original.Current.LengthInUtf16));
            Assert.Equal(retained, copy.Current);
            Assert.Equal(original.DebugInvocationSteps, copy.DebugInvocationSteps);
            index++;
        }
        Assert.Equal(expected.Count, index);
        index = 1;
        // The first copy's EOF must not change the other copy's position.
        while (copy.MoveNext())
        {
            Assert.Equal((expected[index].Index, expected[index].Length),
                (copy.Current.IndexInUtf16, copy.Current.LengthInUtf16));
            Assert.Equal(original.DebugInvocationSteps, copy.DebugInvocationSteps);
            index++;
        }
        Assert.Equal(expected.Count, index);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void RepeatedFreshCopiesCannotRestartTheCancellationCheckpointCadence(string pattern, string text, bool compiled, bool rtl)
    {
        var regex = new Utf8Regex(pattern, Options(compiled, rtl));
        using var source = new CancellationTokenSource();
        var active = false;
        ulong checkpoint = 0;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (!active) return;
            checkpoint = steps;
            source.Cancel();
        });
        try
        {
            var original = regex.EnumerateMatches(Encoding.UTF8.GetBytes(text), source.Token);
            var entrySteps = original.DebugInvocationSteps;
            active = true;
            var canceled = false;
            for (var attempt = 0; attempt < 128 && !canceled; attempt++)
            {
                var copy = original.GetEnumerator();
                try { Assert.True(copy.MoveNext()); }
                catch (OperationCanceledException exception)
                {
                    Assert.Equal(source.Token, exception.CancellationToken);
                    canceled = true;
                }
            }
            Assert.True(canceled, "Fresh value copies restarted the checkpoint cadence.");
            Assert.True(checkpoint > entrySteps);
            Assert.Equal(0UL, checkpoint & 63);
            Assert.True(original.DebugInvocationSteps >= checkpoint);
            Assert.False(original.Current.Success);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ACopyCannotSpendTheSameFiniteWorkAllowanceTwice(bool owned, bool compiled)
    {
        var regex = new Utf8Regex(owned ? @"(ab)\1" : "target", Options(compiled, false));
        var input = owned ? "abab abab"u8.ToArray() : "target target"u8.ToArray();
        var baseline = Create(regex, input, ulong.MaxValue, owned);
        Assert.True(baseline.MoveNext());
        var cap = baseline.DebugInvocationSteps;
        var original = Create(regex, input, cap, owned);
        var copy = original;
        Assert.True(original.MoveNext());
        var retained = original.CurrentValueMatch;
        var denied = false;
        try { _ = copy.MoveNext(); }
        catch (InvalidOperationException) { denied = true; }
        Assert.True(denied, "The copied cursor reused the original work allowance.");
        Assert.Equal(cap + 1, original.DebugInvocationSteps);
        Assert.Equal(original.DebugInvocationSteps, copy.DebugInvocationSteps);
        Assert.Equal(retained, original.CurrentValueMatch);
        Assert.False(copy.CurrentValueMatch.Success);
    }

    private static Utf8OperationMatchCursor Create(Utf8Regex regex, byte[] input, ulong cap, bool owned)
    {
        var invocation = new Utf8SliceInvocation(new(cap, 0, 0), Regex.InfiniteMatchTimeout, default);
        var validation = Utf8Validation.Validate(input, ref invocation);
        return owned ? Utf8OperationMatchCursor.CreateMapped(Program(regex), input, validation,
                ref invocation, false, out _)
            : new Utf8OperationMatchCursor(input, Prepared(regex), true, false, invocation);
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(0, false, true)]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(0, true, true)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    public void SplitCopiesAccumulateWorkThroughTailAndEofWhileRetainingExistingViews(int count, bool compiled, bool rtl)
    {
        const string text = "é😀 abab abab tail";
        var regex = new Utf8Regex(@"(ab)\1", Options(compiled, rtl));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, source.Token);
        var original = Utf8OwnedSplitPreparation.Create(Program(regex), Encoding.UTF8.GetBytes(text), count, ref invocation);
        var copy = original.GetEnumerator();
        Assert.True(original.MoveNext());
        Assert.Equal(original.DebugInvocationSteps, copy.DebugInvocationSteps);
        Assert.True(copy.MoveNext());
        var retained = copy.Current;
        var retainedValue = retained.GetValueString();
        // EnumerateSplits excludes captured delimiters; compare copies directly.
        var originalValues = new List<string> { original.Current.GetValueString() };
        while (original.MoveNext()) originalValues.Add(original.Current.GetValueString());
        Assert.Equal(original.DebugInvocationSteps, copy.DebugInvocationSteps);
        var copyValues = new List<string> { copy.Current.GetValueString() };
        while (copy.MoveNext()) copyValues.Add(copy.Current.GetValueString());
        Assert.Equal(originalValues, copyValues);
        Assert.Equal(original.DebugInvocationSteps, copy.DebugInvocationSteps);
        source.Cancel();
        Assert.Equal(retainedValue, retained.GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
