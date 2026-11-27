using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralEdgeQualificationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8AnchoredLiteralPlan Plan(string pattern, RegexOptions options) => Utf8AnchoredLiteralClassifier.Classify(Utf8FrontEnd.Analyze(pattern, options).SemanticRegex.RuntimeTree);
    private static Utf8OwnedRegexTree Tree(string pattern, RegexOptions options)
    { var tree = Utf8FrontEnd.Analyze(pattern, options).SemanticRegex.RuntimeTree; Assert.NotNull(tree); return tree; }
    private static void Range(string pattern, string text, RegexOptions options, int index, int length)
    {
        var regex = new Utf8Regex(pattern, options: options); var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input)); var match = regex.Match(input); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal((index, length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))); Assert.Equal((index, length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
    }

    [Fact]
    public void EscapedAndCapturedPrefixFragmentsBorrowTheirParsedLiteralLanguage()
    {
        const string pattern = @"^(?<x>\x61)(?:\u0062)";
        var plan = Plan(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix, plan.Kind); Assert.Equal("ab"u8.ToArray(), plan.LiteralUtf8);
        foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            Range(pattern, "ab😀", RegexOptions.CultureInvariant | compiled, 0, 2);
            var match = Utf8MatchSequence.First(new Utf8Regex(pattern, options: RegexOptions.CultureInvariant | compiled).CreateMatchCursor("ab😀"u8.ToArray()));
            Assert.True(match.TryGetGroup(1, out var group)); Assert.Equal((0, 1), (group.StartOffsetInBytes, (group.EndOffsetInBytes - group.StartOffsetInBytes)));
            Range("^(ab)", "ab😀", RegexOptions.CultureInvariant | compiled, 0, 2);
            Assert.True(Utf8MatchSequence.First(new Utf8Regex("^(ab)", options: RegexOptions.CultureInvariant | compiled).CreateMatchCursor("ab😀"u8.ToArray())).TryGetGroup(1, out var aliasGroup));
            Assert.Equal((0, 2), (aliasGroup.StartOffsetInBytes, (aliasGroup.EndOffsetInBytes - aliasGroup.StartOffsetInBytes)));
        }
    }

    [Fact]
    public void ParsedSuffixAnchorsKeepTheirLexicalMultilineAndNewlineRules()
    {
        foreach (var pattern in new[] { @"(?:\x61)(?<x>b)\Z", @"(?-m:ab$)" })
        {
            Assert.Equal(Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffix, Plan(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant).Kind);
            Range(pattern, "😀ab\n", RegexOptions.Multiline | RegexOptions.CultureInvariant, 4, 2);
        }
        Assert.Equal(Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffixStrictEnd, Plan(@"(?:a)(?:\x62)\z", RegexOptions.CultureInvariant).Kind);
        Assert.NotEqual(Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffix, Plan(@"(?m:ab$)", RegexOptions.CultureInvariant).Kind);
        Range(@"(?m:ab$)", "😀ab\ncd", RegexOptions.CultureInvariant, 4, 2);
    }

    [Fact]
    public void UniformLexicalFoldingQualifiesButMixedSensitiveLettersKeepScalarExecution()
    {
        const string prefix = @"\A(?i:a)(?i:b)"; const string suffix = @"(?i:a)(?i:b)\z";
        Assert.Equal(Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix, Plan(prefix, RegexOptions.CultureInvariant).Kind);
        Assert.Equal(Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd, Plan(suffix, RegexOptions.CultureInvariant).Kind);
        Range(prefix, "AB😀", RegexOptions.CultureInvariant, 0, 2); Range(suffix, "😀AB", RegexOptions.CultureInvariant, 4, 2);
        foreach (var pattern in new[] { @"\Aa(?i:b)", @"\A(?i:a)b" })
        {
            Assert.NotEqual(Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix, Plan(pattern, RegexOptions.CultureInvariant).Kind);
            Assert.NotEqual(Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix, Plan(pattern, RegexOptions.CultureInvariant).Kind);
        }
        Assert.False(new Utf8Regex(@"\Aa(?i:b)").IsMatch("AB"u8)); Assert.False(new Utf8Regex(@"\A(?i:a)b").IsMatch("AB"u8));
    }

    [Theory]
    [InlineData(@"\Aab(?<=b)c", "abc😀", 0)]
    [InlineData(@"a(?<=a)bc\z", "😀abc", 4)]
    public void LookbehindPreventsAnAnchoredLiteralProof(string pattern, string text, int start)
    {
        const RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
        Assert.False(Plan(pattern, options).HasValue);
        Range(pattern, text, options, start, 3);
    }

    [Fact]
    public void WiderCaseFibersDeclineTheByteShortcutAndMatchCompleteScalars()
    {
        Assert.NotEqual(Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix, Plan("^k", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Kind);
        Assert.NotEqual(Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix, Plan("k$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Kind);
        foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
        { Range("^k", "K😀", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | compiled, 0, 3); Range("k$", "😀K", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | compiled, 4, 3); }
        Assert.NotEqual(Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix, Plan("^I", RegexOptions.IgnoreCase).Kind);
    }

    [Fact]
    public void LeadingDotLiteralRangesStopAtNewlinesAndChooseTheFirstSuccessfulLine()
    {
        foreach (var compiled in new[] { RegexOptions.None, RegexOptions.Compiled })
        { Range(".*ab", "x\nab", RegexOptions.CultureInvariant | compiled, 2, 2); Range(".*ab", "xxab\nzzab", RegexOptions.CultureInvariant | compiled, 0, 4); Range(".*ab", "😀x\nab", RegexOptions.CultureInvariant | compiled, 6, 2); }
    }    [Fact]
    public void LeadingDotLiteralMayConsumeNewlinesInsideTheLiteralButNotInsideDot()
    {
        Range(@".*\x0AZ", "x\nZ\nZ", RegexOptions.CultureInvariant, 0, 3);
        foreach (var literal in new[] { "a", "aZ", "\nZ", "Z\n", "\r\n" })
        {
            var pattern = ".*" + Regex.Escape(literal); var oracle = new Regex(pattern); var regex = new Utf8Regex(pattern);
            foreach (var text in Subjects(5, ""))
            {
                var input = Encoding.UTF8.GetBytes(text); var expected = oracle.Match(text); var actual = regex.Match(input);
                Assert.Equal(expected.Success, actual.Success); Assert.Equal(expected.Success, regex.IsMatch(input));
                if (actual.Success) Assert.Equal((expected.Index, expected.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes));
            }
        }
    }private static IEnumerable<string> Subjects(int remaining, string prefix)
    { yield return prefix; if (remaining == 0) yield break; foreach (var value in "aZ\n\r") foreach (var text in Subjects(remaining - 1, prefix + value)) yield return text; }

    [Theory]
    [InlineData(".*needle", "prefix\nneedle", "prefix\nneed", true)]
    [InlineData(".*é猫", "😀prefix\né猫", "😀prefix\né", true)]
    [InlineData(@"\A.*needle", "prefix needle", "prefix\nneedle", false)]
    [InlineData(".*needle(?=!)", "prefix needle!", "prefix needle", false)]
    public void RequiredLiteralWitnessRespectsTheFullPattern(string pattern, string hit, string miss, bool provesMatch)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = 1000 } });
        Assert.Equal(provesMatch, regex.Inspection.PreparedRegex.RequiredLiteralProvesMatch);
        using var source = new CancellationTokenSource();
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(hit), source.Token));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(miss), source.Token));
    }

    [Fact]
    public void CompleteScalarDotProofDeclinesSinglelineLazyAtomicAndDifferentRepeatBounds()
    {

        Range(@"(?s:.*)ab", "x\nab", RegexOptions.CultureInvariant, 0, 4);
        Range(@".*?ab", "x\nab", RegexOptions.CultureInvariant, 2, 2);
    }

    [Fact]
    public void ScalarByteRangesRemainPrimaryAcrossDirectionAndNulAliases()
    {
        Assert.Equal(Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix, Plan(@"\A\0a", RegexOptions.CultureInvariant).Kind);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled, RegexOptions.CultureInvariant | RegexOptions.RightToLeft, RegexOptions.CultureInvariant | RegexOptions.RightToLeft | RegexOptions.Compiled })
        { Range(@"\A\0a", "\0a😀", options, 0, 2); Range(@"a\0\z", "😀a\0", options, 4, 2); }
        Assert.NotEqual(Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix, Plan(@"\A😀a", RegexOptions.CultureInvariant).Kind);
        Range(@"\A😀a", "😀a", RegexOptions.CultureInvariant, 0, 5);
    }

    [Fact]
    public void EveryLiteralProofCutoffRetainsOnlyItsFinalPayloadAndRestoresParentLoans()
    {
        foreach (var pattern in new[] { @"\A(?<x>a)(?:b)", @"(?i:a)(?i:b)\z" })
        {
            var tree = Tree(pattern, RegexOptions.CultureInvariant); ulong peak, work;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            { Assert.True(Utf8AnchoredLiteralClassifier.Classify(tree).HasValue); Assert.Equal(48ul, funded.Budget.ReservedBytes); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
            Assert.InRange(peak, 1ul, 20000ul); Assert.InRange(work, 1ul, 10000ul);
            foreach (var bytes in new[] { true, false })
            {
                var successes = 0; var limit = bytes ? peak : work;
                for (var cutoff = 0ul; cutoff <= limit; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
                    var failure = Record.Exception(() => Utf8AnchoredLiteralClassifier.Classify(tree));
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
    public void LiteralWrapperAdmissionFailureLeavesTheOwnerUsable()
    {
        const RegexOptions options = RegexOptions.CultureInvariant;
        var tree = Tree(@"\Aab", options);
        var literal = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, options, "ab");
        for (var depth = 0; depth < 65; depth++)
            literal = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, options).WithChild(literal);
        var root = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, options);
        root.Children = new[] { Utf8TreeNode.Create(Utf8TreeNodeKind.Beginning, options), literal };
        var deep = Utf8OwnedRegexTree.Create(root, tree.CaptureCount, tree.CaptureNames, tree.CaptureNameToNumberMapping, tree.CaptureNumberSparseMapping,
            tree.Options, tree.Culture, tree.CaptureCatalog, tree.UsesLiteralClassBracketSyntax, tree.UsesIgnoreCase, tree.Features, tree.LiteralEligibility);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8AnchoredLiteralClassifier.Classify(deep)).ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
        var spent = owner.Budget.WorkSteps; Assert.True(Utf8AnchoredLiteralClassifier.Classify(tree).HasValue); Assert.Equal(128ul + 48ul, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > spent); Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
    }
}
