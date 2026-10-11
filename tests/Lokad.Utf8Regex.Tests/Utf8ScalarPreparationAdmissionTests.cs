using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarPreparationAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void PublicAndInternalPreparationRetainOneScalarProgram(bool compiled, bool nonBacktracking)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) |
            (nonBacktracking ? RegexOptions.NonBacktracking : 0);
        var regex = new Utf8Regex("(?<x>.){2}", options);
        var publicProgram = (Utf8RegexProgram)typeof(Utf8Regex)
            .GetField("_program", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
        var scalar = publicProgram.PreparedRegex.ScalarProgram;
        Assert.NotNull(scalar);
        Assert.Same(scalar, typeof(Utf8Regex).GetField("_ownedSliceProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex));
        var detailed = regex.MatchDetailed("\U0001f600\U00010400"u8);
        Assert.True(detailed.Success);
        Assert.Equal((0, 8), (detailed.IndexInBytes, detailed.LengthInBytes));
        Assert.Equal(nonBacktracking ? 1 : 2, detailed.GetGroup("x").CaptureCount);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);

        var prepared = Utf8FrontEnd.Compile("(?<x>.){2}", options);
        Assert.NotNull(prepared.ScalarProgram);
        var starts = new int[prepared.ScalarProgram.GroupCount];
        var ends = new int[starts.Length];
        using var owner = Utf8ConstructionScope.Enter();
        var before = owner.Budget.ReservedBytes;
        var checkpoint = owner.Budget.ReservedBytes;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
            var match = Utf8SliceRunner.TryMatchAt(prepared.ScalarProgram, "\U0001f600\U00010400"u8, 0, starts, ends, true, ref invocation);
            Assert.True(match.IsMatch); Assert.Equal(8, match.EndIndex);
        }
        Assert.Equal(before, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.WorkSteps);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompiledHintKeepsItsCaptureWritingLazyLoopRouting(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var program = Utf8RegexProgram.Compile("(?<x>a){0,2}?b", options, Regex.InfiniteMatchTimeout);
        Assert.NotNull(program.PreparedRegex.ScalarProgram);
        Assert.Equal(compiled, program.PreparedRegex.ScalarProgram.UsesInterpretedLazyLoopSemantics);
        var regex = new Utf8Regex("(?<x>a){0,2}?b", options);
        var match = regex.MatchDetailed("aab"u8);
        Assert.True(match.Success);
        Assert.Equal(2, match.GetGroup("x").CaptureCount);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedPreparationRestoresParentLoansAndRetainsSpentWork(bool work)
    {
        var analysis = Utf8FrontEnd.Analyze(new string('a', 4096) + "(?<x>[A-Z]{2})+", RegexOptions.CultureInvariant);
        var recovery = Utf8FrontEnd.Analyze("", RegexOptions.CultureInvariant);
        using var owner = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity,
            work ? Utf8ConstructionScope.DefaultBufferBytes : 32768,
            work ? 32ul : Utf8ConstructionScope.DefaultWorkSteps, Utf8ConstructionScope.DefaultDepth);
        var held = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var before = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8RegexPreparer.Prepare(analysis));
        Assert.Equal(work ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(before, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, held.Value); Assert.True(owner.Budget.WorkSteps > 2);
        Assert.True(owner.Budget.PeakBytes <= owner.Budget.MaximumBytes);
        if (work)
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8RegexPreparer.Prepare(recovery)).ResourceKind);
        else
        {
            var prepared = Utf8RegexPreparer.Prepare(recovery);
            Assert.NotNull(prepared.ScalarProgram);
            Assert.Equal(17, held.Value);
        }
    }

    [Fact]
    public void CompletePreparedAndCursorDescriptorsBorrowTheirScalarAndNativePayloads()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var prepared = Utf8FrontEnd.Compile("[A-Z]+", RegexOptions.CultureInvariant);
        Assert.NotNull(prepared);
        Assert.NotNull(prepared.OperationMatchCursorPlans);
        foreach (var type in new[] { typeof(Utf8PreparedRegex), typeof(Utf8OperationMatchCursorPlans) })
        {
            var ctor = Assert.Single(type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.True(ctor.IsPrivate);
            Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
            var state = Assert.Single(ctor.GetParameters()).ParameterType;
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var stateFields = state.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.Equal(fields.Select(field => field.FieldType), stateFields.Select(field => field.FieldType));
            var costMethod = typeof(Utf8ConstructionBudget).GetMethod("ObjectBytes", BindingFlags.Static | BindingFlags.NonPublic)!;
            var cost = Math.Max(128ul, (ulong)costMethod.MakeGenericMethod(state).Invoke(null, null)!);
            Assert.True(owner.Budget.ReservedBytes >= cost);

        }
        Assert.NotNull(prepared.ScalarProgram);
        Assert.Same(prepared.SemanticRegex.CaptureCatalog, prepared.ScalarProgram.CaptureCatalog);
        Assert.Same(prepared.ExecutionProgram, prepared.OperationMatchCursorPlans.ExecutionProgram);
    }
}
