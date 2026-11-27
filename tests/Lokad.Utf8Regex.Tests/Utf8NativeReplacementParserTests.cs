using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeReplacementParserTests
{
    private static Utf8AnalyzedReplacement Prepare(string text) =>
        Utf8FrontEndReplacementAnalyzer.Analyze(Encoding.UTF8.GetBytes(text),
            [0, 1, 7, 12], ["zero", "one", "seven", "twelve"]);

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void LiteralBlockEdgesKeepWholeScalarsAndAdjacentUnknownReferences(int prefixLength)
    {
        var prefix = new string('x', prefixLength);
        var template = prefix + "😀é${unknown}$00123" + prefix + "😀é";
        var value = Prepare(template);
        Assert.True(value.IsLiteral);
        Assert.Equal(Encoding.UTF8.GetBytes(template), value.LiteralUtf8);
        Assert.Same(value.Plan.Instructions[0].LiteralUtf8, value.LiteralUtf8);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativePreparationInterruptionsAndWorkDenialsPreserveTheParent(bool timeout)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        const string text = "é${one}😀$00123$+$0";
        ulong steps; var checkpoints = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = () => checkpoints++;
            _ = Prepare(text); steps = measured.Budget.DiagnosticCheckpoints;
        }
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
            using var token = new CancellationTokenSource(); long ticks = 0; var calls = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            owner.Budget.CancellationToken = token.Token;
            owner.Budget.Deadline = timeout ? Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(10)) : Utf8ExecutionDeadline.Infinite;
            owner.Budget.CheckpointHook = () => { if (++calls == target) { if (timeout) ticks = 10; else token.Cancel(); } };
            var failure = Record.Exception(() => Prepare(text));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.Depth);
        }
    }
}
