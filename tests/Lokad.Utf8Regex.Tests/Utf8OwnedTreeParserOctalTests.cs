using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Owned parser octal stage: the BCL-oracled battery pins octal escapes with BCL metadata
// with BCL metadata (longest-prefix fallback with rest replay, class octal
// units, and brace-zero loop elimination with empty pruning), plus decline
// agreement on non-octal and single-digit rests.
public sealed class Utf8OwnedTreeParserOctalTests
{
    public static IEnumerable<object?[]> OctalAcceptCases()
    {
        yield return new object?[] { "\\178x", 512, 512 };
        yield return new object?[] { "\\19+", 512, 512 };
        yield return new object?[] { "\\19", 512, 512 };
        yield return new object?[] { "\\1998", 512, 512 };
        yield return new object?[] { "\\1000", 512, 512 };
        yield return new object?[] { "\\18x", 512, 512 };
        yield return new object?[] { "\\12x", 512, 512 };
        yield return new object?[] { "\\123x", 512, 512 };
        yield return new object?[] { "\\1234x", 512, 512 };
        yield return new object?[] { "\\08x", 512, 512 };
        yield return new object?[] { "\\008x", 512, 512 };
        yield return new object?[] { "\\0008x", 512, 512 };
        yield return new object?[] { "\\178", 512, 512 };
        yield return new object?[] { "\\198", 512, 512 };
        yield return new object?[] { "\\777x", 512, 512 };
        yield return new object?[] { "\\7777x", 512, 512 };
        yield return new object?[] { "\\400x", 512, 512 };
        yield return new object?[] { "(a)\\178x", 512, 512 };
        yield return new object?[] { "\\178x", 513, 513 };
        yield return new object?[] { "[\\178]", 512, 512 };
        yield return new object?[] { "(a){0}(b){0}", 512, 512 };
        yield return new object?[] { "(a){0}|b", 512, 512 };
        yield return new object?[] { "\\101{0}", 512, 512 };
        yield return new object?[] { "\\10{0}", 512, 512 };
        yield return new object?[] { "[\\10-a]", 512, 512 };
        yield return new object?[] { "[\\12-a]", 512, 512 };
        yield return new object?[] { "[\\178-a]", 512, 512 };
        yield return new object?[] { "(?i:(a){0}(b){0})", 512, 513 };
        yield return new object?[] { "((?i:a){0}(b){0})", 512, 512 };
        yield return new object?[] { "((b){0}(?i:a){0})", 512, 512 };
        yield return new object?[] { "(?i:(?:(?i:a){0}(b){0}))", 512, 513 };
        yield return new object?[] { "(?<x>a)\\k<x>{0}", 512, 512 };
        yield return new object?[] { "(?<x>a)(\\k<x>{0})", 512, 512 };
        yield return new object?[] { "\\178x{2}", 513, 513 };
        yield return new object?[] { "\\1012x{2}", 513, 513 };
        yield return new object?[] { "\\178{0}", 512, 512 };
        yield return new object?[] { "x(?m:(a){0}(b){0})", 512, 512 };
        yield return new object?[] { "(?i)(a){0}(b){0}(?-i)", 512, 513 };
        yield return new object?[] { "((?i:a){0}(?m:b){0})", 512, 512 };
        yield return new object?[] { "(a){0}b", 512, 512 };
        yield return new object?[] { "\\1012x", 513, 513 };
        yield return new object?[] { "(a)\\178x{2}", 512, 512 };
        yield return new object?[] { "((\\178))", 512, 512 };
        yield return new object?[] { "\\1789{2}", 512, 512 };
        yield return new object?[] { "(a)\\1{0}b", 512, 512 };
        yield return new object?[] { "(a)\\178{2}", 512, 512 };
        yield return new object?[] { "[\\18x]", 512, 512 };
        yield return new object?[] { "[\\1x]", 512, 512 };
        yield return new object?[] { "[\\08]", 512, 512 };
        yield return new object?[] { "(a)\\1{0}", 512, 512 };
        yield return new object?[] { "\\19{0}", 512, 512 };
        yield return new object?[] { "\\178{2}", 512, 512 };
        yield return new object?[] { "(a)\\19+", 512, 512 };
        yield return new object?[] { "\\178+", 512, 512 };
        yield return new object?[] { "\\178?", 512, 512 };
        yield return new object?[] { "\\178*", 512, 512 };
        yield return new object?[] { "(a)\\178?", 512, 512 };
        yield return new object?[] { "(a){0}?", 512, 512 };
        yield return new object?[] { "\\101{0}", 513, 513 };
        yield return new object?[] { "((a)\\1{0})", 512, 512 };
        yield return new object?[] { "(?<x>a)(?n:(b))", 512, 512 };
    }

    public static IEnumerable<object?[]> OctalPrunedRefDeclineCases()
    {
        // Pruned ({0}) references to undefined groups still fail construction
        // exactly like the BCL oracle, with the pinned construction error.
        yield return new object?[] { "\\1{0}", 513, RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "\\8{0}", 512, RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "\\9{0}", 512, RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "\\1{0}", 512, RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "(\\k<x>{0})", 512, RegexParseError.UndefinedNamedReference, 6 };
        yield return new object?[] { "(?<x>a)\\k<y>{0}", 512, RegexParseError.UndefinedNamedReference, 12 };
        yield return new object?[] { "\\k<undefined>{0}", 512, RegexParseError.UndefinedNamedReference, 13 };
    }

    [Theory]
    [MemberData(nameof(OctalPrunedRefDeclineCases))]
    public void OwnedOctalDeclinesPrunedUndefinedRef(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }
    public static IEnumerable<object?[]> OctalDeclineCases()
    {
        yield return new object?[] { "\\1x", 512, RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "\\8x", 512, RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "\\9x", 512, RegexParseError.UndefinedNumberedReference, 2 };
        yield return new object?[] { "[\\9x]", 512, RegexParseError.UnrecognizedEscape, 3 };
        yield return new object?[] { "\\89x", 512, RegexParseError.UnrecognizedEscape, 2 };
        yield return new object?[] { "[\\8x]", 512, RegexParseError.UnrecognizedEscape, 3 };
    }

    [Theory]
    [MemberData(nameof(OctalAcceptCases))]
    public void OwnedOctalServesWithBclMetadata(string pattern, int options, int expectedOptions)
    {
        var actual = Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options);
        Assert.NotNull(actual);
        var oracle = new Regex(pattern, (RegexOptions)options);
        Assert.Equal(oracle.GetGroupNumbers().Length, actual.CaptureCount);
        Assert.Equal(oracle.GetGroupNames(), actual.CaptureNames ?? []);
        Assert.Equal((RegexOptions)expectedOptions, actual.Options);
        Assert.Equal(CultureInfo.InvariantCulture.Name, actual.Culture?.Name);
        AssertGroupNameMapping(oracle, actual.CaptureNameToNumberMapping);
        AssertGroupNumberMapping(oracle, actual.CaptureNumberSparseMapping);
    }

    [Theory]
    [MemberData(nameof(OctalDeclineCases))]
    public void OwnedOctalDeclinesUnsupported(string pattern, int options, RegexParseError error, int offset)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, (RegexOptions)options));
        var thrown = Assert.Throws<RegexParseException>(() => new Regex(pattern, (RegexOptions)options));
        Assert.Equal(error, thrown.Error);
        Assert.Equal(offset, thrown.Offset);
    }

    private static void AssertGroupNameMapping(Regex oracle, System.Collections.Hashtable? actual)
    {
        var names = oracle.GetGroupNames();
        if (actual is null)
        {
            Assert.All(names, static name => Assert.True(int.TryParse(name, out _), "Non-numeric group name without a mapping: " + name));
            return;
        }

        Assert.Equal(names.Length, actual.Count);
        foreach (var name in names)
        {
            Assert.True(actual.ContainsKey(name), "Missing name mapping: " + name);
            Assert.Equal(oracle.GroupNumberFromName(name), (int)actual[name]!);
        }
    }

    private static void AssertGroupNumberMapping(Regex oracle, System.Collections.Hashtable? actual)
    {
        var numbers = oracle.GetGroupNumbers();
        if (actual is null)
        {
            for (var index = 0; index < numbers.Length; index++)
            {
                Assert.Equal(index, numbers[index]);
            }

            return;
        }

        Assert.Equal(numbers.Length, actual.Count);
        foreach (System.Collections.DictionaryEntry entry in actual)
        {
            Assert.Equal((int)entry.Key, numbers[(int)entry.Value!]);
        }
    }

}
