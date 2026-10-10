using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FinalFamilyQualificationTests
{
    private const string Email = @"^[a-zA-Z0-9_\-\.]+@(\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.|([a-zA-Z0-9\-]+\.)+)([a-zA-Z]{2,12}|[0-9]{1,3})\]?$";
    private const string Balanced = @"BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\s\S]))*END(?(open)(?!))";
    private const string Digits = @"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\d+)/?\?t=(?<tab>[^ ?]+)(?<rest>.*)$";
    private const string Hex = @"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \n]+)?(?<rest>.*)$";
    private const string HexRoute = "/gateway/BigFiles/Browse/Download?hash=";
    private const RegexOptions Fold = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8OwnedRegexTree Tree(string pattern, RegexOptions options)
    { var tree = Utf8FrontEnd.Analyze(pattern, options).SemanticRegex.RuntimeTree; Assert.NotNull(tree); return tree; }
    private static string Describe(Utf8TreeNode node) => $"{node.Kind}({node.M},{node.N},{(int)node.Ch},{node.Str},[{string.Join(',', node.ScalarClass?.Data ?? [])}])" +
        string.Concat(Enumerable.Range(0, node.ChildCount).Select(index => "{" + Describe(node.Child(index)) + "}"));
    private static MethodInfo Classifier(string name, Utf8OwnedRegexTree tree)
    {
        var type = typeof(Utf8FrontEnd).Assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd." + name);
        Assert.True(type is not null, Describe(tree.Root)); Assert.NotNull(type);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        var method = type.GetMethod("Classify", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public); Assert.NotNull(method); return method;
    }
    private static Utf8FallbackDirectFamilyPlan Classify(MethodInfo method, Utf8OwnedRegexTree tree)
    {
        try { return Assert.IsType<Utf8FallbackDirectFamilyPlan>(method.Invoke(null, [tree])); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static Utf8FallbackDirectFamilyPlan Plan(string pattern, RegexOptions options, Utf8FallbackDirectFamilyKind kind)
    {
        var tree = Tree(pattern, options); var method = Classifier(kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole or Utf8FallbackDirectFamilyKind.Utf8BalancedBeginEndCount
            ? "Utf8FallbackFinalProfileClassifier" : "Utf8FallbackQueryClassifier", tree);
        var plan = Classify(method, tree); Assert.True(plan.Kind == kind, Describe(tree.Root)); return plan;
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
    private static bool NativeQuery(ReadOnlySpan<byte> input, Utf8FallbackDirectFamilyPlan plan, out int length) =>
        plan.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole
            ? Utf8AsciiAnchoredUrlExecutor.TryMatchDigitsQueryWhole(input, plan, out length)
            : Utf8AsciiAnchoredUrlExecutor.TryMatchHexQueryWhole(input, plan, out length);

    [Fact]
    public void EmailProofBorrowsAliasesCapturesAndAsciiIntersections()
    {
        var alias = Email.Replace("^", @"\A").Replace("@", @"(?<at>\x40)").Replace("[0-9]", @"\p{Nd}").Replace("$", @"\Z");
        Plan(alias, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole);
        PublicRange(alias, RegexOptions.CultureInvariant, "a@[١.٢.٣.٤]", 0, 15, true);
        var group = new Utf8Regex(alias).MatchDetailed("a@a.ab"u8).GetGroup("at");
        Assert.Equal((1, 1), (group.IndexInBytes, group.LengthInBytes));
    }

    [Fact]
    public void EmailWholeEndpointsAgreeWithIndependentAsciiGrammar()
    {
        Plan(Email, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole);
        var oracle = new Regex(Email, RegexOptions.CultureInvariant);
        foreach (var local in new[] { "a", "1", "-", ".", "a_b", "", "é" })
        foreach (var domain in new[] { "a.ab", "a.1", "a.a", "a.abcdefghijkl", "a.abcdefghijklm", "a.ab]", "[1.2.3.4]", "[1.2.3.ab", "[1.2.3.4", "[1.2.3.4444]" })
        foreach (var suffix in new[] { "", "\n", "\n\n", "\r", " " })
        {
            var text = local + "@" + domain + suffix; var expected = oracle.Match(text);
            if (local == "é") continue; // This comparator lane deliberately overlaps ASCII.
            var actual = Utf8AsciiEmailWholeExecutor.TryMatchWhole(Encoding.ASCII.GetBytes(text), out var length);
            Assert.Equal(expected.Success, actual); if (actual) Assert.Equal(expected.Length, length);
        }
    }

    [Fact]
    public void UngroupedEmailTailAlternativeKeepsItsOwnedUnanchoredGrammar()
    {
        var pattern = Email.Replace("([a-zA-Z]{2,12}|[0-9]{1,3})", "[a-zA-Z]{2,12}|[0-9]{1,3}");
        Assert.NotEqual(Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole, Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, RegexOptions.None)).Kind);
        PublicRange(pattern, RegexOptions.None, "1]", 0, 2, true);
        var tree = Tree(pattern, RegexOptions.None);
        Assert.False(Classify(Classifier("Utf8FallbackFinalProfileClassifier", tree), tree).HasValue);
    }

    [Fact]
    public void BalancedProofUsesCaptureIdentityAndTransparentLiteralAliases()
    {
        var alias = Balanced.Replace("open", "stack").Replace("BEGIN", @"(?:\x42)EGIN").Replace("END", @"E(?:\x4e)D");
        Plan(alias, RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.Utf8BalancedBeginEndCount);
        PublicRange(alias, RegexOptions.CultureInvariant, "BEGINxBEGIN😀END tail", 6, 12, false);
        var match = new Utf8Regex(alias).MatchDetailed("BEGIN😀END"u8);
        Assert.False(match.GetGroup("stack").Success);
    }

    [Fact]
    public void BalancedGrammarRejectsChangedCaptureEffectsAndIncompleteGuards()
    {
        var tree = Tree(Balanced, RegexOptions.None); var method = Classifier("Utf8FallbackFinalProfileClassifier", tree);
        foreach (var pattern in new[] { Balanced.Replace("(?<-open>END)", "(?<open-open>END)"),
            Balanced.Replace("(?!BEGIN|END)", "(?!BEGIN)"), Balanced.Replace("[\\s\\S]", "."),
            Balanced.Replace("*END", "*?END"), Balanced.Replace("(?(open)(?!))", ""), Balanced.Replace("END(?(open)", "EN(?(open)") })
            Assert.False(Classify(method, Tree(pattern, RegexOptions.None)).HasValue);
        Assert.False(Classify(method, Tree(Balanced, RegexOptions.IgnoreCase)).HasValue);
        Assert.False(Classify(method, Tree(Balanced, RegexOptions.RightToLeft)).HasValue);
        var outsidePush = @"(?<open>x?)" + Balanced;
        Assert.False(Classify(method, Tree(outsidePush, RegexOptions.None)).HasValue);
        Assert.Equal(0, new Utf8Regex(outsidePush).Count("BEGINEND"u8));
    }

    [Fact]
    public void BalancedCountsCompleteScalarsAndRetainsTheExistingDepthEnvelope()
    {
        Plan(Balanced, RegexOptions.None, Utf8FallbackDirectFamilyKind.Utf8BalancedBeginEndCount);
        foreach (var item in new[] { ("BEGIN😀\0\nEND", 1), ("BEGINBEGIN😀END", 1), ("BEGINBEGINENDEND", 1), ("BEGINEND😀BEGINEND", 2), ("BEGIN😀", 0) })
        {
            var input = Encoding.UTF8.GetBytes(item.Item1);
            Assert.True(Utf8BalancedBeginEndExecutor.TryCount(input, out var count)); Assert.Equal(item.Item2, count);
            Assert.Equal(item.Item2, new Utf8Regex(Balanced, RegexOptions.Compiled).Count(input));
        }
        var deep = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("BEGIN", 129)) + "😀" + string.Concat(Enumerable.Repeat("END", 129)));
        Assert.False(Utf8BalancedBeginEndExecutor.TryCount(deep, out _));
        Assert.Equal(1, new Utf8Regex(Balanced).Count(deep));
    }

    [Fact]
    public void QueryMarkersRequireEqualsAndOptionalTrigramCanBacktrack()
    {
        var digits = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(Digits, Fold));
        var hex = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(Hex, Fold));
        Assert.False(NativeQuery("~/d/1?tX"u8, digits, out _));
        Assert.True(NativeQuery(Encoding.ASCII.GetBytes("~" + HexRoute + "&name=x"), hex, out _));
        Assert.True(NativeQuery(Encoding.ASCII.GetBytes("~" + HexRoute + "?path=x\r&name=y"), hex, out _));
        Plan(Digits, Fold, Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole);
        Plan(Hex, Fold, Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole);
    }

    [Fact]
    public void QueryValueClassesAndFinalLfOwnTheirExactByteExtent()
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(Digits, Fold)); var oracle = new Regex(Digits, Fold);
        foreach (var text in new[] { "x\r~/d/1?t=x", "~/d/1?t=x\n", "~/d/1?t=x \n", "~/d/1?t=x\nz", "~/d/1?t=\n", "~/d/1?t=?", "~/d/1?t=x?tail\n" })
        {
            var expected = oracle.Match(text); var actual = NativeQuery(Encoding.ASCII.GetBytes(text), plan, out var length);
            Assert.Equal(expected.Success, actual); if (actual) Assert.Equal(expected.Length, length);
        }
        Plan(Digits, Fold, Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole);
        PublicRange(Digits, Fold, "😀~/d/١?t=é\n", 0, 16, true);
        var invalid = new byte[] { (byte)'a', 0xff };
        foreach (var pattern in new[] { Email, Balanced, Digits, Hex })
        {
            var regex = new Utf8Regex(pattern, pattern == Digits || pattern == Hex ? Fold | RegexOptions.Compiled : RegexOptions.Compiled);
            Assert.Throws<ArgumentException>(() => regex.Count(invalid));
        }
    }

    [Fact]
    public void QueryProofBorrowsFragmentedScopedLiteralsAndCapturedCulture()
    {
        var alias = @"(?i:\A" + Digits[1..^1].Replace("https", @"(?<scheme>\x68)ttps") + @"\Z)";
        var plan = Plan(alias, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole);
        Assert.Equal("https://go.lokad.com"u8.ToArray(), plan.PrimaryPrefixUtf8); Assert.Equal("t"u8.ToArray(), plan.RequiredParameterUtf8);
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var tree = Tree(Digits, RegexOptions.IgnoreCase); var method = Classifier("Utf8FallbackQueryClassifier", tree);
            Assert.False(Classify(method, tree).HasValue);
            Plan(Digits, Fold, Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole);
        }
        finally { CultureInfo.CurrentCulture = original; }
        var before = new string('a', 110);
        var boundary = Digits.Replace("https://go", before);
        var bounded = Plan(boundary, Fold, Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole);
        Assert.Equal(128, bounded.SecondaryPrefixUtf8.Length);
        Assert.Equal((UInt128)1 << 124, bounded.Url.PrefixWildcards.Secondary);
        var hit = Encoding.ASCII.GetBytes(before + ".testing.lokadXcom/d/1?t=x");
        Assert.True(NativeQuery(hit, bounded, out var length)); Assert.Equal(hit.Length, length);
        var tooLong = Digits.Replace("https://go", before + "a");
        var tooLongTree = Tree(tooLong, Fold);
        Assert.False(Classify(Classifier("Utf8FallbackQueryClassifier", tooLongTree), tooLongTree).HasValue);
        Assert.Equal(1, new Utf8Regex(tooLong, Fold).Count(Encoding.ASCII.GetBytes(before + "a.testing.lokadXcom/d/1?t=x")));
        var sensitive = Tree(Digits.Replace("https", "(?-i:https)"), Fold);
        Assert.False(Classify(Classifier("Utf8FallbackQueryClassifier", sensitive), sensitive).HasValue);
        var nul = Plan(Digits.Replace("https://go", @"\x00"), Fold, Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole);
        Assert.True(NativeQuery("\0.lokadXcom/d/1?t=x"u8, nul, out var nulLength));
        Assert.Equal(19, nulLength);
    }

    [Fact]
    public void QueryFixedProfilesAgreeWithIndependentAsciiCountsAndRanges()
    {
        foreach (var pattern in new[] { Digits, Hex })
        {
            var plan = Plan(pattern, Fold, pattern == Digits ? Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole : Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole);
            var oracle = new Regex(pattern, Fold);
            foreach (var prefix in new[] { "~", "https://go.lokad.com", "https://go.lokadXcom", "HTTPS://GO.TESTING.LOKAD.COM", "miss", "x\r~", "x\n~" })
            foreach (var trigram in new[] { "", "/abc", "/d", "/123", "/?" })
            foreach (var tail in pattern == Digits ? new[] { "/d/1?t=x", "/d/12/?t=x", "/d/1&t=x", "/d/1?t=x&z", "/d/1?t=x\nz", "/d/1?t=x \n" } :
                new[] { HexRoute + "&name=x", HexRoute + "ab?path=x&name=y", HexRoute + "?path=x\n&name=y", HexRoute + "&name=x\nz", HexRoute + "&name=x?path=z", HexRoute + "&name=x \n" })
            {
                var text = prefix + trigram + tail; var expected = oracle.Match(text); var input = Encoding.ASCII.GetBytes(text);
                var actual = NativeQuery(input, plan, out var length); Assert.Equal(expected.Success, actual); if (actual) Assert.Equal(expected.Length, length);
                Assert.Equal(oracle.Count(text), new Utf8Regex(pattern, Fold | RegexOptions.Compiled).Count(input));
            }
        }
    }

    [Fact]
    public void EveryFinalProofCutoffFundsOnlySelectedArraysAndRestoresParentState()
    {
        foreach (var item in new[] { (Email, RegexOptions.None, "Utf8FallbackFinalProfileClassifier"), (Balanced, RegexOptions.None, "Utf8FallbackFinalProfileClassifier"),
            (Digits, Fold, "Utf8FallbackQueryClassifier"), (Hex, Fold, "Utf8FallbackQueryClassifier") })
        {
            var tree = Tree(item.Item1, item.Item2); var method = Classifier(item.Item3, tree); ulong peak, work, retained;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            {
                var plan = Classify(method, tree); Assert.True(plan.HasValue, Describe(tree.Root));
                retained = item.Item3 == "Utf8FallbackFinalProfileClassifier" ? 0 :
                    Utf8ConstructionBudget.ArrayBytes<byte>(plan.PrimaryPrefixUtf8.Length) + Utf8ConstructionBudget.ArrayBytes<byte>(plan.SecondaryPrefixUtf8.Length) +
                    Utf8ConstructionBudget.ArrayBytes<byte>(plan.RelativePrefixUtf8.Length) + Utf8ConstructionBudget.ArrayBytes<byte>(plan.RouteMarkerUtf8.Length) +
                    Utf8ConstructionBudget.ArrayBytes<byte>(plan.RequiredParameterUtf8.Length) + (plan.OptionalParameterUtf8.Length == 0 ? 0 : Utf8ConstructionBudget.ArrayBytes<byte>(plan.OptionalParameterUtf8.Length));
                Assert.Equal(retained, funded.Budget.ReservedBytes); Assert.Null(funded.Budget.ObjectCheckpoint); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps;
            }
            Assert.InRange(peak, 1ul, 20000ul); Assert.InRange(work, 1ul, 20000ul);
            foreach (var bytes in new[] { true, false })
            {
                var successes = 0; var limit = bytes ? peak : work;
                for (var cutoff = 0ul; cutoff <= limit; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, bytes ? 112 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
                    var failure = Record.Exception(() => Classify(method, tree));
                    if (failure is null) { successes++; Assert.Equal(112ul + retained, owner.Budget.ReservedBytes); }
                    else { Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind); Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); }
                    Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                    Assert.True(bytes ? owner.Budget.PeakBytes <= 112 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
                }
                if (bytes) Assert.InRange(successes, 1, (int)limit + 1); else Assert.Equal(1, successes);
            }
        }
    }

    [Fact]
    public void ActualFinalWrappersUseSharedDepthAndTextScannersStayAbsent()
    {
        var tree = Tree(Balanced, RegexOptions.None); var root = tree.Root;
        for (var depth = 0; depth < 65; depth++) root = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, RegexOptions.None).AddChild(root);
        var deep = Utf8OwnedRegexTree.Create(root, tree.CaptureCount, tree.CaptureNames, tree.CaptureNameToNumberMapping, tree.CaptureNumberSparseMapping,
            tree.Options, tree.Culture, tree.CaptureCatalog, tree.UsesLiteralClassBracketSyntax, tree.UsesIgnoreCase);
        var method = Classifier("Utf8FallbackFinalProfileClassifier", tree); using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 16);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.Throws<Utf8ResourceLimitExceededException>(() => Classify(method, deep)).ResourceKind);
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        var spent = owner.Budget.WorkSteps; Assert.True(Classify(method, tree).HasValue); Assert.Equal(112ul, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > spent); Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        foreach (var name in new[] { "ClassifyRemainingPattern", "IsAnchoredAsciiEmailWhole", "IsUtf8BalancedBeginEndCount", "TryParseAnchoredAsciiDigitsQueryWhole", "TryParseAnchoredAsciiHexQueryWhole",
            "TryReadEscapedAsciiChar", "TryReadEscapedAsciiLiteral", "TryReadAsciiPrefixAlternation", "TryConsumeNamedCapturedOrRaw", "TryConsumeNamedOptional", "TryConsume", "TryConsumeCapturedOrRaw", "RequireBytes" })
            Assert.DoesNotContain(typeof(Utf8FallbackRegexFamilyAnalyzer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static), m => m.Name == name);
    }
}
