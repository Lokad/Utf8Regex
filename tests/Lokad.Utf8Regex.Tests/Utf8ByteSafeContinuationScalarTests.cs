using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ByteSafeContinuationScalarTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8ExecutionTree Tree(string pattern)
    {
        var tree = Utf8ExecutionTreeLowerer.Lower(Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant).SemanticRegex);
        Assert.NotNull(tree); return tree;
    }
    private static readonly (string Pattern, string Input, int Length)[] Rewinding =
    [
        (@"^a*\b", "abc", 0), (@"^a+\B", "aa!", 1), (@"^a*\A", "a", 0),
        (@"(?m)^(?:a|\n)*$", "a\nb", 1), (@"^(?:a|\n)*$\b", "a\n", 1),
        (@"^a*(?:b)?\b", "aaac", 0), (@"^(?:ab)?\b", "abc", 0),
        (@"^(?:ab|cd)?\b", "abc", 0), (@"^(?:ab)?\A", "ab", 0),
        (@"(?m)^(?:\nabc)?$", "\nabcx", 0),
        (@"^(?:ab|cd)?a", "ab", 1), (@"^a*(?:bc|de)?b", "aaabc", 4),
        (@"a(?:\nb)?(?m:$)", "a\nbx", 1),
    ];
    private static readonly string[] Native =
    [@"^a+\b", @"^!+\b", @"^(?:a!)+\B", @"(?m)^a*$", @"(?m)^(?:a\n)*$", @"^a*$", @"^a*\z",
        @"^a*(?:b)?c$", @"^a*b?$", @"^(?:cat|dog)?$", @"^[,\s]+$", @"^(?:[A-Z]+[0-9]+(?:[,\s]+)?)+$"];

    [Fact]
    public void AssertionRewindsRequireOwnedScalarExecutionAndPublicResultsUseByteRanges()
    {
        foreach (var (pattern, inputText, length) in Rewinding)
        {
            var outcome = Utf8ByteSafeLinearVerifierProgram.Compile(Tree(pattern));
            Assert.False(outcome.Succeeded, pattern);
            var input = Encoding.UTF8.GetBytes(inputText);
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            {
                var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
                Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
                var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
                Assert.Equal((0, length), (match.IndexInBytes, match.LengthInBytes));
                Assert.Equal((0, length), (detailed.IndexInBytes, detailed.LengthInBytes));
            }
        }
        Assert.False(Utf8ByteSafeLinearVerifierProgram.Compile(Tree(@"^a*\G")).Succeeded);
    }

    [Fact]
    public void QualifiedBoundaryEndAndNullableSuffixControlsKeepNativePrograms()
    {
        foreach (var pattern in Native)
            Assert.True(Utf8ByteSafeLinearVerifierProgram.Compile(Tree(pattern)).Succeeded, pattern);
        foreach (var (pattern, subject, expected) in new[] { (@"^a+\b", "aaa😀", 3), (@"^!+\b", "!!a", 2),
            (@"^(?:a!)+\B", "a!?", 2), (@"(?m)^a*$", "aaa\nb", 3), (@"^a*\z", "aaa", 3) })
        {
            var program = Utf8ByteSafeLinearVerifierProgram.Compile(Tree(pattern)).Program;
            Assert.True(program.TryMatch(Encoding.UTF8.GetBytes(subject), 0, out var length, out var fallback));
            Assert.Equal(expected, length); Assert.False(fallback);
        }
    }

    [Fact]
    public void AcceptedContinuationsAgreeWithIndependentScalarVmAtEveryScalarBoundary()
    {
        var subjects = new[] { "", "a", "aa!", "abc", "aaac", "ab", "abcd", "cat", "dog", "catx", "!!a", "a!?", "a!a!a",
            "a\nb", "\nabcx", "a\na\nx", "aaa\nb", " \t", "AB12, CD34", "😀a", "aaa😀", "a\u0301", "éab" };
        var accepted = 0; var matches = 0; var dispatches = 0;
        foreach (var pattern in Native.Concat(Rewinding.Select(static value => value.Pattern)).Append(@"^a*\G"))
        {
            var outcome = Utf8ByteSafeLinearVerifierProgram.Compile(Tree(pattern)); if (!outcome.Succeeded) continue; accepted++;
            var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant); Assert.NotNull(tree);
            var owned = Utf8OwnedTreeSliceLowerer.TryLower(tree, RegexOptions.CultureInvariant); Assert.NotNull(owned);
            var starts = new int[owned.GroupCount]; var ends = new int[owned.GroupCount];
            foreach (var text in subjects)
            {
                var input = Encoding.UTF8.GetBytes(text);
                for (var boundary = 0; boundary <= input.Length; boundary++)
                {
                    if (boundary < input.Length && (input[boundary] & 0xc0) == 0x80) continue;
                    var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(100000, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
                    var expected = Utf8SliceRunner.TryMatchAt(owned, input, boundary, starts, ends, true, ref invocation);
                    Assert.Equal(Utf8ExecutionLimitKind.None, expected.Breach);
                    var actual = outcome.Program.TryMatch(input, boundary, out var length, out var fallback);
                    if (fallback) { dispatches++; Assert.True(input.Skip(boundary).Any(static value => value > 127)); continue; }
                    Assert.True(expected.IsMatch == actual, $"{pattern} / {text} / {boundary}");
                    if (actual) { matches++; Assert.Equal(expected.EndIndex - boundary, length); }
                }
            }
        }
        Assert.True(accepted >= Native.Length); Assert.True(matches >= 20); Assert.True(dispatches > 0);
    }

    private static bool Continuation(Utf8ExecutionNode choice, Utf8ExecutionNode[] suffix, bool nullable)
    {
        var type = typeof(Utf8ByteSafeLinearVerifierProgram).Assembly.GetType("Lokad.Utf8Regex.Internal.Execution.Utf8ByteSafeContinuationProof"); Assert.NotNull(type);
        var method = type.GetMethod("AllowsGreedyChoice", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(method);
        try { return Assert.IsType<bool>(method.Invoke(null, [choice, suffix, 0, nullable])); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner) { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
    }
    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, params Utf8ExecutionNode[] children) =>
        new(kind, RegexOptions.None, children, "", '\0', 0, 1, 1);

    [Fact]
    public void EveryContinuationByteAndWorkCutoffRestoresParentLoansAndSpentWork()
    {
        var choice = new Utf8ExecutionNode(Utf8ExecutionNodeKind.Loop, RegexOptions.None, [], "", 'a', 0, 1, 8);
        var suffix = new[] { Node(Utf8ExecutionNodeKind.Group, Node(Utf8ExecutionNodeKind.Boundary)), Node(Utf8ExecutionNodeKind.End) };
        ulong peak, work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Assert.True(Continuation(choice, suffix, false)); peak = measure.Budget.PeakBytes; work = measure.Budget.WorkSteps; Assert.Equal(0ul, measure.Budget.ReservedBytes); }
        Assert.InRange(peak, 1ul, 10000ul); Assert.InRange(work, 1ul, 10000ul);
        foreach (var bytes in new[] { false, true })
        {
            var maximum = bytes ? peak : work; var successes = 0;
            for (var cutoff = 0ul; cutoff <= maximum; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
                var failure = Record.Exception(() => Assert.True(Continuation(choice, suffix, false)));
                if (failure is null) successes++;
                else Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.Depth);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
                Assert.True(bytes ? owner.Budget.PeakBytes <= 128 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
            }
            Assert.True(successes > 0); if (!bytes) Assert.Equal(1, successes);
        }
    }

    [Fact]
    public void ContinuationDeclineAndRealWrapperDepthUnwindWithoutAdmittingAnotherOwner()
    {
        var choice = new Utf8ExecutionNode(Utf8ExecutionNodeKind.Loop, RegexOptions.None, [], "", 'a', 0, 0, 8);
        var end = Node(Utf8ExecutionNodeKind.End);
        for (var index = 0; index < 65; index++) end = Node(Utf8ExecutionNodeKind.Group, end);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Continuation(choice, [end], false));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind); Assert.Equal(9ul, failure.Requested);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.False(Continuation(choice, [Node(Utf8ExecutionNodeKind.Boundary)], false));
        Assert.False(Continuation(choice, [Node(Utf8ExecutionNodeKind.Unsupported)], false));
        Assert.True(Continuation(choice, [Node(Utf8ExecutionNodeKind.End)], false));
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
    }

    [Fact]
    public void LastScalarFactsCoverEveryNullableBranchAndCompleteLexicalCaseFiber()
    {
        var method = typeof(Utf8ByteSafeLinearVerifierProgram).Assembly.GetType("Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionPrefixFacts")!
            .GetMethod("TryLastMask", BindingFlags.NonPublic | BindingFlags.Static); Assert.NotNull(method);
        (bool Success, AsciiCharClass Mask, bool Nullable) Last(Utf8ExecutionNode root)
        {
            object?[] args = [root, null, false, true];
            var success = Assert.IsType<bool>(method.Invoke(null, args));
            return (success, Assert.IsType<AsciiCharClass>(args[1]), Assert.IsType<bool>(args[2]));
        }
        var a = new Utf8ExecutionNode(Utf8ExecutionNodeKind.One, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, [], "", 'a', 0, 1, 1);
        var b = new Utf8ExecutionNode(Utf8ExecutionNodeKind.Loop, RegexOptions.None, [], "", 'b', 0, 0, 1);
        var facts = Last(Node(Utf8ExecutionNodeKind.Concatenate, a, b));
        Assert.True(facts.Success); Assert.False(facts.Nullable); Assert.Equal(3, facts.Mask.Count);
        Assert.True(facts.Mask.Contains((byte)'a')); Assert.True(facts.Mask.Contains((byte)'A')); Assert.True(facts.Mask.Contains((byte)'b'));
        facts = Last(Node(Utf8ExecutionNodeKind.Alternate, Node(Utf8ExecutionNodeKind.Empty), a));
        Assert.True(facts.Success); Assert.True(facts.Nullable); Assert.Equal(2, facts.Mask.Count);
        var k = new Utf8ExecutionNode(Utf8ExecutionNodeKind.One, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, [], "", 'k', 0, 1, 1);
        facts = Last(Node(Utf8ExecutionNodeKind.Alternate, a, k)); Assert.False(facts.Success); Assert.True(facts.Mask.IsEmpty);
        facts = Last(Node(Utf8ExecutionNodeKind.Alternate, a, Node(Utf8ExecutionNodeKind.Unsupported)));
        Assert.False(facts.Success); Assert.True(facts.Mask.IsEmpty);
    }
}
