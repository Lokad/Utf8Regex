using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFullMatchAcceptanceTests
{
    [Theory]
    [InlineData("", "", true)]
    [InlineData("a|ab", "ab", true)]
    [InlineData("a|ab", "abc", false)]
    [InlineData("(?>a|ab)", "ab", false)]
    [InlineData("a*?", "aaa", true)]
    [InlineData("a$", "a\n", false)]
    [InlineData("(?P<x>😀|a)+?", "😀a", true)]
    [InlineData("(?=(a+))a*", "aaa", true)]
    [InlineData(@"\w+", "éa", true)]
    [InlineData(@"\w+", "éa\n", false)]
    [InlineData(@"\w{1,3}", "aaaa", false)]
    [InlineData(@"\w{1,3}", "aaa", true)]
    [InlineData(@"(?P<x>\w+)", "éa", true)]
    [InlineData(@"(?P<x>.+)", "😀a", true)]
    [InlineData(@"(?s:.+)", "😀\n", true)]
    [InlineData(@"[\s\S]+", "😀\n", true)]
    [InlineData(@"(?<=(a))b", "b", false)]
    public void EndpointConstraintMatchesIndependentAnchoredLowering(string pattern, string text, bool success)
    {
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        _ = PythonReTranslator.Translate(parse, false);
        var program = PythonReScalarCompiler.Compile(parse);
        var anchored = PythonReScalarCompiler.Compile(parse with
        {
            Root = new PythonReSequenceNode([parse.Root, new PythonReRawNode(@"\z", PythonReRawKind.Escape)]),
        });
        var bytes = Encoding.UTF8.GetBytes(text);
        var expected = Run(anchored, bytes, false);
        var actual = Run(program, bytes, true);
        Assert.Equal(success, actual.Outcome.IsMatch);
        Assert.Equal(expected.Outcome, actual.Outcome);
        Assert.Equal(expected.Starts, actual.Starts);
        Assert.Equal(expected.Ends, actual.Ends);

        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(success, regex.FullMatch(bytes).Success);
        Assert.Equal(success, regex.FullMatchDetailed(bytes).Success);
        Assert.Equal(success, regex.FullMatchDetailedData(bytes).Success);
        Assert.Equal(success ? text : null, regex.FullMatchToString(bytes));
    }

    private static (Utf8SliceMatchOutcome Outcome, int[] Starts, int[] Ends) Run(
        Utf8SliceProgram program, byte[] input, bool full)
    {
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        try
        {
            var outcome = Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends,
                false, false, true, ref workspace, ref invocation, full);
            return (outcome, starts, ends);
        }
        finally
        {
            workspace.Release(ref invocation);
            Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
        }
    }
}
