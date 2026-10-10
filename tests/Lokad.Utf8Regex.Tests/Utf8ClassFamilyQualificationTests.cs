using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ClassFamilyQualificationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8FallbackDirectFamilyPlan Plan(string pattern, RegexOptions options) => Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, options));
    private static Utf8OwnedRegexTree Tree(string pattern)
    { var tree = Utf8FrontEnd.Analyze(pattern, RegexOptions.None).SemanticRegex.RuntimeTree; Assert.NotNull(tree); return tree; }
    private static MethodInfo Classifier()
    {
        var type = typeof(Utf8FrontEnd).Assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackClassClassifier"); Assert.NotNull(type);
        var method = type.GetMethod("Classify", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public); Assert.NotNull(method); return method;
    }
    private static Utf8FallbackDirectFamilyPlan Classify(MethodInfo method, Utf8OwnedRegexTree tree)
    {
        try { return Assert.IsType<Utf8FallbackDirectFamilyPlan>(method.Invoke(null, [tree])); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static void ScalarRange(string pattern, string text, RegexOptions options, int index, int length)
    {
        var regex = new Utf8Regex(pattern, options); var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input)); var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
        Assert.Equal((index, length), (match.IndexInBytes, match.LengthInBytes)); Assert.Equal((index, length), (detailed.IndexInBytes, detailed.LengthInBytes));
    }
    private static void UnknownValidationCount(string pattern, ReadOnlySpan<byte> input, int expected)
    {
        var plan = Plan(pattern, RegexOptions.CultureInvariant);
        Assert.True(Utf8FallbackDirectFamilyCountRouter.TryCount(input, default, plan, null, default, default, default, out var count));
        Assert.Equal(expected, count);
    }

    [Fact]
    public void AnchoredIdentifierEscapesUseCanonicalFoldedAsciiIntersections()
    {
        const string pattern = @"\G[\x61-\x7A][\x61-\x7A0-9_]*";
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant;
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix, Plan(pattern, options).Kind);
        foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            ScalarRange(pattern, "Ab_2Ω", options | compiled, 0, 4);
            ScalarRange(pattern, "K2", options | compiled, 0, 4);
            ScalarRange(@"\G(?i:[a-z])[a-z0-9_]*", "aB", RegexOptions.CultureInvariant | compiled, 0, 1);
        }
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix, Plan(@"\G(?i:[a-z])[a-z0-9_]*", RegexOptions.CultureInvariant).Kind);
    }

    [Fact]
    public void OperatorEscapesAndIdentifierAliasesKeepTheirQualifiedNativeFamilies()
    {
        const string operators = @"\G[\x3D+\-\^\x2A\/\.\x3C\x3E~!&|?]+";
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredOperatorRun, Plan(operators, RegexOptions.CultureInvariant).Kind);
        ScalarRange(operators, "=+-^*/.<>~!&|?", RegexOptions.CultureInvariant, 0, 14);
        const string identifier = @"[\x41-\x5A\x61-\x7A][A-Za-z0-9]*";
        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiIdentifierToken, Plan(identifier, RegexOptions.None).Kind);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
        { ScalarRange(identifier, "😀 A2 é", options, 5, 2); Assert.Equal(1, new Utf8Regex(identifier, options).Count("😀 A2 é"u8)); }
    }

    [Fact]
    public void NegatedRunsRetainEscapedScalarExclusionsAndAdmittedLiteralBytes()
    {
        const string pattern = @"(?<head>[^\n]+)\x3A(?<tail>[^\r]+)";
        var plan = Plan(pattern, RegexOptions.None);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns, plan.Kind);
        Assert.Equal((byte)10, plan.SeparatorByte); Assert.Equal((byte)13, plan.SecondSeparatorByte); Assert.Equal(":"u8.ToArray(), plan.LiteralUtf8);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled }) ScalarRange(pattern, "😀 a:b", options, 0, 8);
    }

    [Fact]
    public void NegatedRunFinderPreservesGreedyEndpointsAndTheCallerFloor()
    {
        Assert.True(Utf8AsciiLiteralBetweenNegatedRunsExecutor.TryFind("a:bya:c"u8, 0, (byte)':', (byte)'x', (byte)'y', out var index, out var length));
        Assert.Equal((0, 7), (index, length));
        Assert.True(Utf8AsciiLiteralBetweenNegatedRunsExecutor.TryFind("a:bya:c"u8, 2, (byte)':', (byte)'x', (byte)'y', out index, out length));
        Assert.Equal((2, 5), (index, length));
        foreach (var excludedHead in "xy:") foreach (var excludedTail in "xy:")
        {
            var oracle = new Regex("[^" + excludedHead + "]+:[^" + excludedTail + "]+");
            foreach (var text in Subjects(5, ""))
            {
                var input = Encoding.UTF8.GetBytes(text);
                for (var floor = 0; floor <= text.Length; floor++)
                {
                    var expected = oracle.Match(text, floor);
                    Assert.Equal(expected.Success, Utf8AsciiLiteralBetweenNegatedRunsExecutor.TryFind(input, floor, (byte)':', (byte)excludedHead, (byte)excludedTail, out index, out length));
                    if (expected.Success) Assert.Equal((expected.Index, expected.Length), (index, length));
                    Assert.True(Utf8AsciiLiteralBetweenNegatedRunsExecutor.TryIsMatchWithoutValidation(input, floor, (byte)':', (byte)excludedHead, (byte)excludedTail, out var isMatch));
                    Assert.Equal(expected.Success, isMatch);
                }
            }
        }
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled }) ScalarRange("[^x]+:[^y]+", "a:bya:c", options, 0, 7);
        foreach (var padding in new[] { 16, 32, 64 })
        {
            var text = new string('a', padding) + ":b"; var input = Encoding.UTF8.GetBytes(text);
            foreach (var floor in new[] { 0, padding - 1, padding, padding + 1, padding + 2 })
            {
                var expected = Regex.Match(text[floor..], "[^x]+:[^y]+");
                Assert.True(Utf8AsciiLiteralBetweenNegatedRunsExecutor.TryIsMatchWithoutValidation(input, floor, (byte)':', (byte)'x', (byte)'y', out var isMatch));
                Assert.Equal(expected.Success, isMatch);
                Assert.Equal(expected.Success, Utf8AsciiLiteralBetweenNegatedRunsExecutor.TryFind(input, floor, (byte)':', (byte)'x', (byte)'y', out index, out length));
                if (expected.Success) Assert.Equal((floor + expected.Index, expected.Length), (index, length));
            }
        }
    }
    private static IEnumerable<string> Subjects(int remaining, string prefix)
    { yield return prefix; if (remaining == 0) yield break; foreach (var value in "a:xy") foreach (var text in Subjects(remaining - 1, prefix + value)) yield return text; }

    [Fact]
    public void SingleUnicodeCategoriesBorrowCompleteScalarPredicatesInsteadOfPatternSpellings()
    {
        foreach (var pattern in new[] { @"[\p{Lu}]", @"(?:\p{Lu})", @"\p{Lu}{1}" })
        {
            var plan = Plan(pattern, RegexOptions.None); Assert.Equal(Utf8FallbackDirectFamilyKind.UnicodeCategoryCount, plan.Kind);
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
                Assert.Equal(2, new Utf8Regex(pattern, options).Count("𐐀A😀é"u8));
        }
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.UnicodeCategoryCount, Plan(@"(?i:\p{Lu})", RegexOptions.CultureInvariant).Kind);
        Assert.Equal(2, new Utf8Regex(@"(?i:\p{Lu})", RegexOptions.CultureInvariant).Count("aA"u8));
        UnknownValidationCount(@"[\p{Lu}]", "𐐀A😀é"u8, 2);
    }

    [Fact]
    public void LetterRepeatAliasesCountSupplementaryScalarsWithTheirParsedBounds()
    {
        foreach (var pattern in new[] { @"(?:\p{L}){2,3}", @"[\p{L}]{2,3}", @"(\p{L}){2,3}" })
        {
            var plan = Plan(pattern, RegexOptions.None); Assert.Equal(Utf8FallbackDirectFamilyKind.UnicodeLetterBoundedCount, plan.Kind);
            Assert.Equal((2, 3), (plan.MinCount, plan.MaxCount));
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
                Assert.Equal(2, new Utf8Regex(pattern, options).Count("𐐀AéB😀CΩ"u8));
        }
        Assert.Equal(Utf8FallbackDirectFamilyKind.UnicodeLetterCount, Plan(@"[\p{L}]", RegexOptions.None).Kind);
        Assert.Equal(3, new Utf8Regex(@"[\p{L}]").Count("𐐀Aé😀"u8));
        UnknownValidationCount(@"[\p{L}]", "𐐀Aé😀"u8, 3);
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.UnicodeLetterBoundedCount, Plan(@"(?:[\p{L}]{2}){2,3}", RegexOptions.None).Kind);
        Assert.Equal(0, new Utf8Regex(@"(?:[\p{L}]{2}){2,3}").Count("𐐀𐐀𐐀"u8));
    }

    [Fact]
    public void NegatedStarAliasesTreatNulAsACompleteScalarAndAdvanceEmptyMatchesByScalar()
    {
        foreach (var pattern in new[] { @"[^\x00]*", @"[^\0]{0,}", @"(?:[^\u0000])*" })
        {
            var plan = Plan(pattern, RegexOptions.None); Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiUntilByteStarCount, plan.Kind); Assert.Equal((byte)0, plan.TerminatorByte);
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
            { var regex = new Utf8Regex(pattern, options); Assert.Equal(4, regex.Count("😀\0A"u8)); Assert.Equal(2, regex.Count("😀"u8)); }
        }
    }

    [Fact]
    public void WordRepeatAliasesPreserveCompleteRunsAndOnlyUseAsciiCountingOnAsciiSubjects()
    {
        const string pattern = @"(?:\b)(?:[\w]){10,}(?:\b)";
        var plan = Plan(pattern, RegexOptions.None); Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiWordBoundedCount, plan.Kind); Assert.Equal(10, plan.MinCount);
        Assert.Equal(1, new Utf8Regex(pattern).Count("abcdefghijk"u8));
        Assert.Equal(1, new Utf8Regex(pattern).Count(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("𐐀", 10)))));
        Assert.False(Utf8AsciiDirectFamilyCountExecutor.TryCount("𐐀"u8, false, plan, default, default, out _, out _));
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AsciiWordBoundedCount, Plan(@"[\w-]{10,}", RegexOptions.None).Kind);
        const string nestedToken = @"[a]+:[b]+(?:[c]{2})+";
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount, Plan(nestedToken, RegexOptions.None).Kind);
        Assert.Equal(0, new Utf8Regex(nestedToken).Count("a:bc"u8));
        Assert.Equal(1, new Utf8Regex(nestedToken).Count("a:bcc"u8));
    }

    [Fact]
    public void EveryClassFamilyProofCutoffRetainsOnlyTheFinalPayloadOrRestoresParentLoans()
    {
        var method = Classifier();
        foreach (var pattern in new[] { @"[\p{Lu}]", @"[\p{L}]{2,3}", @"[^\0]*", @"[^\n]+\x3A[^\r]+", @"[A-Za-z][A-Za-z0-9]*" })
        {
            var tree = Tree(pattern); var finalBytes = pattern.Contains("3A", StringComparison.Ordinal) ? 48ul : 0ul; ulong peak, work;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            { Assert.True(Classify(method, tree).HasValue); Assert.Equal(finalBytes, funded.Budget.ReservedBytes); Assert.Null(funded.Budget.ObjectCheckpoint); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
            Assert.InRange(peak, 1ul, 20000ul); Assert.InRange(work, 1ul, 10000ul);
            foreach (var bytes in new[] { true, false })
            {
                var successes = 0; var limit = bytes ? peak : work;
                for (var cutoff = 0ul; cutoff <= limit; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, bytes ? 112 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
                    var failure = Record.Exception(() => Classify(method, tree));
                    if (failure is null) { successes++; Assert.Equal(112 + finalBytes, owner.Budget.ReservedBytes); }
                    else
                    { Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind); Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); }
                    Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                    Assert.True(bytes ? owner.Budget.PeakBytes <= 112 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
                }
                if (bytes) Assert.InRange(successes, 1, (int)limit + 1); else Assert.Equal(1, successes);
            }
        }
    }

    [Fact]
    public void SharedClassProofAdmitsActualWrappersAndRecoversTheSameConstructionOwner()
    {
        var tree = Tree(@"[\p{Lu}]"); var root = tree.Root;
        for (var depth = 0; depth < 65; depth++) root = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, RegexOptions.None).WithChild(root);
        var deep = Utf8OwnedRegexTree.Create(root, tree.CaptureCount, tree.CaptureNames, tree.CaptureNameToNumberMapping, tree.CaptureNumberSparseMapping,
            tree.Options, tree.Culture, tree.CaptureCatalog, tree.UsesLiteralClassBracketSyntax, tree.UsesIgnoreCase);
        var method = Classifier();
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.Throws<Utf8ResourceLimitExceededException>(() => Classify(method, deep)).ResourceKind);
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(0ul, owner.Budget.Depth);
        var spent = owner.Budget.WorkSteps; Assert.True(Classify(method, tree).HasValue); Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > spent); Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
    }
}
