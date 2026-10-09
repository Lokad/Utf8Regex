using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R03 split ownership for literal families: Split text operations on literal-family shapes
/// run on the owned cursors instead of the BCL fallback. Split segments are match-delimited
/// gaps (BCL EnumerateSplits never splices group captures, unlike array Split), so grouped
/// shapes ride the same group-agnostic cursors; only the compiled engine kind gates the
/// route. Bare boundary-anchored alternations (which leave the family), right-to-left
/// shapes use separate owned scalar cursors.
/// </summary>
public sealed class LiteralFamilyTextOperationOwnershipTests
{
    public static IEnumerable<object?[]> NativeRouteCases()
    {
        yield return new object?[] { "cat|horse", RegexOptions.CultureInvariant };
        yield return new object?[] { "cat|horse", RegexOptions.Compiled | RegexOptions.CultureInvariant };
        yield return new object?[] { "cat|horse", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant };
        yield return new object?[] { "a|ab|abc", RegexOptions.CultureInvariant };
        yield return new object?[] { "a|b|c", RegexOptions.CultureInvariant };
        yield return new object?[] { "Sherlock Holmes|John Watson|Irene Adler", RegexOptions.None };
        yield return new object?[] { "caf\u00E9|ni\u00F1o|r\u00E9sum\u00E9", RegexOptions.None };
        yield return new object?[] { "(?:cat|horse)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(?:cat|horse)(?=horse)", RegexOptions.CultureInvariant };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant };
        yield return new object?[] { "\\b(cat|horse)\\b", RegexOptions.CultureInvariant };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.Compiled | RegexOptions.CultureInvariant };
        yield return new object?[] { "(?<w>cat)|(?<v>horse)", RegexOptions.CultureInvariant };
    }

    [Theory]
    [MemberData(nameof(NativeRouteCases))]
    public void LiteralFamilyTextOperationsRouteNatively(string pattern, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes("cat caf\u00E9 horse ni\u00F1o");
        Assert.True(regex.Inspection.DebugCanUseNativeSplit(input), "pattern=" + pattern + " options=" + options.ToString());
    }

    public static IEnumerable<object?[]> ExcludedRouteCases()
    {
        yield return new object?[] { "cat|horse\\b", RegexOptions.CultureInvariant }; // Bare boundary-anchored alternations leave the literal family.
        yield return new object?[] { "cat|horse", RegexOptions.RightToLeft | RegexOptions.CultureInvariant }; // Right-to-left scanning uses the scalar cursor.
    }

    [Theory]
    [MemberData(nameof(ExcludedRouteCases))]
    public void NonLiteralFamilySplitShapesUseSeparateOwnedRoutes(string pattern, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes("cat horse caf\u00E9 noir");
        Assert.False(regex.Inspection.DebugCanUseNativeSplit(input), "pattern=" + pattern + " options=" + options.ToString());
    }

    public static IEnumerable<object?[]> SplitParityCases()
    {
        string[] patterns =
        [
            "cat|horse",
            "cat|dog|horse",
            "a|b|c",
            "a|ab|abc",
            "ab|abc|abcd",
            "Sherlock Holmes|John Watson|Irene Adler",
            "caf\u00E9|ni\u00F1o|r\u00E9sum\u00E9",
            "x|y|z",
            "(?:cat|horse)",
            "(?:cat|horse)(?=horse)",
            "(cat)|(horse)",
            "(?<w>cat)|(?<v>horse)",
            "(cat)|horse",
        ];
        RegexOptions[] options =
        [
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        ];
        string[] inputs =
        [
            "cat horse fish horse",
            "horsecat",
            "cathorse",
            "catcat",
            "no match here",
            "",
            "cat",
            "horse",
            "cat horse cat horse cat",
            "CAT HORSE",
            "Cat Horse",
            "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3r\u00E9sum\u00E9\u03B4",
            "abcababa",
        ];
        int[] counts = [1, 2, 3, int.MaxValue];
        foreach (var pattern in patterns)
        {
            foreach (var option in options)
            {
                if (new Utf8Regex(pattern, option).Inspection.ExecutionKind is not (NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals))
                {
                    continue;
                }

                foreach (var input in inputs)
                {
                    foreach (var count in counts)
                    {
                        yield return new object?[] { pattern, option, input, count };
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(SplitParityCases))]
    public void OwnedLiteralFamilySplitCountsMatchOverlappingComparator(string pattern, RegexOptions options, string inputText, int count)
    {
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(regex.Inspection.DebugCanUseNativeSplit(input), "pattern=" + pattern + " options=" + options.ToString());
        // Literal alternatives and BMP subjects overlap the comparator's
        // code-unit semantics. EnumerateSplits returns gaps without captures.
        var oracle = new Regex(pattern, options);
        // .NET 10's split enumerator throws on its first MoveNext for count 1.
        // The scalar contract returns the whole subject as one segment.
        var expected = 1;
        if (count != 1)
        {
            expected = 0;
            foreach (var _ in oracle.EnumerateSplits(inputText, count)) expected++;
        }
        Assert.Equal(expected, regex.Inspection.DebugCountSplitsViaCompiledEngine(input, count));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> ReplaceParityCases()
    {
        string[] patterns = ["cat|horse", "a|ab|abc", "caf\u00E9|ni\u00F1o|r\u00E9sum\u00E9", "(cat)|(horse)"];
        RegexOptions[] options = [RegexOptions.CultureInvariant, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant];
        string[] inputs = ["cat horse fish horse", "horsecat", "no match", "", "CAT HORSE", "\u03B1caf\u00E9\u03B2"];
        string[] replacements = ["X", "$&", "<$&>", "", "pre$" + "`" + "post", "q$" + "'" + "q", "$$", "$_"];
        foreach (var pattern in patterns)
        {
            foreach (var option in options)
            {
                if (new Utf8Regex(pattern, option).Inspection.ExecutionKind is not (NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals))
                {
                    continue;
                }

                foreach (var input in inputs)
                {
                    foreach (var replacement in replacements)
                    {
                        yield return new object?[] { pattern, option, input, replacement };
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(ReplaceParityCases))]
    public void OwnedLiteralFamilyReplaceLengthsMatchOverlappingComparator(string pattern, RegexOptions options, string inputText, string replacement)
    {
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var nativeLength = regex.Inspection.DebugReplaceViaNativeTextOperations(input, replacement);
        Assert.True(nativeLength >= 0, "pattern=" + pattern + " options=" + options.ToString() + " replacement=" + replacement);
        var oracle = new Regex(pattern, options);
        Assert.Equal(Encoding.UTF8.GetByteCount(oracle.Replace(inputText, replacement)), nativeLength);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
