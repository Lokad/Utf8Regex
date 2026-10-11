using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReParserContinuationTests
{
    public static IEnumerable<object[]> NestedGroups()
    {
        foreach (var depth in new[] { 129, 16385 })
            foreach (var opener in new[] { "(", "(?:", "(?>", "(?=", "(?!", "(?<=", "(?<!", "(?s:", "(?-s:" })
                yield return [depth, opener];
    }

    [Theory]
    [MemberData(nameof(NestedGroups))]
    public void RawDeepGroupsPreserveScalarAtomsMetadataAndLexicalScopes(int depth, string opener)
    {
        var pattern = string.Concat(Enumerable.Repeat(opener, depth)) + "." + new string(')', depth);
        using var scope = Utf8ConstructionScope.Enter();
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.DotAll);
        Assert.Equal(opener == "(" ? depth : 0, parse.CaptureGroupCount);
        Assert.Equal(opener is "(?<=" or "(?<!", parse.HasLookbehind);
        var node = parse.Root;
        for (var level = 0; level < depth; level++)
        {
            var group = Assert.IsType<PythonReGroupNode>(node);
            if (opener == "(")
            {
                Assert.Equal(level + 1, group.GroupNumber);
                Assert.Same(group.Inner, parse.GroupBodies[group.GroupNumber]);
            }
            node = group.Inner;
        }
        var atom = Assert.IsType<PythonReRawNode>(node);
        Assert.Equal(PythonReRawKind.Dot, atom.Kind);
        Assert.Equal(opener == "(?-s:" ? "(?!\\n)" + PythonReParser.ManagedUnicodeScalarPattern
            : PythonReParser.ManagedUnicodeScalarPattern, atom.Text);
        Assert.InRange(scope.Budget.PeakDepth, (ulong)depth, Utf8ConstructionScope.DefaultDepth);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.True(scope.Budget.ReservedBytes > 0); // Successful AST loans stay with the owner.
        Assert.Equal(0ul, scope.Budget.Reserved);
    }

    [Theory]
    [InlineData(129)] [InlineData(16385)]
    public void DeepConditionalsResumeBothBranchesWithoutFlatteningNestedAlternation(int depth)
    {
        var pattern = "(a)" + string.Concat(Enumerable.Repeat("(?(1)", depth)) + "😀+" +
            string.Concat(Enumerable.Repeat("|(?:x|y))", depth));
        using var scope = Utf8ConstructionScope.Enter();
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        var sequence = Assert.IsType<PythonReSequenceNode>(parse.Root);
        var node = sequence.Elements[1];
        for (var level = 0; level < depth; level++)
        {
            var conditional = Assert.IsType<PythonReConditionalNode>(node);
            Assert.Equal(1, conditional.Reference.GroupNumber);
            var no = Assert.IsType<PythonReGroupNode>(conditional.NoBranch);
            Assert.IsType<PythonReAlternationNode>(no.Inner);
            node = conditional.YesBranch;
        }
        var repeat = Assert.IsType<PythonReQuantifierNode>(node);
        Assert.Equal(1, repeat.Min);
        Assert.Null(repeat.Max);
        Assert.Equal("😀", Assert.IsType<PythonReRawNode>(repeat.Inner).Text);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.True(scope.Budget.ReservedBytes > 0);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(8)]
    public void ParserDepthDenialRestoresParentLoansBeforeAnotherParse(int maximum)
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, (ulong)maximum + 1);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        scope.Budget.EnterDepth();
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            new PythonReParser(new string('(', 64) + "😀" + new string(')', 64)).Parse(PythonReCompileOptions.None));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal((ulong)maximum + 1, failure.Limit);
        Assert.Equal(failure.Limit + 1, failure.Requested);
        Assert.Equal(1ul, scope.Budget.Depth);
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        // With no child frame available, only the held parent's array
        // initialization was admitted; no parser resume may execute.
        if (maximum == 0) Assert.Equal(1ul, scope.Budget.WorkSteps);
        else Assert.True(scope.Budget.WorkSteps > 1);
        if (maximum >= 2)
            Assert.IsType<PythonReLiteralNode>(new PythonReParser("a").Parse(PythonReCompileOptions.None).Root);
        scope.Budget.LeaveDepth();
        scope.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData("(?P<x>😀)(?(x)(?s:.)|(?:a|b))")]
    [InlineData("(?x:a # comment\n| (?-x: ))")]
    [InlineData("(a)(?(1)|b)")]
    public void EveryParserWorkCutoffRestoresParentLoansAndDoesNotRefundWork(string pattern)
    {
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            _ = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
            work = measured.Budget.WorkSteps;
        }
        Assert.InRange(work, 1ul, 512ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cutoff + 1, ulong.MaxValue);
            var parent = scope.Budget.AllocateArray<byte>(1, false);
            scope.Budget.EnterDepth();
            var failure = Record.Exception(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            Assert.Equal(1ul, scope.Budget.Depth);
            if (cutoff < work)
            {
                var limit = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, limit.ResourceKind);
                Assert.True(limit.Requested > limit.Limit);
                Assert.True(scope.Budget.WorkSteps <= cutoff + 1);
                Assert.Equal(48ul, scope.Budget.ReservedBytes);

            }
            else
            {
                Assert.Null(failure);
                Assert.True(scope.Budget.ReservedBytes > 48);
                Assert.True(scope.Budget.ReservedBytes > 0);
            }
            scope.Budget.LeaveDepth();
            scope.Budget.ReleaseArray(parent, false);
        }
    }

    [Theory]
    [InlineData("(", "😀", ")", "missing ), unterminated subpattern", 0)]
    [InlineData("(?:", "😀", ")", "missing ), unterminated subpattern", 0)]
    [InlineData("(?s:", "😀", ")", "missing ), unterminated subpattern", 0)]
    [InlineData("(?:", "*", ")", "nothing to repeat", 3)]
    [InlineData("(?:", "a**", ")", "multiple repeat", 5)]
    public void DeepGrammarFailuresKeepThePythonErrorAndReleaseAllFrames(string opener, string body, string closer, string message, int offset)
    {
        const int depth = 16385;
        var prefix = string.Concat(Enumerable.Repeat(opener, depth));
        var pattern = prefix + body + string.Concat(Enumerable.Repeat(closer, message.StartsWith("missing") ? depth - 1 : depth));
        using var scope = Utf8ConstructionScope.Enter();
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<PythonRePatternException>(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
        Assert.StartsWith(message, failure.Message);
        Assert.Equal(message.StartsWith("missing") ? 0 : prefix.Length - opener.Length + offset, failure.Position);
        var spent = scope.Budget.WorkSteps;
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.IsType<PythonReLiteralNode>(new PythonReParser("a").Parse(PythonReCompileOptions.None).Root);
        Assert.True(scope.Budget.WorkSteps > spent);
        Assert.True(scope.Budget.ReservedBytes >= 48ul + 128ul);
        Assert.Equal(0, parent[0]);
        scope.Budget.ReleaseArray(parent, false);
    }

    [Fact]
    public void ExhaustedParserFrameCapacityRejectsBeforeParsingAndRecoversInANewOwner()
    {
        using (var scope = new Utf8ConstructionScope(ulong.MaxValue, 48, ulong.MaxValue, ulong.MaxValue))
        {
            var parent = scope.Budget.AllocateArray<byte>(1, false);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                new PythonReParser("(?P<x>😀)").Parse(PythonReCompileOptions.None));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(48ul, scope.Budget.ReservedBytes);
            Assert.Equal(0ul, scope.Budget.Depth);
            scope.Budget.ReleaseArray(parent, false);
        }
        Assert.Equal(1, new PythonReParser("(?P<x>😀)").Parse(PythonReCompileOptions.None).CaptureGroupCount);
    }
}
