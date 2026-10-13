using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DfaOptionalLoopTests
{
    [Theory]
    [InlineData(Utf8ResourceLimitKind.ConstructionBytes)]
    [InlineData(Utf8ResourceLimitKind.ConstructionWorkSteps)]
    [InlineData(Utf8ResourceLimitKind.ConstructionDepth)]
    [InlineData(Utf8ResourceLimitKind.ConstructionInstructionCapacity)]
    public void OptionalGraphDenialPreservesTheOriginalOwnerAndCallerProgram(Utf8ResourceLimitKind kind)
    {
        var input = new Utf8ByteSafeLinearVerifierProgram([
            Utf8ByteSafeLinearVerifierStep.LoopProgram([
                Utf8ByteSafeLinearVerifierStep.LoopAsciiClass(AsciiCharClass.ForByte((byte)'a'), 1, -1),
                Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'b')], 0, 1),
            Utf8ByteSafeLinearVerifierStep.Accept()]);
        using var parent = new Utf8ConstructionScope(
            kind == Utf8ResourceLimitKind.ConstructionInstructionCapacity ? 0 : Utf8ConstructionScope.DefaultInstructionCapacity,
            kind == Utf8ResourceLimitKind.ConstructionBytes ? 48 : Utf8ConstructionScope.DefaultBufferBytes,
            kind == Utf8ResourceLimitKind.ConstructionWorkSteps ? 1 : Utf8ConstructionScope.DefaultWorkSteps,
            kind == Utf8ResourceLimitKind.ConstructionDepth ? 0 : Utf8ConstructionScope.DefaultDepth);
        var loan = parent.Budget.AllocateArray<byte>(1, false);
        loan[0] = 37;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8ByteSafeLazyDfaVerifierProgram.Compile(input));
        Assert.Equal(kind, failure.ResourceKind);
        Assert.Equal(48ul, parent.Budget.ReservedBytes);
        Assert.Equal(0ul, parent.Budget.Reserved);
        Assert.Equal(0ul, parent.Budget.Depth);
        Assert.Equal(37, loan[0]);
        Assert.Equal(2, input.Steps.Length);
        Assert.Equal(2, input.Steps[0].Program.Length);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OptionalBodyKeepsItsInnerSingleIterationBound(bool prefix)
    {
        var atom = Utf8ByteSafeLinearVerifierStep.LoopAsciiClass(AsciiCharClass.ForByte((byte)'a'), 0, 1);
        Utf8ByteSafeLinearVerifierStep[] body = prefix
            ? [Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'x'), atom, Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'b')]
            : [atom, Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'b')];
        var outcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(new([
            Utf8ByteSafeLinearVerifierStep.RequireBeginning(RegexOptions.None),
            Utf8ByteSafeLinearVerifierStep.LoopProgram(body, 0, 1),
            Utf8ByteSafeLinearVerifierStep.RequireEnd(RegexOptions.None),
            Utf8ByteSafeLinearVerifierStep.Accept()]));
        Assert.True(outcome.Succeeded);
        var oracle = new Regex("^(?:" + (prefix ? "x" : "") + "a?b)?$", RegexOptions.CultureInvariant);
        foreach (var text in new[] { "", "a", "b", "ab", "aab", "aaab", "xa", "xb", "xab", "xaab", "xaaab", "\n", "ab\n", "😀" })
        {
            var expected = oracle.Match(text);
            Assert.Equal(expected.Success, outcome.Program.TryMatch(Encoding.UTF8.GetBytes(text), 0, out var width));
            Assert.Equal(expected.Success ? expected.Length : 0, width);
        }
    }

    [Theory]
    [InlineData(false, false, -1)] [InlineData(false, true, -1)]
    [InlineData(true, false, -1)] [InlineData(true, true, -1)]
    [InlineData(false, false, int.MaxValue)] [InlineData(false, true, int.MaxValue)]
    [InlineData(true, false, int.MaxValue)] [InlineData(true, true, int.MaxValue)]
    public void OptionalUnboundedBodiesCompileWithoutPrematureAcceptance(bool repeatedBody, bool prefix, int maximum)
    {
        var a = Utf8ByteSafeLinearVerifierStep.LoopAsciiClass(AsciiCharClass.ForByte((byte)'a'), 1, maximum);
        var body = repeatedBody
            ? Utf8ByteSafeLinearVerifierStep.LoopProgram([a,
                Utf8ByteSafeLinearVerifierStep.LoopAsciiClass(AsciiCharClass.ForByte((byte)'b'), 1, maximum)], 1, maximum)
            : a;
        var suffix = (byte)(repeatedBody ? 'c' : 'b');
        Utf8ByteSafeLinearVerifierStep[] optionalBody = prefix
            ? [Utf8ByteSafeLinearVerifierStep.MatchByte((byte)'x'), body, Utf8ByteSafeLinearVerifierStep.MatchByte(suffix)]
            : [body, Utf8ByteSafeLinearVerifierStep.MatchByte(suffix)];
        var linear = new Utf8ByteSafeLinearVerifierProgram([
            Utf8ByteSafeLinearVerifierStep.RequireBeginning(RegexOptions.None),
            Utf8ByteSafeLinearVerifierStep.LoopProgram(optionalBody, 0, 1),
            Utf8ByteSafeLinearVerifierStep.RequireEnd(RegexOptions.None),
            Utf8ByteSafeLinearVerifierStep.Accept()]);
        var pattern = "^(?:" + (prefix ? "x" : "") + (repeatedBody ? "(?:a+b+)+c" : "a+b") + ")?$";
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var outcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(linear);
        Assert.True(outcome.Succeeded);
        foreach (var text in new[] { "", "a", "b", "c", "ab", "aab", "aaab", "abab", "abbabb", "abc", "aabc",
            "ababc", "ababcc", "xab", "xaab", "xabab", "xabc", "xaabc", "xababc", "xababcc", "\n", "ab\n", "ab😀", "😀" })
        {
            var expected = oracle.Match(text);
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(expected.Success, outcome.Program.TryMatch(input, 0, out var width));
            Assert.Equal(expected.Success ? Encoding.UTF8.GetByteCount(expected.Value) : 0, width);
        }
    }

    [Theory]
    [InlineData(-1)] [InlineData(int.MaxValue)]
    public void AlternativeGraphsRespectBothOpenEndedSentinels(int maximum)
    {
        var outcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(new([
            Utf8ByteSafeLinearVerifierStep.LoopAnyText(["ab"u8.ToArray(), "cd"u8.ToArray()], 1, maximum),
            Utf8ByteSafeLinearVerifierStep.Accept()]));
        Assert.True(outcome.Succeeded);
        foreach (var text in new[] { "", "ab", "cd", "abcd", "ababcd", "abx", "ax", "😀" })
        {
            var expected = new Regex("(?:ab|cd)+", RegexOptions.CultureInvariant).Match(text);
            var atStart = expected.Success && expected.Index == 0;
            Assert.Equal(atStart, outcome.Program.TryMatch(Encoding.UTF8.GetBytes(text), 0, out var width));
            Assert.Equal(atStart ? expected.Length : 0, width);
        }
    }
}
