using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ExecutionPrefixScalarTests
{
    private static Utf8ExecutionTree Tree(string pattern, RegexOptions options = RegexOptions.CultureInvariant)
    {
        var tree = Utf8ExecutionTreeLowerer.Lower(Utf8FrontEnd.Analyze(pattern, options).SemanticRegex);
        Assert.NotNull(tree);
        return tree;
    }

    private static Utf8SearchPlan EmptyPlan() => Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.None));

    [Fact]
    public void GuardsCoverNestedWideScalarsAndRawLexicalCaseFibers()
    {
        foreach (var row in new (string Pattern, string Subject)[]
        {
            ("(ab.)cd", "ab😀cd"), ("(ab[é😀])cd", "abécd"), ("(ab(?i:k))cd", "abKcd"),
            ("(ab😀)cd", "ab😀cd"), ("(?i:abc)de", "ABCde"),
        })
        {
            var guards = Utf8DeterministicVerifierGuards.Create(Tree(row.Pattern), EmptyPlan());
            Assert.True(guards.Matches(Encoding.UTF8.GetBytes(row.Subject), 0), row.Pattern);
        }
        var raw = new Utf8ExecutionTree(new(Utf8ExecutionNodeKind.Multi, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            Array.Empty<Utf8ExecutionNode>(), "abc", '\0', 0, 0, 0));
        Assert.True(Utf8DeterministicVerifierGuards.Create(raw, EmptyPlan()).Matches("ABC"u8, 0));
    }

    [Fact]
    public void GuardFactsCoverIndependentScalarVmMatchesInBothDirections()
    {
        var patterns = new[] { "((cat.)|dog)z", "(?:a*|b)c", "(ab.)cd", "(ab😀)cd", "(ab(?i:k))cd", "(?i:abc)de",
            "a[é-ê]{2}bc", "[ab](cat|dog)", "a(?=b)bc", "(cat|dog)?z", "(?:|a)b", "(?:a|)b",
            "(?i:cat|dog)", "(?i:k)ab", "[\\t-\\r ]*abc[0-9]", "\\s*abc[0-9]", "(?<x>ab)\\k<x>c",
            "a(?>b.)cd", "(a|b){2}cd", "a[😀-🙏]bc", "(?:ab|a)b", "\\A(ab.)cd\\z" };
        var inputs = new[] { "cat😀z", "dogz", "catxz", "c", "aaac", "bc", "ab😀cd", "abxcd", "abKcd", "ABCde", "aéêbc",
            "acat", "bdog", "abc", "z", "b", "ab", "CAT", "DOG", "Kab", "Kab", "  abc7", "\u00a0abc7", "ababc", "aabc",
            "abcd", "a😀bc", "a🙏bc", "abab", "xcat😀zy", "" }.Select(Encoding.UTF8.GetBytes).ToArray();
        var observed = 0;
        foreach (var pattern in patterns)
        foreach (var direction in new[] { RegexOptions.None, RegexOptions.RightToLeft })
        {
            var options = RegexOptions.CultureInvariant | direction;
            var front = Utf8FrontEnd.Compile(pattern, options);
            var tree = front.ExecutionTree;
            Assert.NotNull(tree);
            var guards = Utf8DeterministicVerifierGuards.Create(tree, front.SearchPlan);
            var owned = Utf8OwnedTreeParser.TryParse(pattern, options);
            Assert.NotNull(owned);
            var program = Utf8OwnedTreeSliceLowerer.TryLower(owned, options);
            Assert.NotNull(program);
            var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
            foreach (var input in inputs)
            for (var boundary = 0; boundary <= input.Length; boundary++)
            {
                if (boundary < input.Length && (input[boundary] & 0xc0) == 0x80) continue;
                var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(100_000, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
                var match = Utf8SliceRunner.TryMatchAt(program, input, boundary, starts, ends, true, ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach);
                if (!match.IsMatch) continue;
                observed++;
                var first = Math.Min(boundary, match.EndIndex); var length = Math.Abs(boundary - match.EndIndex);
                Assert.True(guards.Matches(input, first), $"guard: {pattern} / {direction} / {Encoding.UTF8.GetString(input)}");
            }
        }
        Assert.True(observed > 75);
        foreach (var row in new (string Pattern, string Subject)[]
        {
            ("((cat.)|dog)z", "cat😀z"), ("(?:a*|b)c", "c"), ("(ab(?i:k))cd", "abKcd"),
            ("(?i:abc)de", "ABCde"), ("a[é-ê]{2}bc", "aéêbc"), ("\\s*abc[0-9]", "\u00a0abc7"),
        })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(row.Pattern, RegexOptions.CultureInvariant | mode);
            var input = Encoding.UTF8.GetBytes(row.Subject);
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
            Assert.Equal((0, input.Length), (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal((0, input.Length), (detailed.IndexInBytes, detailed.LengthInBytes));
        }
    }

    [Fact]
    public void EveryExecutionFactWorkCutoffRestoresParentLoansWithoutRefundingWork()
    {
        foreach (var pattern in new[] { "((cat.)|dog)z", "(?:a*|b)c", "(?i:abc)de", "(ab😀)cd", "(?:|a)b" })
        {
            var tree = Tree(pattern); var plan = EmptyPlan();
            foreach (var route in new[] { 2 })
            {
                ulong work;
                using (var measured = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
                { Run(); work = measured.Budget.WorkSteps; }
                Assert.InRange(work, 1ul, 10_000ul);
                for (ulong cutoff = 0; cutoff <= work; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 1, ulong.MaxValue);
                    var parent = owner.Budget.AllocateArray<byte>(1, false); var checkpoint = owner.Budget.ReservedBytes;
                    Exception? failure;
                    using (var discarded = Utf8ConstructionScope.Enter()) failure = Record.Exception(Run);
                    if (cutoff == work) Assert.Null(failure);
                    else Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(48ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
                    Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Single(parent);
                    Assert.InRange(owner.Budget.WorkSteps, 1ul, cutoff + 1);
                }
                void Run()
                {
                    _ = Utf8DeterministicVerifierGuards.Create(tree, plan);
                }
            }
        }
    }

    [Fact]
    public void LexicalFlagTransferPreservesEveryLoweredNode()
    {
        foreach (var pattern in new[] { "(?i:k)", "(?i:abc)", "a(?i:b)c", "(?i:é)" })
        {
            var semantic = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant).SemanticRegex;
            var execution = Utf8ExecutionTreeLowerer.Lower(semantic);
            Assert.NotNull(execution); Assert.NotNull(semantic.RuntimeTree);
            var pairs = new Stack<(Utf8TreeNode Owned, Utf8ExecutionNode Lowered)>();
            pairs.Push((semantic.RuntimeTree.Root, execution.Root));
            while (pairs.TryPop(out var pair))
            {
                Assert.Equal(pair.Owned.MatchCaseInsensitive, (pair.Lowered.Options & RegexOptions.IgnoreCase) != 0);
                Assert.Equal(pair.Owned.ChildCount, pair.Lowered.Children.Count);
                for (var index = 0; index < pair.Owned.ChildCount; index++) pairs.Push((pair.Owned.Child(index), pair.Lowered.Children[index]));
            }
        }
    }
}
