using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PreparedReplacementAdmissionTests
{
    public static IEnumerable<object[]> Shapes()
    {
        yield return ["$1"];
        yield return [@"\x{1f600}"];
        yield return [@"${1:+a:b}"];
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void PreparedGraphsKeepMajorPayloadAndInstructionBounds(string template)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var plan = Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended);
        var arrays = new HashSet<byte[]>(ReferenceEqualityComparer.Instance);
        foreach (var instruction in plan.Instructions) arrays.Add(instruction.Payload);
        var bytes = Utf8ConstructionBudget.ArrayBytes<Pcre2ReplacementInstruction>(plan.Instructions.Length)
            + Utf8ConstructionBudget.ArrayBytes<Pcre2ReplacementFragment>(plan.Fragments.Length)
            + arrays.Aggregate(0ul, (total, array) => total + Utf8ConstructionBudget.ArrayBytes<byte>(array.Length));
        Assert.True(owner.Budget.ReservedBytes >= bytes + 128ul);
        Assert.Equal((ulong)plan.Instructions.Length, owner.Budget.Reserved);
        Assert.True(owner.Budget.PeakBytes > owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        foreach (var array in arrays) Assert.Equal(array, new UTF8Encoding(false, true).GetBytes(Encoding.UTF8.GetString(array)));
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryConstructionWorkCutoffPreservesTheParentAndSpentWork(string template)
    {
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        { _ = Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended); work = measured.Budget.WorkSteps; }
        for (var cap = 0ul; cap <= work; cap++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cap + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Pcre2ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended));
                if (cap < work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.True(owner.Budget.WorkSteps <= cap + 2);
            var spent = owner.Budget.WorkSteps;
            using (var child = Utf8ConstructionScope.Enter()) { }
            Assert.Equal(spent, owner.Budget.WorkSteps);
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ByteInstructionAndDepthDenialsRollbackWithoutRefundingWork(string template)
    {
        ulong bytes, slots, depth, minimumBytes, instructionCount;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            var plan = Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended);
            bytes = measured.Budget.PeakBytes; slots = measured.Budget.Peak; depth = measured.Budget.PeakDepth;
            minimumBytes = measured.Budget.ReservedBytes; instructionCount = (ulong)plan.Instructions.Length;
        }
        foreach (var cap in new[] { 0ul, 1ul, bytes / 2, bytes - 1, bytes })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 128 + cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Pcre2ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended));
                if (failure is not null) Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                if (cap < minimumBytes) Assert.NotNull(failure);
                if (cap == bytes) Assert.Null(failure);
                Assert.True(owner.Budget.PeakBytes <= 128 + cap);
            }
            AssertParent(owner.Budget, checkpoint, parent);
        }
        for (var cap = 0ul; cap <= slots; cap++)
        {
            using var owner = new Utf8ConstructionScope(cap, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended));
                if (failure is not null) Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                if (cap < 2 * instructionCount) Assert.NotNull(failure);
                if (cap == slots) Assert.Null(failure);
                Assert.True(owner.Budget.Peak <= cap);
            }
            Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.ReservedBytes);
        }
        for (var cap = 0ul; cap <= depth; cap++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, cap);
            using (var child = Utf8ConstructionScope.Enter())
                if (cap < depth) Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.Throws<Utf8ResourceLimitExceededException>(() => Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended)).ResourceKind);
                else _ = Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
        }
    }

    [Fact]
    public void ContinuationsAndOutputShareWorkingCapacityAndAlwaysRelease()
    {
        var preparation = new Pcre2ReplacementPreparation(null, Pcre2PreparedReplacementParser.Parse("${1:+A:B}", Pcre2SubstitutionOptions.Extended));
        var groups = new[] { Pcre2GroupData.FromByteOffsets("x"u8, 0, 0, 1), Pcre2GroupData.FromByteOffsets("x"u8, 1, 0, 1) };
        var writer = new Pcre2ReplacementTemplateWriter("x"u8, [],
            new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 144),
                Timeout.InfiniteTimeSpan, default));
        try
        {
            Assert.Equal(48ul, writer.ReservedWorkingBytes);
            writer.AppendTemplate(preparation, Pcre2SubstitutionOptions.Extended, groups, 0, 1, null);
            Assert.Equal(144ul, writer.ReservedWorkingBytes);
            Assert.Equal("A"u8.ToArray(), writer.Complete());
            Assert.Equal(0ul, writer.ReservedWorkingBytes);
        }
        finally { writer.Release(); }
        var denied = new Pcre2ReplacementTemplateWriter("x"u8, [],
            new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 143),
                Timeout.InfiniteTimeSpan, default));
        try
        {
            Pcre2MatchException? failure = null;
            try { denied.AppendTemplate(preparation, Pcre2SubstitutionOptions.Extended, groups, 0, 1, null); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.NotNull(failure); Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
            Assert.Equal(48ul, denied.ReservedWorkingBytes);
        }
        finally { denied.Release(); }
        Assert.Equal(0ul, denied.ReservedWorkingBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryPreparationCheckpointObservesBorrowedOperationControlAndRollsBack(bool timeout)
    {
        foreach (var shape in Shapes())
        {
            var template = (string)shape[0];
            var checkpoints = 0;
            using (var measured = Utf8ConstructionScope.Enter())
            {
                measured.Budget.CheckpointHook = _ => checkpoints++;
                _ = Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended);
            }
            for (var target = 1; target <= checkpoints; target++)
            {
                using var owner = Utf8ConstructionScope.Enter();
                var parent = owner.Budget.AllocateObject(17, static value => new Pcre2ParentValue(value));
                var checkpoint = owner.Budget.ReservedBytes;
                using var token = new CancellationTokenSource();
                long clock = 30000;
                using var timer = Utf8ExecutionDeadline.UseClockForDiagnostics(() => clock);
                var calls = 0;
                owner.Budget.CheckpointHook = _ => { if (++calls == target) { if (timeout) clock++; else token.Cancel(); } };
                var failure = Record.Exception(() =>
                {
                    using var control = Pcre2ReplacementPreparationControl.EnterOperation(timeout ? TimeSpan.FromMilliseconds(1) : Timeout.InfiniteTimeSpan, token.Token);
                    _ = Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended);
                });
                if (timeout) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
                else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
                AssertParent(owner.Budget, checkpoint, parent);
                Assert.False(Pcre2ReplacementPreparationControl.IsActive);
                owner.Budget.CheckpointHook = null;
                using (var recovery = Utf8ConstructionScope.Enter())
                    _ = Pcre2PreparedReplacementParser.Parse("", Pcre2SubstitutionOptions.Extended);
                AssertParent(owner.Budget, checkpoint, parent);
            }
        }
    }

    [Fact]
    public void CanceledPreparationDoesNotPublishOrRetainItsTokenInTheCache()
    {
        var regex = new Utf8Pcre2Regex("(x)");
        using var token = new CancellationTokenSource();
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.CheckpointHook = _ => token.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.Replace("x"u8, @"\x{1f600}", 0,
            Pcre2SubstitutionOptions.Extended, Pcre2MatchOptions.None, token.Token));
        Assert.Equal(token.Token, error.CancellationToken);
        Assert.Equal(0, regex.DebugReplacementCacheEntryCount);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.False(Pcre2ReplacementPreparationControl.IsActive);
        owner.Budget.CheckpointHook = null;
        Assert.Equal("😀"u8.ToArray(), regex.Replace("x"u8, @"\x{1f600}", 0, Pcre2SubstitutionOptions.Extended));
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
    }

    [Fact]
    public void AnErrorInUnselectedNestedValidationKeepsAndReleasesTheStackLoan()
    {
        var preparation = new Pcre2ReplacementPreparation(null, Pcre2PreparedReplacementParser.Parse("${1:+A:${missing:+B:C}}", Pcre2SubstitutionOptions.Extended));
        var groups = new[] { Pcre2GroupData.FromByteOffsets("x"u8, 0, 0, 1), Pcre2GroupData.FromByteOffsets("x"u8, 1, 0, 1) };
        var writer = new Pcre2ReplacementTemplateWriter("x"u8, [],
            new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 144),
                Timeout.InfiniteTimeSpan, default));
        try
        {
            Pcre2SubstitutionException? failure = null;
            try { writer.AppendTemplate(preparation, Pcre2SubstitutionOptions.Extended, groups, 0, 1, null); }
            catch (Pcre2SubstitutionException error) { failure = error; }
            Assert.NotNull(failure); Assert.Equal(Pcre2ErrorKind.UnknownReplacementGroup, failure.ErrorKind);
            Assert.Equal(0, writer.Length); Assert.Equal(144ul, writer.ReservedWorkingBytes);
        }
        finally { writer.Release(); }
        Assert.Equal(0ul, writer.ReservedWorkingBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationAndDeadlineInsideExpansionReleaseLiveContinuationCapacity(bool timeout)
    {
        var template = "z";
        for (var index = 0; index < 128; index++) template = "${1:-" + template + "}";
        var preparation = new Pcre2ReplacementPreparation(null, Pcre2PreparedReplacementParser.Parse(template, Pcre2SubstitutionOptions.Extended));
        var groups = new Pcre2GroupData[2]; groups[0] = Pcre2GroupData.FromByteOffsets("x"u8, 0, 0, 1);
        using var token = new CancellationTokenSource();
        var reads = 0;
        using var timer = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads >= 50) { if (timeout) return 30001; token.Cancel(); }
            return 30000;
        });
        var writer = new Pcre2ReplacementTemplateWriter("x"u8, [],
            new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 0),
                timeout ? TimeSpan.FromMilliseconds(1) : TimeSpan.FromHours(1), token.Token));
        try
        {
            Exception? failure = null;
            try { writer.AppendTemplate(preparation, Pcre2SubstitutionOptions.Extended, groups, 0, 1, null); }
            catch (Exception error) { failure = error; }
            if (timeout) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.True(writer.ReservedWorkingBytes > 1);
            Assert.Equal(0, writer.Length);
        }
        finally { writer.Release(); }
        Assert.Equal(0ul, writer.ReservedWorkingBytes);
    }

    private sealed class Pcre2ParentValue(int value) { internal int Value => value; }
    private static void AssertParent(Utf8ConstructionBudget budget, ulong checkpoint, Pcre2ParentValue parent)
    {
        Assert.Equal(128ul, budget.ReservedBytes); Assert.Equal(0ul, budget.Reserved); Assert.Equal(0ul, budget.Depth);
        Assert.Equal(checkpoint, budget.ReservedBytes); Assert.Equal(17, parent.Value);
    }
}
