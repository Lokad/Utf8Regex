using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralEdgeQualificationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8FallbackDirectFamilyPlan Plan(string pattern, RegexOptions options) => Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, options));
    private static Utf8OwnedRegexTree Tree(string pattern, RegexOptions options)
    { var tree = Utf8FrontEnd.Analyze(pattern, options).SemanticRegex.RuntimeTree; Assert.NotNull(tree); return tree; }
    private static MethodInfo Classifier()
    {
        var type = typeof(Utf8FrontEnd).Assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackLiteralClassifier"); Assert.NotNull(type);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        var method = type.GetMethod("Classify", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public); Assert.NotNull(method); return method;
    }
    private static Utf8FallbackDirectFamilyPlan Classify(MethodInfo method, Utf8OwnedRegexTree tree)
    {
        try { return Assert.IsType<Utf8FallbackDirectFamilyPlan>(method.Invoke(null, [tree])); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static void Range(string pattern, string text, RegexOptions options, int index, int length)
    {
        var regex = new Utf8Regex(pattern, options); var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input)); var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
        Assert.Equal((index, length), (match.IndexInBytes, match.LengthInBytes)); Assert.Equal((index, length), (detailed.IndexInBytes, detailed.LengthInBytes));
    }

    [Fact]
    public void EscapedAndCapturedPrefixFragmentsBorrowTheirParsedLiteralLanguage()
    {
        const string pattern = @"^(?<x>\x61)(?:\u0062)";
        var plan = Plan(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, plan.Kind); Assert.Equal("ab"u8.ToArray(), plan.LiteralUtf8);
        foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            Range(pattern, "ab😀", RegexOptions.CultureInvariant | compiled, 0, 2);
            var match = new Utf8Regex(pattern, RegexOptions.CultureInvariant | compiled).MatchDetailed("ab😀"u8);
            var group = match.GetGroup("x"); Assert.Equal((0, 1), (group.IndexInBytes, group.LengthInBytes));
            Range("^(ab)", "ab😀", RegexOptions.CultureInvariant | compiled, 0, 2);
            var aliasGroup = new Utf8Regex("^(ab)", RegexOptions.CultureInvariant | compiled).MatchDetailed("ab😀"u8).GetGroup(1);
            Assert.Equal((0, 2), (aliasGroup.IndexInBytes, aliasGroup.LengthInBytes));
        }
    }

    [Fact]
    public void ParsedSuffixAnchorsKeepTheirLexicalMultilineAndNewlineRules()
    {
        foreach (var pattern in new[] { @"(?:\x61)(?<x>b)\Z", @"(?-m:ab$)" })
        {
            Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix, Plan(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant).Kind);
            Range(pattern, "😀ab\n", RegexOptions.Multiline | RegexOptions.CultureInvariant, 4, 2);
        }
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffixStrictEnd, Plan(@"(?:a)(?:\x62)\z", RegexOptions.CultureInvariant).Kind);
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix, Plan(@"(?m:ab$)", RegexOptions.CultureInvariant).Kind);
        Range(@"(?m:ab$)", "😀ab\ncd", RegexOptions.CultureInvariant, 4, 2);
    }

    [Fact]
    public void UniformLexicalFoldingQualifiesButMixedSensitiveLettersKeepScalarExecution()
    {
        const string prefix = @"\A(?i:a)(?i:b)"; const string suffix = @"(?i:a)(?i:b)\z";
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix, Plan(prefix, RegexOptions.CultureInvariant).Kind);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd, Plan(suffix, RegexOptions.CultureInvariant).Kind);
        Range(prefix, "AB😀", RegexOptions.CultureInvariant, 0, 2); Range(suffix, "😀AB", RegexOptions.CultureInvariant, 4, 2);
        foreach (var pattern in new[] { @"\Aa(?i:b)", @"\A(?i:a)b" })
        {
            Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix, Plan(pattern, RegexOptions.CultureInvariant).Kind);
            Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, Plan(pattern, RegexOptions.CultureInvariant).Kind);
        }
        Assert.False(new Utf8Regex(@"\Aa(?i:b)").IsMatch("AB"u8)); Assert.False(new Utf8Regex(@"\A(?i:a)b").IsMatch("AB"u8));
    }

    [Fact]
    public void WiderCaseFibersDeclineTheByteShortcutAndMatchCompleteScalars()
    {
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix, Plan("^k", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Kind);
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix, Plan("k$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Kind);
        foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
        { Range("^k", "K😀", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | compiled, 0, 3); Range("k$", "😀K", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | compiled, 4, 3); }
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix, Plan("^I", RegexOptions.IgnoreCase).Kind);
    }

    [Fact]
    public void LeadingDotLiteralRangesStopAtNewlinesAndChooseTheFirstSuccessfulLine()
    {
        Assert.True(Utf8AsciiLeadingAnyRunTrailingLiteralExecutor.TryFindMatch("x\nab"u8, "ab"u8, out var index, out var length)); Assert.Equal((2, 2), (index, length));
        Assert.True(Utf8AsciiLeadingAnyRunTrailingLiteralExecutor.TryFindMatch("xxab\nzzab"u8, "ab"u8, out index, out length)); Assert.Equal((0, 4), (index, length));
        foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
        { Range(".*ab", "x\nab", RegexOptions.CultureInvariant | compiled, 2, 2); Range(".*ab", "xxab\nzzab", RegexOptions.CultureInvariant | compiled, 0, 4); Range(".*ab", "😀x\nab", RegexOptions.CultureInvariant | compiled, 6, 2); }
    }

    [Fact]
    public void LeadingDotLiteralMayConsumeNewlinesInsideTheLiteralButNotInsideDot()
    {
        const string pattern = @".*\x0AZ";
        Assert.Equal(Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral, Plan(pattern, RegexOptions.CultureInvariant).Kind);
        Range(pattern, "x\nZ\nZ", RegexOptions.CultureInvariant, 0, 3);
        foreach (var literal in new[] { "a", "aZ", "\nZ", "Z\n", "\r\n" })
        {
            var oracle = new Regex(".*" + Regex.Escape(literal)); var bytes = Encoding.UTF8.GetBytes(literal);
            foreach (var text in Subjects(5, ""))
            {
                var input = Encoding.UTF8.GetBytes(text); var expected = oracle.Match(text);
                Assert.Equal(expected.Success, Utf8AsciiLeadingAnyRunTrailingLiteralExecutor.TryFindMatch(input, bytes, out var index, out var length));
                Assert.Equal(expected.Success, Utf8AsciiLeadingAnyRunTrailingLiteralExecutor.IsMatch(input, bytes));
                if (expected.Success) Assert.Equal((expected.Index, expected.Length), (index, length));
            }
        }
    }
    private static IEnumerable<string> Subjects(int remaining, string prefix)
    { yield return prefix; if (remaining == 0) yield break; foreach (var value in "aZ\n\r") foreach (var text in Subjects(remaining - 1, prefix + value)) yield return text; }

    [Fact]
    public void CompleteScalarDotProofDeclinesSinglelineLazyAtomicAndDifferentRepeatBounds()
    {
        Assert.Equal(Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral, Plan(@"[^\n]*\x61b", RegexOptions.CultureInvariant).Kind);
        foreach (var pattern in new[] { @"(?s:.*)ab", @".*?ab", @".+ab", @"(?>.*)ab", @"[a\n]*ab" })
            Assert.NotEqual(Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral, Plan(pattern, RegexOptions.CultureInvariant).Kind);
        Range(@"(?s:.*)ab", "x\nab", RegexOptions.CultureInvariant, 0, 4);
        Range(@".*?ab", "x\nab", RegexOptions.CultureInvariant, 2, 2);
    }

    [Fact]
    public void ScalarByteRangesRemainPrimaryAcrossDirectionAndNulAliases()
    {
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, Plan(@"\A\0a", RegexOptions.CultureInvariant).Kind);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled, RegexOptions.CultureInvariant | RegexOptions.RightToLeft, RegexOptions.CultureInvariant | RegexOptions.RightToLeft | RegexOptions.Compiled })
        { Range(@"\A\0a", "\0a😀", options, 0, 2); Range(@"a\0\z", "😀a\0", options, 4, 2); }
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, Plan(@"\A😀a", RegexOptions.CultureInvariant).Kind);
        Range(@"\A😀a", "😀a", RegexOptions.CultureInvariant, 0, 5);
    }

    [Fact]
    public void EveryLiteralProofCutoffRetainsOnlyItsFinalPayloadAndRestoresParentLoans()
    {
        var method = Classifier();
        foreach (var pattern in new[] { @"\A(?<x>a)(?:b)", @"(?i:a)(?i:b)\z", @"[^\n]*\x61b" })
        {
            var tree = Tree(pattern, RegexOptions.CultureInvariant); ulong peak, work;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            { Assert.True(Classify(method, tree).HasValue); Assert.Equal(48ul, funded.Budget.ReservedBytes); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
            Assert.InRange(peak, 1ul, 20000ul); Assert.InRange(work, 1ul, 10000ul);
            foreach (var bytes in new[] { true, false })
            {
                var successes = 0; var limit = bytes ? peak : work;
                for (var cutoff = 0ul; cutoff <= limit; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
                    var failure = Record.Exception(() => Classify(method, tree));
                    if (failure is null) { successes++; Assert.Equal(128ul + 48ul, owner.Budget.ReservedBytes); }
                    else
                    { Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind); Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); }
                    Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                    Assert.True(bytes ? owner.Budget.PeakBytes <= 128 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
                }
                if (bytes) Assert.InRange(successes, 1, (int)limit + 1); else Assert.Equal(1, successes);
            }
        }
    }

    [Fact]
    public void ActualLiteralWrappersAreAdmittedAndTheSameOwnerRecoversWithoutDeadScanners()
    {
        var tree = Tree(@"\Aab", RegexOptions.CultureInvariant); var root = tree.Root;
        for (var depth = 0; depth < 65; depth++) root = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, RegexOptions.CultureInvariant).WithChild(root);
        var deep = Utf8OwnedRegexTree.Create(root, tree.CaptureCount, tree.CaptureNames, tree.CaptureNameToNumberMapping, tree.CaptureNumberSparseMapping,
            tree.Options, tree.Culture, tree.CaptureCatalog, tree.UsesLiteralClassBracketSyntax, tree.UsesIgnoreCase);
        var method = Classifier(); using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.Throws<Utf8ResourceLimitExceededException>(() => Classify(method, deep)).ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
        var spent = owner.Budget.WorkSteps; Assert.True(Classify(method, tree).HasValue); Assert.Equal(128ul + 48ul, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > spent); Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        foreach (var name in new[] { "ClassifyPattern", "TryParseAnchoredAsciiLiteralPrefix", "TryParseAnchoredAsciiLiteralSuffix", "TryParseLeadingAnyRunTrailingAsciiLiteral", "TryDecodeStrictAsciiRegexLiteral", "IsEscapedAsciiRegexLiteral", "IsRegexMetaChar" })
            Assert.DoesNotContain(typeof(Utf8FallbackRegexFamilyAnalyzer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static), m => m.Name == name);
    }
}
