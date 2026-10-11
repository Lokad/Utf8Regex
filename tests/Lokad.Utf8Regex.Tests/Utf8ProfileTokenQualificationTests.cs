using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProfileTokenQualificationTests
{
    private const string Uri = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
    private const string Octet = @"(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
    private const string Ip = "(?:" + Octet + @"\.){3}" + Octet;
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8OwnedRegexTree Tree(string pattern)
    { var tree = Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant).SemanticRegex.RuntimeTree; Assert.NotNull(tree); return tree; }
    private static string Describe(Utf8TreeNode node) => $"{node.Kind}({node.M},{node.N},{(int)node.Ch},{node.Str},[{string.Join(',', node.ScalarClass?.Data ?? [])}])" +
        string.Concat(Enumerable.Range(0, node.ChildCount).Select(index => "{" + Describe(node.Child(index)) + "}"));
    private static Utf8FallbackDirectFamilyPlan Plan(string pattern, Utf8FallbackDirectFamilyKind kind)
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant));
        Assert.True(plan.Kind == kind, Describe(Tree(pattern).Root)); return plan;
    }
    private static MethodInfo Classifier()
    {
        var type = typeof(Utf8FrontEnd).Assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackProfileClassifier"); Assert.NotNull(type);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        var method = type.GetMethod("Classify", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public); Assert.NotNull(method); return method;
    }
    private static Utf8FallbackDirectFamilyPlan Classify(MethodInfo method, Utf8OwnedRegexTree tree)
    {
        try { return Assert.IsType<Utf8FallbackDirectFamilyPlan>(method.Invoke(null, [tree])); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static void Range(string pattern, string text, int index, int length)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant); Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
            Assert.Equal((index, length), (match.IndexInBytes, match.LengthInBytes)); Assert.Equal((index, length), (detailed.IndexInBytes, detailed.LengthInBytes));
        }
    }
    private static void AsciiOracle(string pattern, IEnumerable<string> subjects, Utf8FallbackDirectFamilyPlan plan)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        foreach (var text in subjects)
        {
            var input = Encoding.ASCII.GetBytes(text);
            var expectedCount = oracle.Count(text);
            var actualCount = plan.Kind == Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount ? Utf8AsciiIpv4TokenExecutor.CountAsciiIpv4Tokens(input) :
                plan.Kind == Utf8FallbackDirectFamilyKind.Utf8UriToken ? Utf8UriTokenCountExecutor.CountWellFormed(input) : Utf8AsciiBoundedDateTokenExecutor.CountAsciiBoundedDateTokens(input, plan);
            Assert.Equal(expectedCount, actualCount);
            for (var floor = 0; floor <= input.Length; floor++)
            {
                var expected = oracle.Match(text, floor);
                var found = Utf8AsciiTokenFamilyExecutor.TryFindToken(input, floor, plan, out var index, out var length);
                Assert.Equal(expected.Success, found);
                if (found) Assert.Equal((expected.Index, expected.Length), (index, length));
            }
        }
    }

    [Fact]
    public void UriBodyMinimumCountsCompleteScalarsRatherThanSurrogateUnits()
    {
        foreach (var text in new[] { "x://😀", "𐐀://😀?q#f", "x://𐐀", "x:///😀😀" })
        {
            Assert.Equal(0, Utf8UriTokenCountExecutor.CountWellFormed(Encoding.UTF8.GetBytes(text)));
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled }) Assert.Equal(0, new Utf8Regex(Uri, options).Count(Encoding.UTF8.GetBytes(text)));
        }
        Range(Uri, " x://😀/ ", 1, 9); Range(Uri, " 𐐀://😀😀?q#f ", 1, 19);
        Assert.Equal(1, Utf8UriTokenCountExecutor.CountWellFormed("𐐀://😀😀"u8));
    }

    [Fact]
    public void UriFragmentAllowsNulAndDoesNotStartASecondTokenInsideIt()
    {
        var text = "x://😀a#\0y://ab";
        Assert.Equal(1, Utf8UriTokenCountExecutor.CountWellFormed(Encoding.UTF8.GetBytes(text)));
        Range(Uri, text, 0, Encoding.UTF8.GetByteCount(text));
    }

    [Fact]
    public void UriAliasesProveTheEntireUnicodeWordAndWhitespaceLanguages()
    {
        const string alias = @"[\p{L}\p{Mn}\p{Nd}\p{Pc}]+\x3a//[^\s\x2f\x3f\x23]+[^\s\x3f\x23]+(?:\x3f[^\s\x23]*)?(?:\x23[^\s]*)?";
        Plan(alias, Utf8FallbackDirectFamilyKind.Utf8UriToken);
        Range(alias, " 𐐀://😀/ ", 1, 12);
        var method = Classifier();
        foreach (var pattern in new[] { Uri.Replace(@"[\w]", @"[\w.]"), Uri.Replace(@"\s", @"\p{Z}"), Uri.Replace("+", "+?"), Uri.Replace(@"(?:\?", @"(?>\?") })
            Assert.False(Classify(method, Tree(pattern)).HasValue);
        AsciiOracle(Uri, new[] { "", "x://a", "x://ab", "x://a/", "x://ab#\0y://cd", "x://ab?\0#f", "x://ab y://cd", "///x://ab", "x://ab\vx://cd", "_://a\0" }, Plan(Uri, Utf8FallbackDirectFamilyKind.Utf8UriToken));
    }

    [Fact]
    public void FiveByteDateUsesItsDeclaredMinimumAndPreservesScalarRanges()
    {
        var plan = Utf8FallbackDirectFamilyPlan.ForBoundedDateToken(1, 1, 1, 1, 1, 1, (byte)'/', (byte)'/', false, false);
        Assert.True(Utf8AsciiBoundedDateTokenExecutor.TryFindAsciiBoundedDateToken("1/2/3"u8, 0, plan, out var index, out var length));
        Assert.Equal((0, 5), (index, length)); Assert.Equal(1, Utf8AsciiBoundedDateTokenExecutor.CountAsciiBoundedDateTokens("1/2/3"u8, plan));
        Range(@"\d{1}/\d{1}/\d{1}", "😀 ١/٢/٣", 5, 8);
    }

    [Fact]
    public void DateAliasesBorrowFieldsAndBoundariesWithoutOwningTheTree()
    {
        const string pattern = @"\b(?<day>\p{Nd}{1,2})\x2f(?:[0-9]{1,2})\x2d\d{2,4}\b";
        var plan = Plan(pattern, Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken);
        Assert.Equal((1, 2, 1, 2, 2, 4), ((int)plan.FirstFieldMinCount, plan.FirstFieldMaxCount, plan.SecondFieldMinCount, plan.SecondFieldMaxCount, plan.ThirdFieldMinCount, plan.ThirdFieldMaxCount));
        Assert.True(plan.RequireLeadingBoundary); Assert.True(plan.RequireTrailingBoundary);
        Range(pattern, "😀 ١/2-٢٠ ", 5, 9);
        var captured = new Utf8Regex(pattern).MatchDetailed(Encoding.UTF8.GetBytes("😀 ١/2-٢٠ ")).GetGroup("day");
        Assert.Equal((5, 2), (captured.IndexInBytes, captured.LengthInBytes));
        var input = Encoding.UTF8.GetBytes("𐐀1/2-20"); Assert.False(new Utf8Regex(pattern).IsMatch(input));
        Assert.False(Utf8AsciiTokenFamilyExecutor.TryCountTokens(input, false, plan, out _, out _));
        var method = Classifier();
        foreach (var unproved in new[] { @"\d{0,2}/\d{1}/\d{1}", @"\d{1,2}?/\d{1}/\d{1}", @"(?:\d{2}){2}/\d{1}/\d{1}", @"[\dA]{1}/\d{1}/\d{1}", @"\d{1}.\d{1}/\d{1}" })
            Assert.False(Classify(method, Tree(unproved)).HasValue);
        Range(@"\d{0,2}/\d{1}/\d{1}", "😀 /2/3", 5, 4);
    }

    [Fact]
    public void BoundedDateNativeRangesAgreeWithIndependentAsciiOracleAtEveryFloor()
    {
        var subjects = Enumerable.Range(0, 256).Select(bits => new string(Enumerable.Range(0, 7).Select(bit => (bits & (1 << bit)) == 0 ? '1' : '/').ToArray()));
        foreach (var pattern in new[] { @"\d{1}/\d{1}/\d{1}", @"\d{1,2}/\d{1,2}/\d{1,3}", @"\b\d{1,2}/\d{1,2}/\d{2,4}\b" })
            AsciiOracle(pattern, subjects.Concat(["1/2/3", "11/22/333", "a1/2/33b", " 11/2/33 ", "1/2/3 1/2/3"]), Plan(pattern, Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken));
    }

    [Fact]
    public void Ipv4AliasesUseOneSelectorAndPreserveOrderedOctetBranches()
    {
        var alias = Ip.Replace("25", @"\x32\x35").Replace(@"\.", @"\x2e");
        Plan(alias, Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount);
        Range(alias, "😀00.11.25.255", 4, 12);
        var reorderedOctet = @"(?:[01]?[0-9][0-9]|25[0-5]|2[0-4][0-9])";
        var reordered = "(?:" + Octet + @"\.){3}" + reorderedOctet;
        Assert.False(Classify(Classifier(), Tree(reordered)).HasValue);
        // In RTL the two-digit arm leaves a digit before the separator and
        // backtracks to the three-digit arm. Keep both directional outcomes.
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(reordered, options | RegexOptions.CultureInvariant);
            var expectedLength = (options & RegexOptions.RightToLeft) != 0 ? 12 : 11;
            var match = regex.Match("00.11.22.255"u8); var detailed = regex.MatchDetailed("00.11.22.255"u8);
            Assert.Equal((0, expectedLength), (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal((0, expectedLength), (detailed.IndexInBytes, detailed.LengthInBytes));
            Assert.True(regex.IsMatch("00.11.22.255"u8)); Assert.Equal(1, regex.Count("00.11.22.255"u8));
        }
        Assert.False(Classify(Classifier(), Tree(Ip.Replace("[0-5]", "[0-6]"))).HasValue);
    }

    [Fact]
    public void Ipv4NativeRangesAgreeWithOrderedAsciiOracleAtEveryFloor()
    {
        var subjects = from a in new[] { "00", "199", "255", "256" } from b in new[] { "01", "249", "255" }
            from c in new[] { "00", "200", "255" } from d in new[] { "0", "00", "099", "199", "200", "249", "255", "256", "999" }
            select $"!{a}.{b}.{c}.{d}!";
        AsciiOracle(Ip, subjects.Concat(["00.11.22.25500", "255.255.255.255", "00.00.00.00 11.11.11.11"]), Plan(Ip, Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount));
        Assert.False(Utf8AsciiIpv4TokenExecutor.TryFindAsciiIpv4Token("00.00.00.00"u8, int.MaxValue, out _, out _));
    }

    [Fact]
    public void EveryProfileProofCutoffRollsBackScratchAndRetainsNoPayloadLoan()
    {
        var method = Classifier();
        foreach (var pattern in new[] { Uri, Ip, @"\b\d{1,2}/\d{1,2}/\d{2,4}\b" })
        {
            var tree = Tree(pattern); ulong peak, work;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            { Assert.True(Classify(method, tree).HasValue, Describe(tree.Root)); Assert.Equal(0ul, funded.Budget.ReservedBytes); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
            Assert.InRange(peak, 1ul, 20000ul); Assert.InRange(work, 1ul, 20000ul);
            foreach (var bytes in new[] { true, false })
            {
                var successes = 0; var limit = bytes ? peak : work;
                for (var cutoff = 0ul; cutoff <= limit; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
                    var failure = Record.Exception(() => Classify(method, tree));
                    if (failure is null) successes++;
                    else Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
                    Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                    Assert.True(bytes ? owner.Budget.PeakBytes <= 128 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
                }
                if (bytes) Assert.InRange(successes, 1, (int)limit + 1); else Assert.Equal(1, successes);
            }
        }
    }

    [Fact]
    public void ActualProfileWrappersAreAdmittedAndDeadTextSelectorsAreAbsent()
    {
        var tree = Tree(Uri); var root = tree.Root;
        for (var depth = 0; depth < 65; depth++) root = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, RegexOptions.CultureInvariant).WithChild(root);
        var deep = Utf8OwnedRegexTree.Create(root, tree.CaptureCount, tree.CaptureNames, tree.CaptureNameToNumberMapping, tree.CaptureNumberSparseMapping,
            tree.Options, tree.Culture, tree.CaptureCatalog, tree.UsesLiteralClassBracketSyntax, tree.UsesIgnoreCase);
        var method = Classifier(); using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.Throws<Utf8ResourceLimitExceededException>(() => Classify(method, deep)).ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var spent = owner.Budget.WorkSteps; Assert.True(Classify(method, tree).HasValue); Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.WorkSteps > spent); Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        foreach (var name in new[] { "TryParseUtf8UriToken", "TryParseAsciiIpv4Token", "TryParseAsciiBoundedDateToken", "TryConsumeDigitBound", "TryConsumeAsciiDateSeparator" })
            Assert.DoesNotContain(typeof(Utf8FallbackRegexFamilyAnalyzer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static), m => m.Name == name);
        Assert.DoesNotContain("AsciiIpv4Token", Enum.GetNames<Utf8FallbackDirectFamilyKind>());
    }
}
