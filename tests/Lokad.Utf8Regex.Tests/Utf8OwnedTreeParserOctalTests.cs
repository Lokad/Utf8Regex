using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned parser octal stage: the differential battery pins octal escapes identical
// to the provider trees (longest-prefix fallback with rest replay, class octal
// units, and brace-zero loop elimination with empty pruning), plus decline
// agreement on non-octal and single-digit rests.
public sealed class Utf8OwnedTreeParserOctalTests
{
    public static IEnumerable<object?[]> OctalAcceptCases()
    {
        yield return new object?[] { "\\178x", 512 };
        yield return new object?[] { "\\19+", 512 };
        yield return new object?[] { "\\19", 512 };
        yield return new object?[] { "\\1998", 512 };
        yield return new object?[] { "\\1000", 512 };
        yield return new object?[] { "\\18x", 512 };
        yield return new object?[] { "\\12x", 512 };
        yield return new object?[] { "\\123x", 512 };
        yield return new object?[] { "\\1234x", 512 };
        yield return new object?[] { "\\08x", 512 };
        yield return new object?[] { "\\008x", 512 };
        yield return new object?[] { "\\0008x", 512 };
        yield return new object?[] { "\\178", 512 };
        yield return new object?[] { "\\198", 512 };
        yield return new object?[] { "\\777x", 512 };
        yield return new object?[] { "\\7777x", 512 };
        yield return new object?[] { "\\400x", 512 };
        yield return new object?[] { "(a)\\178x", 512 };
        yield return new object?[] { "\\178x", 513 };
        yield return new object?[] { "[\\178]", 512 };
        yield return new object?[] { "(a){0}(b){0}", 512 };
        yield return new object?[] { "(a){0}|b", 512 };
        yield return new object?[] { "\\101{0}", 512 };
        yield return new object?[] { "\\10{0}", 512 };
        yield return new object?[] { "[\\10-a]", 512 };
        yield return new object?[] { "[\\12-a]", 512 };
        yield return new object?[] { "[\\178-a]", 512 };
        yield return new object?[] { "(?i:(a){0}(b){0})", 512 };
        yield return new object?[] { "((?i:a){0}(b){0})", 512 };
        yield return new object?[] { "((b){0}(?i:a){0})", 512 };
        yield return new object?[] { "(?i:(?:(?i:a){0}(b){0}))", 512 };
        yield return new object?[] { "\\1{0}", 513 };
        yield return new object?[] { "\\8{0}", 512 };
        yield return new object?[] { "\\9{0}", 512 };
        yield return new object?[] { "(?<x>a)\\k<x>{0}", 512 };
        yield return new object?[] { "(?<x>a)(\\k<x>{0})", 512 };
        yield return new object?[] { "\\178x{2}", 513 };
        yield return new object?[] { "\\1012x{2}", 513 };
        yield return new object?[] { "\\178{0}", 512 };
        yield return new object?[] { "x(?m:(a){0}(b){0})", 512 };
        yield return new object?[] { "(?i)(a){0}(b){0}(?-i)", 512 };
        yield return new object?[] { "((?i:a){0}(?m:b){0})", 512 };
        yield return new object?[] { "(a){0}b", 512 };
        yield return new object?[] { "\\1012x", 513 };
        yield return new object?[] { "(a)\\178x{2}", 512 };
        yield return new object?[] { "((\\178))", 512 };
        yield return new object?[] { "\\1789{2}", 512 };
        yield return new object?[] { "(a)\\1{0}b", 512 };
        yield return new object?[] { "\\1{0}", 512 };
        yield return new object?[] { "(a)\\178{2}", 512 };
        yield return new object?[] { "[\\18x]", 512 };
        yield return new object?[] { "[\\1x]", 512 };
        yield return new object?[] { "[\\08]", 512 };
        yield return new object?[] { "(a)\\1{0}", 512 };
        yield return new object?[] { "(\\k<x>{0})", 512 };
        yield return new object?[] { "(?<x>a)\\k<y>{0}", 512 };
        yield return new object?[] { "\\k<undefined>{0}", 512 };
        yield return new object?[] { "\\19{0}", 512 };
        yield return new object?[] { "\\178{2}", 512 };
        yield return new object?[] { "(a)\\19+", 512 };
        yield return new object?[] { "\\178+", 512 };
        yield return new object?[] { "\\178?", 512 };
        yield return new object?[] { "\\178*", 512 };
        yield return new object?[] { "(a)\\178?", 512 };
        yield return new object?[] { "(a){0}?", 512 };
        yield return new object?[] { "\\101{0}", 513 };
        yield return new object?[] { "((a)\\1{0})", 512 };
        yield return new object?[] { "(?<x>a)(?n:(b))", 512 };
    }

    public static IEnumerable<object?[]> OctalDeclineCases()
    {
        yield return new object?[] { "\\1x", 512 };
        yield return new object?[] { "\\8x", 512 };
        yield return new object?[] { "\\9x", 512 };
        yield return new object?[] { "[\\9x]", 512 };
        yield return new object?[] { "\\89x", 512 };
        yield return new object?[] { "[\\8x]", 512 };
    }

    [Theory]
    [MemberData(nameof(OctalAcceptCases))]
    public void OwnedOctalMatchesProviderTree(string pattern, int options)
    {
        var expected = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options);
        Assert.NotNull(expected);
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        Assert.Equal(expected.CaptureCount, actual.CaptureCount);
        Assert.Equal(expected.CaptureNames ?? [], actual.CaptureNames ?? []);
        Assert.Equal(expected.Options, actual.Options);
        Assert.Equal(expected.Culture, actual.Culture);
        AssertHashtableEqual(expected.CaptureNameToNumberMapping, actual.CaptureNameToNumberMapping);
        AssertHashtableEqual(expected.CaptureNumberSparseMapping, actual.CaptureNumberSparseMapping);
        Assert.True(TreesEqual(expected.Root, actual.Root));
    }

    [Theory]
    [MemberData(nameof(OctalDeclineCases))]
    public void OwnedOctalDeclinesUnsupported(string pattern, int options)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        Assert.Throws<RuntimeFrontEnd.RegexParseException>(() => RuntimeFrontEnd.RegexParser.Parse(
            pattern,
            (RegexOptions)options,
            RuntimeFrontEnd.RegexParser.GetTargetCulture((RegexOptions)options)));
    }

    private static void AssertHashtableEqual(System.Collections.Hashtable? left, System.Collections.Hashtable? right)
    {
        if (left is null || right is null)
        {
            Assert.True(left is null && right is null);
            return;
        }
        Assert.Equal(left.Count, right.Count);
        foreach (var key in left.Keys)
        {
            Assert.True(right.ContainsKey(key));
            Assert.Equal(left[key], right[key]);
        }
    }

    private static bool TreesEqual(RuntimeFrontEnd.RegexNode left, RuntimeFrontEnd.RegexNode right)
    {
        return right.Kind == left.Kind &&
            right.Options == left.Options &&
            right.Ch == left.Ch &&
            right.Str == left.Str &&
            right.Str2 == left.Str2 &&
            right.M == left.M &&
            right.N == left.N &&
            right.ChildCount == left.ChildCount &&
            Enumerable.Range(0, left.ChildCount).All(index => TreesEqual(left.Child(index), right.Child(index)));
    }
}
