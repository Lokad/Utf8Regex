using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeReplacementParserTests
{
    public static IEnumerable<object[]> Templates()
    {
        foreach (var row in Utf8ReplacementPreparationAdmissionTests.Shapes())
        {
            var text = (string)row[0];
            if (text == Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text))) yield return row;
        }
        foreach (var text in new[] { "é😀\0${one}$0000000000000000012$+", "${é}${𝒜}${😀}${1é}",
            "${0000000000000000000000000000000000000000000012}", "${bad name}${01}$0999$$",
            "${21474836489999999999999999999}$21474836489999999999999999999", "é${unclosed😀" })
        foreach (var ecma in new[] { false, true }) yield return [text, ecma];
    }

    private static Utf8AnalyzedReplacement Prepare(string text, bool native, bool ecma)
        => Utf8FrontEndReplacementAnalyzer.Analyze(native ? new Utf8ConstructionPattern(Encoding.UTF8.GetBytes(text))
            : new Utf8ConstructionPattern(text), [0, 1, 7, 12], ["zero", "one", "seven", "twelve"], ecma);

    [Theory]
    [MemberData(nameof(Templates))]
    public void NativeAndTextSourcesKeepInstructionsFlagsAndUndefinedSpellings(string template, bool ecma)
    {
        var expected = Prepare(template, false, ecma); var actual = Prepare(template, true, ecma);
        Assert.Equal(expected.IsLiteral, actual.IsLiteral);
        Assert.Equal(expected.ContainsSubstitutions, actual.ContainsSubstitutions);
        Assert.Equal(expected.ContainsNamedGroups, actual.ContainsNamedGroups);
        Assert.Equal(expected.ContainsGroupReferences, actual.ContainsGroupReferences);
        Assert.Equal(expected.ContainsSpecialSubstitutions, actual.ContainsSpecialSubstitutions);
        Assert.Equal(expected.LiteralUtf8, actual.LiteralUtf8);
        Assert.Equal(expected.Plan.Instructions.Count, actual.Plan.Instructions.Count);
        for (var index = 0; index < expected.Plan.Instructions.Count; index++)
        {
            var left = expected.Plan.Instructions[index]; var right = actual.Plan.Instructions[index];
            Assert.Equal(left.Kind, right.Kind); Assert.Equal(left.GroupNumber, right.GroupNumber);
            Assert.Equal(left.LiteralUtf8, right.LiteralUtf8);
        }
    }

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void LiteralBlockEdgesKeepWholeScalarsAndAdjacentUnknownReferences(int prefixLength)
    {
        var prefix = new string('x', prefixLength);
        var template = prefix + "😀é${unknown}$00123" + prefix + "😀é";
        var value = Prepare(template, true, false);
        Assert.True(value.IsLiteral);
        Assert.Equal(Encoding.UTF8.GetBytes(template), value.LiteralUtf8);
        Assert.Same(value.Plan.Instructions[0].LiteralUtf8, value.LiteralUtf8);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativePreparationInterruptionsAndWorkDenialsPreserveTheParent(bool timeout)
    {
        const string text = "é${one}😀$00123$+$0";
        ulong steps; var checkpoints = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = _ => checkpoints++;
            _ = Prepare(text, true, false); steps = measured.Budget.WorkSteps;
        }
        for (ulong maximum = 0; maximum <= steps; maximum++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, maximum, ulong.MaxValue);
            owner.Budget.ReserveBytes(7);
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Prepare(text, true, false));
                if (maximum < steps) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.Depth);
            Assert.InRange(owner.Budget.WorkSteps, 0UL, maximum);
        }
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
            using var token = new CancellationTokenSource(); long ticks = 0; var calls = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            owner.Budget.CancellationToken = token.Token;
            owner.Budget.Deadline = timeout ? Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10)) : Utf8ExecutionDeadline.Infinite;
            owner.Budget.CheckpointHook = _ => { if (++calls == target) { if (timeout) ticks = 10; else token.Cancel(); } };
            var failure = Record.Exception(() => Prepare(text, true, false));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.Depth);
        }
    }
}
