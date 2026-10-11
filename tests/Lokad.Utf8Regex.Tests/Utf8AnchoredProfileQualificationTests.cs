using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AnchoredProfileQualificationTests
{
    private const string Number = @"\G-?[0-9]+(\.[0-9]+)?(e[+-]?[0-9]+)?";
    private const string Quoted = "\\G\"(?:[^\"\\\\]|\\\\.)*\"";
    private const string Decimal = @"^[-+]?\d*\.?\d*$";
    private const string Color = @"^#?([a-f0-9]{6}|[a-f0-9]{3})$";
    private const string Cell = @"^(?<col>[a-z])(?<row>(\d)+)$";
    private const string Range = @"^(?<col1>[a-z])(?<row1>(\d)+):?(?<col2>[a-z])(?<row2>(\d)+)$";
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8OwnedRegexTree Tree(string pattern, RegexOptions options)
    { var tree = Utf8FrontEnd.Analyze(pattern, options).SemanticRegex.RuntimeTree; Assert.NotNull(tree); return tree; }
    private static string Describe(Utf8TreeNode node) => $"{node.Kind}({node.M},{node.N},{(int)node.Ch},{node.Str},{node.MatchCaseInsensitive},[{string.Join(',', node.ScalarClass?.Data ?? [])}])" +
        string.Concat(Enumerable.Range(0, node.ChildCount).Select(index => "{" + Describe(node.Child(index)) + "}"));
    private static Utf8FallbackDirectFamilyPlan Plan(string pattern, RegexOptions options, Utf8FallbackDirectFamilyKind kind)
    {
        var plan = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(pattern, options));
        Assert.True(plan.Kind == kind, Describe(Tree(pattern, options).Root)); return plan;
    }
    private static MethodInfo Classifier()
    {
        var type = typeof(Utf8FrontEnd).Assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackAnchoredProfileClassifier"); Assert.NotNull(type);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        var method = type.GetMethod("Classify", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public); Assert.NotNull(method); return method;
    }
    private static Utf8FallbackDirectFamilyPlan Classify(MethodInfo method, Utf8OwnedRegexTree tree)
    {
        try { return Assert.IsType<Utf8FallbackDirectFamilyPlan>(method.Invoke(null, [tree])); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
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
    private static void Oracle(string pattern, RegexOptions options, Utf8FallbackDirectFamilyPlan plan, IEnumerable<string> subjects)
    {
        var oracle = new Regex(pattern, options);
        foreach (var text in subjects)
        {
            var input = Encoding.ASCII.GetBytes(text); var expected = oracle.Match(text);
            var actual = Utf8FallbackDirectFamilyCategories.IsAnchoredPrefixMatchFamily(plan.Kind)
                ? Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, plan, out var length)
                : Utf8AsciiAnchoredWholeFamilyExecutor.TryMatchWhole(input, plan, out length);
            Assert.Equal(expected.Success, actual);
            if (actual) Assert.Equal(expected.Length, length);
        }
    }
    private static void PublicRange(string pattern, RegexOptions options, string text, int length, bool rtl)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var lane in rtl ? new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled } : new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options | lane); var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            Assert.Equal((0, length), (match.IndexInBytes, match.LengthInBytes)); Assert.Equal((0, length), (detailed.IndexInBytes, detailed.LengthInBytes));
        }
    }

    [Fact]
    public void MissingOptionalFeaturesCannotSelectASupersetNativeProfile()
    {
        foreach (var item in new[] {
            ( @"\G[0-9]+", RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredNumberPrefix, "-1" ),
            ( @"\G-?[0-9]+", RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredNumberPrefix, "1.2e3" ),
            ( @"^\d*\d*$", RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole, "+.1" ),
            ( @"^([a-f0-9]{6}|[a-f0-9]{3})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole, "#abc" ),
            ( @"^[a-z]\d+[a-z]\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole, "a1:b2" ) })
        {
            var plan = Utf8FallbackRegexFamilyAnalyzer.Classify(Utf8FrontEnd.Analyze(item.Item1, item.Item2));
            Assert.NotEqual(item.Item3, plan.Kind);
            var expected = new Regex(item.Item1, item.Item2).Match(item.Item4);
            foreach (var lane in new[] { RegexOptions.None, RegexOptions.Compiled })
            {
                var actual = new Utf8Regex(item.Item1, item.Item2 | lane).Match(Encoding.ASCII.GetBytes(item.Item4));
                Assert.Equal(expected.Success, actual.Success);
                if (expected.Success) Assert.Equal((expected.Index, expected.Length), (actual.IndexInBytes, actual.LengthInBytes));
            }
        }
    }

    [Fact]
    public void ParsedNumberAliasesKeepRequiredOptionalGroupsAndRollbackEndpoints()
    {
        const string alias = @"\G\x2d?[0-9]+(?:\x2e[0-9]+)?(?:\x65[\x2b\x2d]?[0-9]+)?";
        var plan = Plan(alias, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredNumberPrefix);
        Oracle(alias, RegexOptions.CultureInvariant, plan, Subjects("01.-+eEx", 4).Concat(["-12.5e-2", "1.e2", "1e+", "1.2e+", "1e2.3"]));
        PublicRange(alias, RegexOptions.CultureInvariant, "-1.2e3😀", 6, false);
        var unicode = Number.Replace("[0-9]", @"\p{Nd}");
        Plan(unicode, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredNumberPrefix);
        PublicRange(unicode, RegexOptions.CultureInvariant, "١.٢e3😀", 7, false);
        Assert.False(Classify(Classifier(), Tree(Number, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).HasValue);
    }

    [Fact]
    public void SignedDecimalAliasesKeepNullableDigitsAndFinalLfAnchor()
    {
        const string alias = @"\A[+\x2d]?[\p{Nd}]*\x2e?[\p{Nd}]*\Z";
        var plan = Plan(alias, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole);
        Oracle(alias, RegexOptions.CultureInvariant, plan, Subjects("01.+-x\n", 4));
        PublicRange(alias, RegexOptions.CultureInvariant, "-١.٢\n", 6, true);
    }

    [Fact]
    public void WholeColorAndSpreadsheetExecutorsRespectFinalLfRatherThanStrictEnd()
    {
        foreach (var text in new[] { "abc\n", "#AbC\n", "#abcdef\n" })
        { Assert.True(Utf8AsciiHexColorExecutor.TryMatchWhole(Encoding.ASCII.GetBytes(text), out var length)); Assert.Equal(text.Length - 1, length); }
        Assert.True(Utf8AsciiSpreadsheetReferenceExecutor.TryMatchCellReferenceWhole("a12\n"u8, out var cell)); Assert.Equal(3, cell);
        Assert.True(Utf8AsciiSpreadsheetReferenceExecutor.TryMatchRangeReferenceWhole("a12:b34\n"u8, out var range)); Assert.Equal(7, range);
        foreach (var text in new[] { "abc\n\n", "#abc\r\n", "a1\n\n", "a1:b2\r\n" })
            Assert.False(new Utf8Regex(text.StartsWith("a1") ? Range : Color, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).IsMatch(Encoding.ASCII.GetBytes(text)));
    }

    [Fact]
    public void ColorAndCellAliasesProveLexicalClassesAndCaptureByteRanges()
    {
        const string color = @"\A\x23?(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})\Z";
        var colorPlan = Plan(color, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole);
        Oracle(color, RegexOptions.CultureInvariant, colorPlan, Subjects("aA0#x\n", 4).Concat(["#abcdef\n", "ABCDEF", "#ABCDEG"]));
        Assert.False(Classify(Classifier(), Tree(Color.Replace("[a-f0-9]", "[a-f0-9K]"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).HasValue);
        const string cell = @"(?i:\A(?<col>[a-z])(?<row>\p{Nd}+)\Z)";
        var cellPlan = Plan(cell, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole);
        Oracle(cell, RegexOptions.CultureInvariant, cellPlan, Subjects("aA09:\n", 4));
        PublicRange(cell, RegexOptions.CultureInvariant, "K١٢\n", 7, true);
        var match = new Utf8Regex(cell, RegexOptions.CultureInvariant).MatchDetailed("K١٢\n"u8);
        var col = match.GetGroup("col"); var row = match.GetGroup("row");
        Assert.Equal((0, 3), (col.IndexInBytes, col.LengthInBytes)); Assert.Equal((3, 4), (row.IndexInBytes, row.LengthInBytes));
    }

    [Fact]
    public void RangeAliasesKeepOptionalColonAndConstructionCulture()
    {
        const string pattern = @"\A(?<a>[a-zA-Z])\d+(?:\x3a)?(?<b>[a-zA-Z])\d+\Z";
        var plan = Plan(pattern, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole);
        Oracle(pattern, RegexOptions.CultureInvariant, plan, Subjects("aA1:\n", 5).Concat(["a12:b34\n", "a1b2", "a1::b2"]));
        PublicRange(pattern, RegexOptions.CultureInvariant, "a١:B٢\n", 7, true);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var tree = Tree(Cell, RegexOptions.IgnoreCase); var regex = new Utf8Regex(Cell, RegexOptions.IgnoreCase);
            Assert.False(Classify(Classifier(), tree).HasValue);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.False(regex.IsMatch("I1"u8)); Assert.True(regex.IsMatch("i1"u8)); Assert.True(regex.IsMatch("İ1"u8));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void QuotedPrefixAllowsUnescapedLfAndConsumesOneScalarAfterBackslash()
    {
        var texts = new[] { "\"a\nb\"", "\"\\😀\"", "\"😀\n\\𐐀\"" };
        foreach (var text in texts)
        { Assert.True(Utf8AsciiPrefixTokenExecutor.TryMatchQuotedStringPrefix(Encoding.UTF8.GetBytes(text), out var length)); Assert.Equal(Encoding.UTF8.GetByteCount(text), length); PublicRange(Quoted, RegexOptions.CultureInvariant, text, length, false); }
        Assert.False(Utf8AsciiPrefixTokenExecutor.TryMatchQuotedStringPrefix("\"\\\n\""u8, out _));
        Assert.False(new Utf8Regex(Quoted).IsMatch("\"\\\n\""u8));
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Quoted, options); Assert.Equal(2, regex.Count("\"😀\"\"a\""u8));
            var values = regex.EnumerateMatches("\"😀\"\"a\""u8); Assert.True(values.MoveNext());
            Assert.Equal((0, 6), (values.Current.IndexInBytes, values.Current.LengthInBytes)); Assert.True(values.MoveNext());
            Assert.Equal((6, 3), (values.Current.IndexInBytes, values.Current.LengthInBytes)); Assert.False(values.MoveNext());
        }
        Assert.Throws<ArgumentException>(() => new Utf8Regex(Quoted).IsMatch(new byte[] { 34, 34, 0xff }));
    }

    [Fact]
    public void QuotedAliasesProveFullNegativeClassAndLexicalDotLanguage()
    {
        var alias = Quoted.Replace("\\G\"", @"\G\x22").Replace("\\\\.", @"\x5c[^\n]");
        var plan = Plan(alias, RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix);
        Oracle(alias, RegexOptions.CultureInvariant, plan, Subjects("a\"\\\n\0", 5));
        var method = Classifier();
        foreach (var pattern in new[] { Quoted.Replace("*", "*?"), Quoted.Replace("*", "+"), Quoted.Replace("(?:", "(?>"), Quoted.Replace("[^", "[a^") })
            Assert.False(Classify(method, Tree(pattern, RegexOptions.CultureInvariant)).HasValue);
        Assert.False(Classify(method, Tree(Quoted, RegexOptions.Singleline | RegexOptions.CultureInvariant)).HasValue);
        PublicRange(Quoted, RegexOptions.Singleline | RegexOptions.CultureInvariant, "\"\\\n\"", 4, false);
    }

    [Fact]
    public void EveryAnchoredProfileCutoffUnwindsBorrowedScratchWithoutFinalLoans()
    {
        var method = Classifier();
        foreach (var pattern in new[] { Number, Quoted, Decimal, Color, Cell, Range })
        {
            var options = RegexOptions.CultureInvariant | (pattern == Color || pattern == Cell || pattern == Range ? RegexOptions.IgnoreCase : RegexOptions.None);
            var tree = Tree(pattern, options); ulong peak, work;
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
    public void ActualAnchoredWrappersAndLexicalAnchorsAreQualifiedWithoutDeadScanners()
    {
        var tree = Tree(Number, RegexOptions.CultureInvariant); var root = tree.Root;
        for (var depth = 0; depth < 65; depth++) root = Utf8TreeNode.Create(Utf8TreeNodeKind.Group, RegexOptions.CultureInvariant).WithChild(root);
        var deep = Utf8OwnedRegexTree.Create(root, tree.CaptureCount, tree.CaptureNames, tree.CaptureNameToNumberMapping, tree.CaptureNumberSparseMapping,
            tree.Options, tree.Culture, tree.CaptureCatalog, tree.UsesLiteralClassBracketSyntax, tree.UsesIgnoreCase);
        var method = Classifier();
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8))
        {
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ReservedBytes;
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.Throws<Utf8ResourceLimitExceededException>(() => Classify(method, deep)).ResourceKind);
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            var spent = owner.Budget.WorkSteps; Assert.True(Classify(method, tree).HasValue); Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.True(owner.Budget.WorkSteps > spent); Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        }
        foreach (var pattern in new[] { Decimal.Replace("$", @"\z"), Color.Replace("$", @"\z"), Cell.Replace("$", @"\z") })
            Assert.False(Classify(method, Tree(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).HasValue);
        Assert.False(Classify(method, Tree(Cell, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).HasValue);
        Plan(@"(?-m:" + Cell + ")", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole);
        foreach (var name in new[] { "IsAnchoredAsciiNumberPrefix", "IsAnchoredQuotedStringPrefix", "IsAnchoredAsciiSignedDecimalWhole", "IsAnchoredAsciiHexColorWhole", "IsAnchoredAsciiCellReferenceWhole", "IsAnchoredAsciiRangeReferenceWhole", "TryConsumeOptionalGroup", "TryConsumeAsciiDigitLoop" })
            Assert.DoesNotContain(typeof(Utf8FallbackRegexFamilyAnalyzer).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static), m => m.Name == name);
    }
}
