using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LinePrefixQualificationTests
{
    private const string Until = @"\G///[^\n]*\n";
    private const string Trimmed = @"^\s*(\[\|\s*)?///\s?(?<doc>.*)$";
    private const string Title = @"^\s*///(?<title>#.*)$";
    private const string Digits = @"^([0-9]+)(\-| |$)(.*)$";
    private const string Import = "^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$";
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8OwnedRegexTree Tree(string pattern, RegexOptions options)
    { var tree = Utf8FrontEnd.Analyze(pattern, options).SemanticRegex.RuntimeTree; Assert.NotNull(tree); return tree; }
    private static string Describe(Utf8TreeNode node) => $"{node.Kind}({node.M},{node.N},{(int)node.Ch},{node.Str},[{string.Join(',', node.ScalarClass?.Data ?? [])}])" +
        string.Concat(Enumerable.Range(0, node.ChildCount).Select(index => "{" + Describe(node.Child(index)) + "}"));
    private static Utf8FallbackDirectFamilyPlan Plan(string pattern, RegexOptions options, Utf8FallbackDirectFamilyKind kind)
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, options));
        Assert.True(plan.Kind == kind, Describe(Tree(pattern, options).Root)); return plan;
    }
    private static MethodInfo Classifier()
    {
        var type = typeof(Utf8FrontEnd).Assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackLineClassifier"); Assert.NotNull(type);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        var method = type.GetMethod("Classify", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public); Assert.NotNull(method); return method;
    }
    private static Utf8FallbackDirectFamilyPlan Classify(MethodInfo method, Utf8OwnedRegexTree tree)
    {
        try { return Assert.IsType<Utf8FallbackDirectFamilyPlan>(method.Invoke(null, [tree])); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static void PublicRange(string pattern, RegexOptions options, string text, int index, int length, bool rtl)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var lane in rtl ? new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled } : new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options | lane); var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            Assert.Equal((index, length), (match.IndexInBytes, match.LengthInBytes)); Assert.Equal((index, length), (detailed.IndexInBytes, detailed.LengthInBytes));
        }
    }
    private static IEnumerable<string> Subjects(string alphabet, int maximum)
    {
        yield return "";
        for (var length = 1; length <= maximum; length++)
        {
            var count = (int)Math.Pow(alphabet.Length, length);
            for (var value = 0; value < count; value++)
            {
                var code = value; var chars = new char[length];
                for (var index = 0; index < length; index++) { chars[index] = alphabet[code % alphabet.Length]; code /= alphabet.Length; }
                yield return new string(chars);
            }
        }
    }

    [Fact]
    public void UntilTerminatorProofBorrowsFragmentedLiteralsAndNulScalarBoundaries()
    {
        const string alias = @"\G(?<a>\x2f)(?:\x2f)/[^\n]*\x0a";
        var plan = Plan(alias, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte);
        Assert.Equal("///"u8.ToArray(), plan.LiteralUtf8); Assert.Equal((byte)'\n', plan.TerminatorByte);
        PublicRange(alias, RegexOptions.CultureInvariant, "///😀\nrest", 0, 8, false);
        const string nul = @"\Gx[^\x00]*\x00";
        Plan(nul, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte);
        PublicRange(nul, RegexOptions.CultureInvariant, "x😀\0", 0, 6, false);
    }

    [Fact]
    public void TrimmedOptionalWhitespaceCanConsumeLfIncludingTheFinalLf()
    {
        foreach (var text in new[] { "///\n", "///\nbody", " \n[| ///\nbody" })
        {
            Assert.True(Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredTrimmedOptionalLiteralPrefixTail(Encoding.ASCII.GetBytes(text), "///"u8, "[|"u8, out var length));
            Assert.Equal(text.Length, length); PublicRange(Trimmed, RegexOptions.CultureInvariant, text, 0, length, false);
        }
        PublicRange(Trimmed, RegexOptions.CultureInvariant, " /// 😀", 0, 11, false);
        var group = new Utf8Regex(Trimmed).MatchDetailed("///\n😀"u8).GetGroup("doc");
        Assert.Equal((4, 4), (group.IndexInBytes, group.LengthInBytes));
    }

    [Fact]
    public void TrimmedAliasesAndLexicalAnchorsPreserveNativeAsciiEndpoints()
    {
        const string alias = @"(?-m:\A\s*(?:\x5b\x7c\s*)?\x2f(?<x>/)/\s?(?<doc>[^\n]*)\Z)";
        var plan = Plan(alias, RegexOptions.Multiline | RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredTrimmedOptionalLiteralPrefixTail);
        var oracle = new Regex(alias, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        foreach (var tail in Subjects("a \n\0", 4))
        foreach (var prefix in new[] { "///", "[| ///", " \n///" })
        {
            var text = prefix + tail; var expected = oracle.Match(text);
            var found = Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(Encoding.ASCII.GetBytes(text), plan, out var length);
            Assert.Equal(expected.Success, found); if (found) Assert.Equal(expected.Length, length);
        }
    }

    [Fact]
    public void TitleCounterConsumesUnicodeIndentAndAdvancesPastCompletePrefixes()
    {
        var finder = new Utf8AsciiLiteralFinder("///#"u8);
        var input = " ///#😀\n\t///#x\n"u8;
        Assert.Equal(2, Utf8LinePrefixExecutor.CountMatchingLines(input, finder, true));
        const string alias = @"^\s*(?:\x2f/)(?<title>/\x23[^\n]*)$";
        Plan(alias, RegexOptions.Multiline | RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.LinePrefixCount);
        foreach (var options in new[] { RegexOptions.Multiline, RegexOptions.Multiline | RegexOptions.Compiled })
            Assert.Equal(2, new Utf8Regex(alias, options).Count(input));
        var capture = new Utf8Regex(alias, RegexOptions.Multiline).MatchDetailed(input).GetGroup("title");
        Assert.Equal((5, 6), (capture.IndexInBytes, capture.LengthInBytes));
        Assert.Equal(1, Utf8LinePrefixExecutor.CountMatchingLines("a\na\na\n"u8, new Utf8AsciiLiteralFinder("a\na"u8), false));
    }

    [Fact]
    public void QuotedLineCounterUsesExactPrefixAndCompleteUnicodeWhitespaceAndBody()
    {
        foreach (var item in new[] { ("import2 \"x\"", "import2", 1), ("import\n\"😀\"", "import", 1), ("import \"a\nb\"", "import", 1), ("import \"a\\b\"", "import", 0) })
        {
            Assert.True(Utf8QuotedLineSegmentExecutor.TryCountViaLineWalk(Encoding.UTF8.GetBytes(item.Item1), Encoding.ASCII.GetBytes(item.Item2), [], out var count));
            Assert.Equal(item.Item3, count);
        }
        const string alias = "^\\x69(?<i>m)port\\s+(?:shared\\s+)?\"(?<p>(\\.|[^\\\\\"]*))\"[^\\n]*$";
        Plan(alias, RegexOptions.Multiline | RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount);
        PublicRange(alias, RegexOptions.Multiline | RegexOptions.CultureInvariant, "import shared \"😀\nx\"tail", 0, 28, false);
    }

    [Fact]
    public void QuotedLineAndTitleNativeCountsAgreeWithIndependentOverlappingOracle()
    {
        Plan(Import, RegexOptions.Multiline, Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount);
        var imports = new Regex(Import, RegexOptions.Multiline); var titles = new Regex(Title, RegexOptions.Multiline);
        var finder = new Utf8AsciiLiteralFinder("///#"u8);
        foreach (var a in new[] { "", "import \"\"", "import shared \"x\"", "import\n\"a\nb\"", "import \".\"", "import \"a\\b\"", "import \"x\"" })
        foreach (var b in new[] { "", "\nimport \"z\"", " import \"x\"", "\n///#x", "\n ///#y" })
        {
            var text = a + b; var input = Encoding.UTF8.GetBytes(text);
            Assert.True(Utf8QuotedLineSegmentExecutor.TryCountViaLineWalk(input, "import"u8, "shared"u8, out var count)); Assert.Equal(imports.Count(text), count);
            Assert.Equal(titles.Count(text), Utf8LinePrefixExecutor.CountMatchingLines(input, finder, true));
        }
    }

    [Fact]
    public void LeadingDigitTailRequiresEndArmAndDisjointNonLfSeparators()
    {
        foreach (var pattern in new[] { @"^[0-9]+-.*$", @"^[0-9]+(1|$).*$", @"^[0-9]+(\n|$).*$" })
            Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail, Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant)).Kind);
        PublicRange(@"^[0-9]+(1|$).*$", RegexOptions.CultureInvariant, "111x", 0, 4, true);
        PublicRange(@"^[0-9]+(\n|$).*$", RegexOptions.CultureInvariant, "1\n", 0, 2, false);
        const string alias = @"\A\p{Nd}+(?:\x2d|\x20|\Z)[^\n]*\Z";
        var plan = Plan(alias, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail);
        var oracle = new Regex(alias, RegexOptions.CultureInvariant);
        foreach (var text in Subjects("1- x\n", 5))
        {
            var expected = oracle.Match(text); var actual = Utf8AsciiLeadingDigitsTailExecutor.TryMatchWhole(Encoding.ASCII.GetBytes(text), plan.LiteralUtf8, out var length);
            Assert.Equal(expected.Success, actual); if (actual) Assert.Equal(expected.Length, length);
        }
        PublicRange(alias, RegexOptions.CultureInvariant, "١-😀", 0, 7, true);
    }

    [Fact]
    public void UnsafeLineWhitespaceDotsOptionsAndOptionalOverlapsKeepScalarGrammar()
    {
        var method = Classifier();
        foreach (var item in new[] { (Title, RegexOptions.Multiline | RegexOptions.Singleline), (Title, RegexOptions.Multiline | RegexOptions.IgnoreCase),
            (Trimmed, RegexOptions.Multiline), (Trimmed.Replace("*", "*?"), RegexOptions.None), (Until.Replace("*", "+"), RegexOptions.None),
            (Trimmed.Replace(@"\[\|", "///"), RegexOptions.None), (Import.Replace("\\s+", "[ ]+"), RegexOptions.Multiline) })
            Assert.False(Classify(method, Tree(item.Item1, item.Item2)).HasValue);
        Assert.False(Classify(method, Tree(Title, RegexOptions.Multiline | RegexOptions.RightToLeft)).HasValue);
        Assert.Throws<ArgumentException>(() => new Utf8Regex(Trimmed).IsMatch(new byte[] { 47, 47, 47, 0xff }));
    }

    [Fact]
    public void EveryLineProofCutoffFundsOnlyFinalLiteralsAndRestoresParentState()
    {
        var method = Classifier();
        foreach (var item in new[] { (Until, RegexOptions.CultureInvariant, 48ul), (Trimmed, RegexOptions.CultureInvariant, 96ul), (Title, RegexOptions.Multiline, 48ul), (Import, RegexOptions.Multiline, 96ul), (Digits, RegexOptions.CultureInvariant, 48ul) })
        {
            var tree = Tree(item.Item1, item.Item2); ulong peak, work;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            { Assert.True(Classify(method, tree).HasValue, Describe(tree.Root)); Assert.Equal(item.Item3, funded.Budget.ReservedBytes); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
            Assert.InRange(peak, 1ul, 20000ul); Assert.InRange(work, 1ul, 20000ul);
            foreach (var bytes in new[] { true, false })
            {
                var successes = 0; var limit = bytes ? peak : work;
                for (var cutoff = 0ul; cutoff <= limit; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
                    var failure = Record.Exception(() => Classify(method, tree));
                    if (failure is null) { successes++; Assert.Equal(128ul + item.Item3, owner.Budget.ReservedBytes); }
                    else { Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind); Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); }
                    Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                    Assert.True(bytes ? owner.Budget.PeakBytes <= 128 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
                }
                if (bytes) Assert.InRange(successes, 1, (int)limit + 1); else Assert.Equal(1, successes);
            }
        }
    }

    [Fact]
    public void ActualLineWrappersUseSharedAdmittedFramesAndRetiredScannersStayAbsent()
    {
        var tree = Tree(Until, RegexOptions.CultureInvariant); var root = tree.Root;
        for (var depth = 0; depth < 65; depth++) root = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, RegexOptions.CultureInvariant).WithChild(root);
        var deep = Utf8OwnedRegexTree.Create(root, tree.CaptureCount, tree.CaptureNames, tree.CaptureNameToNumberMapping, tree.CaptureNumberSparseMapping,
            tree.Options, tree.Culture, tree.CaptureCatalog, tree.UsesLiteralClassBracketSyntax, tree.UsesIgnoreCase);
        var method = Classifier(); using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.Throws<Utf8ResourceLimitExceededException>(() => Classify(method, deep)).ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var spent = owner.Budget.WorkSteps; Assert.True(Classify(method, tree).HasValue); Assert.Equal(128ul + 48ul, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > spent); Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        foreach (var name in new[] { "TryParseAnchoredPrefixUntilBytePattern", "TryParseAnchoredAsciiLeadingDigitsTail", "TryParseAnchoredTrimmedOptionalLiteralPrefixTailPattern", "TryParseLinePrefixCountFamily", "TryParseAnchoredQuotedLineSegmentCountFamily", "TryParseAnchoredQuotedLineSegmentPattern", "TryParseOptionalLiteralWhitespaceGroup", "TryReadSingleAsciiLiteralAlternationOrDollar", "TryReadSingleAsciiLiteralBranch", "CompactRegexWhitespace", "TryDecodeAsciiLiteral" })
            Assert.DoesNotContain(typeof(Utf8FallbackRegexFamilyAnalyzer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static), m => m.Name == name);
    }
}
