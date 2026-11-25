using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2RecursiveControlFlowCompilerTests
{
    [Theory]
    [InlineData("(abc)(?1)", "abcabc", true)]
    [InlineData("(a)(?-1)", "aa", true)]
    [InlineData("(a)(?+1)(b)", "abb", true)]
    [InlineData("(?<word>ab)(?&word)", "abab", true)]
    [InlineData("(?<word>ab)(?P>word)", "abab", true)]
    [InlineData("(?|(abc)|(xyz))(?1)", "xyzabc", true)]
    [InlineData("(?|(abc)|(xyz))(?1)", "xyzxyz", false)]
    public void NumberedRelativeAndNamedCallsUseTheGenericProgram(
        string pattern,
        string input,
        bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("aba", true)]
    [InlineData("abcba", true)]
    [InlineData("abca", false)]
    public void WholePatternRecursionBacktracksWithInvocationLocalCaptures(string input, bool expected)
    {
        var regex = new Utf8Pcre2Regex("^(.|(.)(?1)\\2)$");

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(@"a(?R)?b", "aabb", true, 1)]
    [InlineData(@"a(?&x)?b(?(DEFINE)(?<x>x(?0)?y))", "axabyb", true, 2)]
    [InlineData(@"a(?&x)b(?(DEFINE)(?<x>x😀y))", "ax😀yb", false, 1)]
    public void WholePatternCallBodyIsEmittedOnlyForAWholePatternCall(string pattern, string text, bool wholeCall, int bodyCount)
    {
        using var regex = new Utf8Pcre2Regex(pattern);
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match).Program;
        Assert.Equal(wholeCall, program.Instructions.Any(instruction =>
            instruction.Kind == Pcre2BacktrackingInstructionKind.SubroutineCall && instruction.SubroutineSlot == 0));
        Assert.Equal(bodyCount, program.Instructions.Count(instruction => instruction.Kind == Pcre2BacktrackingInstructionKind.SubroutineReturn));

        var input = Encoding.UTF8.GetBytes(text);
        using var token = new CancellationTokenSource();
        var match = regex.Match(input, cancellationToken: token.Token);
        Assert.True(match.Success);
        Assert.Equal((0, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
    }

    [Fact]
    public void AssertionAndMainProgramEmitOnlyTheirReachableCallBodies()
    {
        using var regex = new Utf8Pcre2Regex(
            @"\A(?=(?&first))(?&second)\z(?(DEFINE)(?<first>(?&second))(?<second>é)(?<unused>y))");
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match).Program;
        var assertion = Assert.Single(program.AssertionPrograms);
        var mainCall = Assert.Single(program.Instructions, instruction => instruction.Kind == Pcre2BacktrackingInstructionKind.SubroutineCall);
        Assert.Equal(2, mainCall.SubroutineSlot);
        Assert.InRange(mainCall.PrimaryTarget, 1, program.Instructions.Length - 1);
        Assert.Single(program.Instructions, instruction => instruction.Kind == Pcre2BacktrackingInstructionKind.SubroutineReturn);

        var assertionCalls = assertion.Instructions.Where(instruction => instruction.Kind == Pcre2BacktrackingInstructionKind.SubroutineCall).ToArray();
        Assert.Equal(new[] { 1, 2 }, assertionCalls.Select(instruction => instruction.SubroutineSlot));
        Assert.All(assertionCalls, call => Assert.InRange(call.PrimaryTarget, 1, assertion.Instructions.Length - 1));
        Assert.Equal(2, assertion.Instructions.Count(instruction => instruction.Kind == Pcre2BacktrackingInstructionKind.SubroutineReturn));

        Assert.Equal(3, regex.CaptureGroupCount);
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[4];
        var match = regex.Match("é"u8, captures: captures);
        Assert.True(match.Success);
        Assert.Equal((0, 2), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.False(captures[1].Success);
        Assert.False(captures[2].Success);
        Assert.False(captures[3].Success);
        Assert.False(regex.IsMatch("y"u8));
    }

    [Theory]
    [InlineData(@"\A(?&first)\z(?(DEFINE)(?<first>a)(?<unused>(?&missing)))", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData(@"\A(?&first)\z(?(DEFINE)(?<first>a)(?<unused>(?(R&missing)a|b)))", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData(@"\A(?&first)\z(?(DEFINE)(?<first>a)(?<unused>\99))", Pcre2ErrorKind.UnrecognizedEscape)]
    [InlineData(@"\A(?&first)\z(?(DEFINE)(?<first>a)(?<unused>(?(99)a|b)))", Pcre2ErrorKind.UnrecognizedEscape)]
    [InlineData(@"\Aa\z(?(DEFINE)(?<unused>(?&missing)))", Pcre2ErrorKind.InvalidAfterParensQuery)]
    public void UnusedBodiesStillValidateReferences(string pattern, Pcre2ErrorKind expected)
    {
        var error = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern));
        Assert.Equal(expected, error.ErrorKind);
    }

    [Fact]
    public void DefineBlocksCanHoldMultipleForwardReferencedDefinitions()
    {
        var regex = new Utf8Pcre2Regex("^(?&left)(?&right)(?(DEFINE)(?<left>a|x)(?<right>b|y))$");
        ReadOnlySpan<byte> match1Input = "ab"u8;
        var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];

        var match = regex.Match(match1Input, captures: match1Captures);

        Assert.True(match.Success);
        // A returned subroutine restores caller captures, including unset slots.
        Assert.False(match1Captures[1].Success);
        Assert.False(match1Captures[2].Success);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.False(regex.IsMatch("ax"u8));
    }

    [Theory]
    [InlineData("ab", true)]
    [InlineData("c", true)]
    [InlineData("ac", false)]
    [InlineData("b", false)]
    public void CaptureSetConditionSelectsExactlyOneBranch(string input, bool expected)
    {
        var regex = new Utf8Pcre2Regex("^(a)?(?(1)b|c)$");

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void NamedDuplicateCaptureConditionUsesTheFirstSetSlot()
    {
        var regex = new Utf8Pcre2Regex("^(?J)(?:(?<x>a)|(?<x>b))(?(x)c|d)$");

        Assert.True(regex.IsMatch("ac"u8));
        Assert.True(regex.IsMatch("bc"u8));
        Assert.False(regex.IsMatch("ad"u8));
    }

    [Fact]
    public void AssertionConditionCapturesFlowThroughEitherDecision()
    {
        var positive2Regex = new Utf8Pcre2Regex("^(?(?=(a))abc|def)");
        ReadOnlySpan<byte> positive2Input = "abc"u8;
        var positive2Captures = new Utf8Pcre2CaptureRange[positive2Regex.CaptureGroupCount + 1];
        var positive = positive2Regex.Match(positive2Input, captures: positive2Captures);
        var negative3Regex = new Utf8Pcre2Regex("^(?(?!(a))def|abc)");
        ReadOnlySpan<byte> negative3Input = "abc"u8;
        var negative3Captures = new Utf8Pcre2CaptureRange[negative3Regex.CaptureGroupCount + 1];
        var negative = negative3Regex.Match(negative3Input, captures: negative3Captures);

        Assert.True(positive.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((positive2Captures[1].Success ? positive2Input.Slice(positive2Captures[1].StartOffsetInBytes, positive2Captures[1].EndOffsetInBytes - positive2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.True(negative.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((negative3Captures[1].Success ? negative3Input.Slice(negative3Captures[1].StartOffsetInBytes, negative3Captures[1].EndOffsetInBytes - negative3Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("b", true)]
    [InlineData("ba", true)]
    [InlineData("baaa", true)]
    [InlineData("a", false)]
    public void NamedRecursionConditionDistinguishesACallFromTopLevel(string input, bool expected)
    {
        var regex = new Utf8Pcre2Regex("^(?<r>(?(R&r)a|b)(?&r)?)$");

        Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("R&outer")]
    [InlineData("R1")]
    public void RecursionConditionTestsOnlyTheInnermostCall(string condition)
    {
        var pattern = $@"\A(?&outer)(?(R)u|v)\z(?(DEFINE)(?<outer>(?&inner)(?({condition})z|w))(?<inner>(?({condition})x|y)))";
        using var regex = new Utf8Pcre2Regex(pattern);

        Assert.True(regex.IsMatch("yzv"u8));
        Assert.False(regex.IsMatch("xzv"u8));
        Assert.False(regex.IsMatch("yzu"u8));
    }

    [Fact]
    public void RecursionConditionRecognizesEverySlotWithADuplicateName()
    {
        using var regex = new Utf8Pcre2Regex(@"(?J)\A(?2)\z(?(DEFINE)(?<g>a)(?<g>(?(R&g)b|c)))");

        Assert.True(regex.IsMatch("b"u8));
        Assert.False(regex.IsMatch("c"u8));
    }

    [Theory]
    [InlineData(@"(?J)\A(?<R>a)(?(R)b|c)\z", "ab", "ac")]
    [InlineData(@"(?J)\A(?<R1>a)(?(R1)b|c)\z", "ab", "ac")]
    [InlineData(@"\A(?(R99)b|c)(?<R99>a)\z", "ca", "ba")]
    [InlineData(@"\A(?<Rex>a)(?(Rex)b|c)\z", "ab", "ac")]
    [InlineData(@"(?J)\A(?:(?<R>a)|(?<R>b))(?(R)c|d)\z", "bc", "bd")]
    public void BareConditionsPreferMatchingCaptureNames(string pattern, string hit, string miss)
    {
        using var regex = new Utf8Pcre2Regex(pattern);

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(hit)));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(miss)));
    }

    [Theory]
    [InlineData(@"\A(?&g)\z(?(DEFINE)(?<g>(?=(?(R&g)a|b)).))", "a", "b")]
    [InlineData(@"\A(?&g)\z(?(DEFINE)(?<g>(?=(?(R)a|b)).))", "a", "b")]
    [InlineData(@"\A(?&g)\z(?(DEFINE)(?<g>.(?<=(?(R&g)a|b))))", "a", "b")]
    [InlineData(@"\A(?&g)\z(?(DEFINE)(?<g>(?(?=(?(R&g)a|b))a|c)))", "a", "b")]
    [InlineData(@"\A(?&g)\z(?(DEFINE)(?<g>(?=(?&h)(?(R&g)z|w))..)(?<h>(?(R&g)x|y)))", "yz", "xz")]
    public void AssertionsInheritTheCurrentCallAndUseLocalCallsWhenPresent(string pattern, string hit, string miss)
    {
        using var regex = new Utf8Pcre2Regex(pattern);

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(hit)));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(miss)));
    }

    [Fact]
    public void RecursiveCapturesDriveReplacementAndGlobalIteration()
    {
        var regex = new Utf8Pcre2Regex("(?<pair>a(?&pair)?b)");

        Assert.Equal(2, regex.Count("aabb ab"u8));
        Assert.Equal("[aabb] [ab]", Encoding.UTF8.GetString(regex.Replace("aabb ab"u8, "[${pair}]"u8)));
    }

    [Fact]
    public void RecursiveCallsChargeInvocationLimitsAndRemainConcurrent()
    {
        var limited = new Utf8Pcre2Regex("^(?<r>a(?&r)?b)$", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 2
            },
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
        });
        Assert.Equal(
            Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() => limited.IsMatch("aaabbb"u8)).ErrorKind);

        var regex = new Utf8Pcre2Regex("^(?<r>a(?&r)?b)$");
        Parallel.For(0, 256, index =>
        {
            var input = index % 2 == 0 ? "ab"u8 : "aaabbb"u8;
            Assert.True(regex.IsMatch(input));
        });
    }

    [Fact]
    public void RecursiveCallsChargeForScanningUnusedRepeatSlots()
    {
        const string prefix = @"\A(?:(?<r>x(?&r)?y)|z";
        const string repeat = "(?:ab){1,3}w";
        const string suffix = @")\z";
        const string input = "xxxxxxxxxxyyyyyyyyyy";
        var pattern = prefix + string.Concat(Enumerable.Repeat(repeat, 512)) + suffix;
        var policy = new Utf8RegexPolicy { ResourcePolicy = new() { MaxWorkSteps = 100 } };
        var bytes = Encoding.UTF8.GetBytes(input);

        using var small = new Utf8Pcre2Regex(prefix + repeat + suffix, policy: policy);
        using var large = new Utf8Pcre2Regex(pattern, policy: policy);
        using var unlimited = new Utf8Pcre2Regex(pattern);
        Assert.True(small.IsMatch(bytes));
        Assert.True(unlimited.IsMatch(bytes));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => large.IsMatch(bytes)).ErrorKind);
    }
}
