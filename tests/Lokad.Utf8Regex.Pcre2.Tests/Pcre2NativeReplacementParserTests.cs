using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2NativeReplacementParserTests
{
    public static IEnumerable<object[]> Templates()
    {
        foreach (var value in new[] { "", "é😀\0e\u0301", "$$$0$&${0}", "$1${name}$<name>$*MARK",
            @"\x{1f600}\N{U+10428}\o{377}\cA", @"\U\Qé😀$1\E𐐨", @"\é\😀",
            @"${1:+é\:😀:${name:-fallback}}", @"${1:-\x{z}}", @"${1:+ok:\x{z}}",
            @"\1234\0113\8\99", "$2147483648999999999999999999999", "$<é😀>",
            "${*MARK}", "${}", "${unterminated", "$<unclosed", @"\g<é😀>",
            @"\g<>", @"\x{d800}", @"\o{}", @"\c😀", "\\", "$", "tail$" })
        foreach (var options in new[] { Pcre2SubstitutionOptions.None, Pcre2SubstitutionOptions.Extended })
            yield return [value, options];
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void NativeAndTextSourcesPublishTheSameByteInstructions(string template, Pcre2SubstitutionOptions options)
    {
        var utf8 = Encoding.UTF8.GetBytes(template);
        var expected = Pcre2ReplacementPreparationControl.Prepare(template, options, false);
        var actual = Pcre2ReplacementPreparationControl.Prepare(new Utf8ConstructionPattern(utf8), options, false);
        Assert.Equal(expected.Simple.HasValue, actual.Simple.HasValue);
        if (expected.Simple is { } simple)
        {
            var other = actual.Simple!.Value;
            Assert.Equal(simple.Segments.Length, other.Segments.Length);
            for (var index = 0; index < simple.Segments.Length; index++)
            {
                Assert.Equal(simple.Segments[index].IsWholeMatch, other.Segments[index].IsWholeMatch);
                Assert.Equal(simple.Segments[index].LiteralUtf8, other.Segments[index].LiteralUtf8);
            }
        }
        if (expected.Prepared is { } prepared)
        {
            var other = actual.Prepared!.Value;
            Assert.Equal(prepared.Fragments, other.Fragments);
            Assert.Equal(prepared.Instructions.Length, other.Instructions.Length);
            for (var index = 0; index < prepared.Instructions.Length; index++)
            {
                var instruction = prepared.Instructions[index]; var native = other.Instructions[index];
                Assert.Equal(instruction with { Payload = [] }, native with { Payload = [] });
                Assert.Equal(instruction.Payload, native.Payload);
            }
        }
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void NativeWorkAndInterruptionCutoffsKeepTheParentAndOriginalAuthority(bool extended, bool timeout)
    {
        var source = Encoding.UTF8.GetBytes(extended ? @"é${1:+\U😀:\x{10428}}\é" : "é😀$$$0");
        var options = extended ? Pcre2SubstitutionOptions.Extended : Pcre2SubstitutionOptions.None;
        ulong steps; var checkpoints = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = _ => checkpoints++;
            _ = Pcre2ReplacementPreparationControl.Prepare(new Utf8ConstructionPattern(source), options, false);
            steps = measured.Budget.WorkSteps;
        }
        for (ulong limit = 0; limit <= steps; limit++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, limit, ulong.MaxValue);
            owner.Budget.ReserveBytes(7);
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Pcre2ReplacementPreparationControl.Prepare(new Utf8ConstructionPattern(source), options, false));
                if (limit < steps) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(7UL, owner.Budget.ReservedBytes);
            Assert.InRange(owner.Budget.WorkSteps, 0UL, limit);
        }
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
            using var token = new CancellationTokenSource(); var calls = 0;
            long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            owner.Budget.CheckpointHook = _ => { if (++calls == target) { if (timeout) ticks = 10; else token.Cancel(); } };
            var invocation = new Utf8SliceInvocation(new(1_000_000, 1024, 1 << 20),
                timeout ? TimeSpan.FromMilliseconds(10) : Timeout.InfiniteTimeSpan, token.Token);
            using (var control = Pcre2ReplacementPreparationControl.Borrow(invocation, token.Token))
            {
                if (timeout) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() =>
                    Pcre2ReplacementPreparationControl.Prepare(new Utf8ConstructionPattern(source), options, false)).ErrorKind);
                else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                    Pcre2ReplacementPreparationControl.Prepare(new Utf8ConstructionPattern(source), options, false)).CancellationToken);
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
            _ = Pcre2ReplacementPreparationControl.Prepare(new Utf8ConstructionPattern(source), Pcre2SubstitutionOptions.Extended, false);
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
                    ulong.MaxValue, kind == Utf8ResourceLimitKind.ConstructionDepth ? limit : ulong.MaxValue);
                owner.Budget.ReserveBytes(7);
                using (var child = Utf8ConstructionScope.Enter())
                {
                    var failure = Record.Exception(() => Pcre2ReplacementPreparationControl.Prepare(
                        new Utf8ConstructionPattern(source), Pcre2SubstitutionOptions.Extended, false));
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
