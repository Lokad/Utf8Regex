using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2NativeReplacementParserTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void NativeWorkAndInterruptionCutoffsKeepTheParentAndOriginalAuthority(bool extended, bool timeout)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var source = Encoding.UTF8.GetBytes(extended ? @"é${1:+\U😀:\x{10428}}\é" : "é😀$$$0");
        var options = extended ? Pcre2SubstitutionOptions.Extended : Pcre2SubstitutionOptions.None;
        ulong steps; var checkpoints = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = () => checkpoints++;
            _ = Pcre2ReplacementPreparationControl.Prepare(source, options, false);
            steps = measured.Budget.DiagnosticCheckpoints;
        }
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
            using var token = new CancellationTokenSource(); var calls = 0;
            long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            owner.Budget.CheckpointHook = () => { if (++calls == target) { if (timeout) ticks = 10; else token.Cancel(); } };
            var invocation = new Utf8SliceInvocation(new(1024, 1 << 20),
                timeout ? TimeSpan.FromMilliseconds(10) : Timeout.InfiniteTimeSpan, token.Token);
            using (var control = Pcre2ReplacementPreparationControl.Borrow(invocation, token.Token))
            {
                if (timeout) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() =>
                    Pcre2ReplacementPreparationControl.Prepare(source, options, false)).ErrorKind);
                else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                    Pcre2ReplacementPreparationControl.Prepare(source, options, false)).CancellationToken);
            }
            Assert.Equal(7UL, owner.Budget.ReservedBytes);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void NativeMajorBuffersInstructionsAndDepthAreAdmittedBeforeAcquisition()
    {
        var source = "é${1:+\\U😀:\\x{10428}}\\é"u8.ToArray();
        ulong bytes, slots, depth;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            _ = Pcre2ReplacementPreparationControl.Prepare(source, Pcre2SubstitutionOptions.Extended, false);
            bytes = measured.Budget.PeakBytes; slots = measured.Budget.Peak; depth = measured.Budget.PeakDepth;
        }
        foreach (var kind in new[] { Utf8ResourceLimitKind.ConstructionBytes,
            Utf8ResourceLimitKind.ConstructionInstructionCapacity, Utf8ResourceLimitKind.ConstructionDepth })
        {
            var maximum = kind == Utf8ResourceLimitKind.ConstructionBytes ? bytes :
                kind == Utf8ResourceLimitKind.ConstructionDepth ? depth : slots;
            foreach (var limit in new[] { 0UL, maximum / 2, maximum - 1, maximum }.Distinct())
            {
                using var owner = new Utf8ConstructionScope(
                    kind == Utf8ResourceLimitKind.ConstructionInstructionCapacity ? limit : ulong.MaxValue,
                    kind == Utf8ResourceLimitKind.ConstructionBytes ? 7 + limit : ulong.MaxValue,
                    kind == Utf8ResourceLimitKind.ConstructionDepth ? limit : ulong.MaxValue);
                owner.Budget.ReserveBytes(7);
                using (var child = Utf8ConstructionScope.Enter())
                {
                    var failure = Record.Exception(() => Pcre2ReplacementPreparationControl.Prepare(
                        source, Pcre2SubstitutionOptions.Extended, false));
                    if (limit == 0) Assert.Equal(kind, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    else if (limit == maximum) Assert.Null(failure);
                    else if (failure is not null) Assert.Equal(kind, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                }
                Assert.Equal(7UL, owner.Budget.ReservedBytes); Assert.Equal(0UL, owner.Budget.Reserved);
                Assert.Equal(0UL, owner.Budget.Depth);
            }
        }
    }
}
