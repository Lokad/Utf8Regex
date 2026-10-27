using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFullProgramReuseTests
{
    [Theory]
    [InlineData("")]
    [InlineData("a|ab")]
    [InlineData("(?P<x>😀|a)+?")]
    [InlineData("(a)(?P<x>.)(?<=\\1)(?(x)(?:é|b)+|x)")]
    [InlineData("(?i:(?P<x>[a-z]+))(?P=x)")]
    [InlineData("(?>a|ab)b")]
    [InlineData("(?=(a+))a*")]
    [InlineData("[^😀]+$")]
    public void DerivedProgramMatchesIndependentAnchoredLowering(string pattern)
    {
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        _ = PythonReTranslator.Translate(parse, false);
        var original = PythonReScalarCompiler.Compile(parse);
        var anchored = PythonReScalarCompiler.Compile(parse with
        {
            // The shared leaf parser spells absolute EOF as \z.
            Root = new PythonReSequenceNode([parse.Root, new PythonReRawNode(@"\z", PythonReRawKind.Escape)]),
        });
        var derived = PythonReScalarCompiler.RequireFullMatch(original);

        Assert.Equal(anchored.Instructions, derived.Instructions);
        Assert.Equal(anchored.GroupNames, derived.GroupNames);
        Assert.Equal(anchored.GroupNumbers, derived.GroupNumbers);
        Assert.Equal(anchored.RepeatCount, derived.RepeatCount);
        Assert.Equal(anchored.GroupCount, derived.GroupCount);
        Assert.Equal(anchored.UsesCaptureOpeningJournal, derived.UsesCaptureOpeningJournal);
        Assert.Equal(anchored.CanMatchEmpty, derived.CanMatchEmpty);
        Assert.Same(original.SetMasks, derived.SetMasks);
        Assert.Same(original.CategorySetMembers, derived.CategorySetMembers);
        Assert.Same(original.Assertions, derived.Assertions);
        Assert.Same(original.BackreferenceLowerings, derived.BackreferenceLowerings);
        Assert.Same(original.GroupNames, derived.GroupNames);
        Assert.Equal(Utf8SliceInstructionKind.Accept, original.Instructions[^1].Kind);
    }

    [Theory]
    [InlineData("a|ab", "ab", true)]
    [InlineData("a|ab", "abc", false)]
    [InlineData("(?>a|ab)", "ab", false)]
    [InlineData("a*?", "aaa", true)]
    [InlineData("a$", "a\n", false)]
    [InlineData("(?P<x>😀|a)+?", "😀a", true)]
    [InlineData("", "", true)]
    [InlineData(@"\w+", "éa", true)]
    [InlineData(@"\w+", "éa\n", false)]
    [InlineData(@"\w{1,3}", "aaaa", false)]
    [InlineData(@"\w{1,3}", "aaa", true)]
    [InlineData(@"(?P<x>\w+)", "éa", true)]
    [InlineData(@"(?P<x>.+)", "😀a", true)]
    [InlineData(@"(?s:.+)", "😀\n", true)]
    [InlineData(@"[\s\S]+", "😀\n", true)]
    [InlineData(@"(?<=(a))b", "b", false)]
    public void EndRequirementParticipatesInBacktracking(string pattern, string input, bool success)
    {
        var regex = new Utf8PythonRegex(pattern);
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        Assert.Equal(success, regex.FullMatch(bytes).Success);
        Assert.Equal(success, regex.FullMatchDetailed(bytes).Success);
        Assert.Equal(success, regex.FullMatchDetailedData(bytes).Success);
        Assert.Equal(success ? input : null, regex.FullMatchToString(bytes));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void DerivationAdmissionRollsBackWithoutChangingBorrowedProgram(int limit)
    {
        var parse = new PythonReParser("(?P<x>[é😀]+)(?=a)a").Parse(PythonReCompileOptions.None);
        var original = PythonReScalarCompiler.Compile(parse);
        var previous = original.Instructions.ToArray();
        using var owner = new Utf8ConstructionScope(
            limit == 0 ? 1ul : ulong.MaxValue,
            limit == 1 ? 1ul : ulong.MaxValue,
            limit == 2 ? 1ul : ulong.MaxValue,
            ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            PythonReScalarCompiler.RequireFullMatch(original));
        Assert.Equal(limit switch
        {
            0 => Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            1 => Utf8ResourceLimitKind.ConstructionBytes,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps,
        }, failure.ResourceKind);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(previous, original.Instructions);
    }
}
