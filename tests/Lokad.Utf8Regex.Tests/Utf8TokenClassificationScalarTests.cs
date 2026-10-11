using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TokenClassificationScalarTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8FallbackDirectFamilyPlan Plan(string pattern) => Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, RegexOptions.None));
    private static Utf8OwnedRegexTree Tree(string pattern)
    {
        var tree = Utf8FrontEnd.Analyze(pattern, RegexOptions.None).SemanticRegex.RuntimeTree;
        Assert.NotNull(tree); return tree;
    }
    private static MethodInfo Classifier()
    {
        var type = typeof(Utf8FrontEnd).Assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackTokenClassifier");
        Assert.NotNull(type);
        var method = type.GetMethod("Classify", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method); return method;
    }
    private static Utf8FallbackDirectFamilyPlan Classify(MethodInfo method, Utf8OwnedRegexTree tree)
    {
        try { return Assert.IsType<Utf8FallbackDirectFamilyPlan>(method.Invoke(null, [tree])); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static void ScalarRange(string pattern, string text, int index, int length)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options); var input = Encoding.UTF8.GetBytes(text);
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
            Assert.Equal((index, length), (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal((index, length), (detailed.IndexInBytes, detailed.LengthInBytes));
        }
    }

    [Fact]
    public void EscapedClassAtomsUseCanonicalScalarMembership()
    {
        const string pattern = @"[\n]+:[\d]+[a-z]+";
        var plan = Plan(pattern); var prepared = Utf8FallbackPreparedTokenSearchFactory.CreateLiteralStructuredTokenSearch(plan);
        Assert.True(prepared.HasValue); Assert.Equal(1, prepared.Count("\n:12a"u8));
        Assert.True(prepared.TryFindNext("\n:12a"u8, 0, out var index, out var length)); Assert.Equal((0, 5), (index, length));
        ScalarRange(pattern, "\n:12a", 0, 5);
        foreach (var tokenPattern in new[] { "[a]+:[b]+[c]+", "[a]+:[b]+\\.[c]+" })
        {
            var tokenPlan = Plan(tokenPattern);
            var delimited = Utf8FallbackPreparedTokenSearchFactory.CreateDelimitedTokenSearch(tokenPlan);
            var structured = Utf8FallbackPreparedTokenSearchFactory.CreateLiteralStructuredTokenSearch(tokenPlan);
            var input = tokenPattern.Contains("\\.", StringComparison.Ordinal) ? "a:b.c"u8 : "a:bc"u8;
            using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
            for (var iteration = 0; iteration < 100; iteration++)
                Assert.Equal(1, Utf8AsciiPreparedTokenFamilyExecutor.CountTokens(input, tokenPlan, delimited, structured));
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < 1000; iteration++)
            {
                if (Utf8AsciiPreparedTokenFamilyExecutor.CountTokens(input, tokenPlan, delimited, structured) != 1 ||
                    !Utf8AsciiPreparedTokenFamilyExecutor.TryFindToken(input, 0, tokenPlan, delimited, structured, out index, out length) ||
                    index != 0 || length != input.Length) throw new InvalidOperationException("Prepared operation changed its range.");
            }
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
            Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);

        }
    }

    [Fact]
    public void NulClassMembersRemainDistinctFromOptionalTailSeparation()
    {
        const string pattern = "[a]+:[b]+[c]+(?:\\?[\0x]*)?(?:#[y]*)?";
        var prepared = Utf8FallbackPreparedTokenSearchFactory.CreateLiteralStructuredTokenSearch(Plan(pattern));
        Assert.True(prepared.HasValue); Assert.True(prepared.TryFindNext("a:bc?\0x#y"u8, 0, out var index, out var length));
        Assert.Equal((0, 9), (index, length)); Assert.Equal(1, prepared.Count("a:bc?\0x#y"u8));
        ScalarRange(pattern, "a:bc?\0x#y", 0, 9);
    }

    [Fact]
    public void AsciiIntersectionsDeclineUnicodeSubjectsWhileQualifiedWordTokensRemainNative()
    {
        var plan = Plan("[a]+:[b]+[^x]+");
        Assert.False(Utf8AsciiDirectFamilyCountExecutor.TryCount("a:b😀"u8, false, plan, default, default, out _, out _));
        ScalarRange("[a]+:[b]+[^x]+", "a:b😀", 0, 7);
        const string email = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount, Plan(email).Kind);
        ScalarRange(email, "𐐀@𐐀.𐐀", 0, 14);
        const string plainWord = @"\w+@\w+\.\w+";
        var plainPlan = Plan(plainWord);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount, plainPlan.Kind);
        Assert.False(Utf8AsciiDirectFamilyCountExecutor.TryCount("𐐀@𐐀.𐐀"u8, false, plainPlan, default, default, out _, out _));
        ScalarRange(plainWord, "𐐀@𐐀.𐐀", 0, 14);
    }

    [Fact]
    public void UnsupportedSecondaryLiteralsKeepScalarGrammarWithoutSelectingAnIncompleteShortcut()
    {
        const string pattern = "[a]+::[b]+--[c]+";
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount, Plan(pattern).Kind);
        ScalarRange(pattern, "a::b--c", 0, 7);
    }

    [Fact]
    public void CanonicalClassifierTransfersOnlyFinalLiteralsAndRollsBackEveryInsufficientBudget()
    {
        var tree = Tree("[a]+:[b]+[c]+(?:\\?[x]*)?"); var method = Classifier(); ulong peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount, Classify(method, tree).Kind);
            Assert.Equal(96ul, funded.Budget.ReservedBytes);
            Assert.Equal(0ul, funded.Budget.Depth); Assert.Equal(0ul, funded.Budget.Reserved);
            peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps;
        }
        Assert.InRange(peak, 96ul, 20000ul); Assert.InRange(work, 1ul, 10000ul);
        foreach (var bytes in new[] { true, false })
        {
            var successes = 0; var limit = bytes ? peak : work;
            for (var cutoff = 0ul; cutoff <= limit; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
                var failure = Record.Exception(() => Classify(method, tree));
                if (failure is null) { successes++; Assert.Equal(128ul + 96ul, owner.Budget.ReservedBytes); }
                else
                {
                    Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                }
                Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                Assert.True(bytes ? owner.Budget.PeakBytes <= 128 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
            }
            // The shared buffer may choose smaller growth under a finite byte
            // limit. Every successful extent still retains exactly 96 bytes.
            if (bytes) Assert.InRange(successes, 1, (int)limit + 1);
            else Assert.Equal(1, successes);
        }
    }

    [Fact]
    public void ClassifierAdmitsActualOwnedWrapperDepthAndRecoversTheSameOwner()
    {
        var tree = Tree("[a]+:[b]+[c]+"); var root = tree.Root;
        for (var depth = 0; depth < 65; depth++) root = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, RegexOptions.None).WithChild(root);
        var deep = Utf8OwnedRegexTree.Create(root, tree.CaptureCount, tree.CaptureNames, tree.CaptureNameToNumberMapping,
            tree.CaptureNumberSparseMapping, tree.Options, tree.Culture, tree.CaptureCatalog, tree.UsesLiteralClassBracketSyntax, tree.UsesIgnoreCase);
        var method = Classifier();
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Classify(method, deep));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind); Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
        var spent = owner.Budget.WorkSteps;
        Assert.True(Classify(method, tree).HasValue); Assert.Equal(128ul + 48ul, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > spent); Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void DelimitedGreedyMiddleNeverAcceptsTailOnlyBytesBeforeItsSecondaryDelimiter()
    {
        var search = new PreparedAsciiDelimitedTokenSearch("::"u8, "."u8, "a"u8, "xy"u8, "12"u8);
        Assert.False(search.TryFindNext("a::x1.2"u8, 0, out _, out _)); Assert.Equal(0, search.Count("a::x1.2"u8));
        foreach (var middle in new[] { "x", "x.", "x.1" })
        foreach (var tail in new[] { "1", "1.", "x.1" })
        {
            var prepared = new PreparedAsciiDelimitedTokenSearch("::"u8, "."u8, "a"u8, Encoding.UTF8.GetBytes(middle), Encoding.UTF8.GetBytes(tail));
            var oracle = new Regex("[a]+::[" + middle + "]+\\.[" + tail + "]+");
            foreach (var text in Subjects(5, "a::"))
            {
                var input = Encoding.UTF8.GetBytes(text); var expected = oracle.Match(text);
                Assert.Equal(oracle.Count(text), prepared.Count(input)); Assert.Equal(expected.Success, prepared.TryFindNext(input, 0, out var index, out var length));
                if (expected.Success) Assert.Equal((expected.Index, expected.Length), (index, length));
            }
        }
        ScalarRange("[a]+::[xy]+\\.[12]+", "a::xy.12", 0, 8);
    }
    private static IEnumerable<string> Subjects(int remaining, string prefix)
    {
        yield return prefix; if (remaining == 0) yield break;
        foreach (var value in "x.1") foreach (var text in Subjects(remaining - 1, prefix + value)) yield return text;
    }

    [Fact]
    public void OverlappingGreedyHeadsDeclineEarliestLiteralShortcuts()
    {
        const string pattern = "[a:]+:[a]+[a]+";
        var plan = Plan(pattern);
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount, plan.Kind);
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount, plan.Kind);
        ScalarRange(pattern, "a:aa:aa", 0, 7);
    }
}
