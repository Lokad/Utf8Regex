using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R03 lazy fallback construction: building a Utf8Regex never constructs the BCL engine,
/// and owned routes across every public operation never construct it either. The first
/// fallback use constructs once. This is the construction half of the R03 engine gate.
/// </summary>
public sealed class LazyFallbackConstructionTests
{
    public static IEnumerable<object?[]> FreshCases()
    {
        string[] patterns =
        [
            "abc",
            "cat|horse",
            "(cat|horse)",
            "(?<w>cat)|(?<v>horse)",
            "(cat)|(horse)",
            "(caf\u00E9)|(ni\u00F1o)",
            "(a)",
            "(?:cat|horse)(?=horse)",
            "\\b(?:cat|horse)\\b",
            "^abc",
            "abc$",
            "\\bword\\b",
            "[a-z]+",
            "(a)(b)",
            "(a)+",
            "a*",
            "\\d+",
            "\U0001F600",
        ];
        RegexOptions[] options =
        [
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.None,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        ];
        foreach (var pattern in patterns)
        {
            foreach (var option in options)
            {
                yield return new object?[] { pattern, option };
            }
        }
    }

    [Theory]
    [MemberData(nameof(FreshCases))]
    public void FreshInstanceConstructsNoFallbackEngine(string pattern, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> OwnedCases()
    {
        string[] patterns =
        [
            "abc",
            "cat|horse",
            "(cat|horse)",
            "(?<w>cat)|(?<v>horse)",
            "(cat)|(horse)",
            "(caf\u00E9)|(ni\u00F1o)",
            "(a)",
            "(?:cat|horse)(?=horse)",
        ];
        string[] asciiInputs =
        [
            "cat horse fish horse",
            "horsecat",
            "no match here",
            "",
            "CAT horse Cat",
        ];
        // ASCII-literal shapes divert enumeration to BCL verification on non-ASCII
        // input (established R03 projection rule), so only the rest take unicode input.
        string[] unicodeInputs =
        [
            "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3r\u00E9sum\u00E9\u03B4",
        ];
        string[] asciiOnlyPatterns = ["abc", "(a)"];
        foreach (var pattern in patterns)
        {
            foreach (var input in asciiInputs)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, input };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, input };
            }

            if (System.Array.IndexOf(asciiOnlyPatterns, pattern) < 0)
            {
                foreach (var input in unicodeInputs)
                {
                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, input };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, input };
                }
            }
        }

        yield return new object?[] { "(CAT)|(DOG)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "cat dog fish" };
        yield return new object?[] { "cat|horse", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "CAT horse" };
    }

    [Theory]
    [MemberData(nameof(OwnedCases))]
    public void OwnedOperationsConstructNoFallbackEngine(string pattern, RegexOptions options, string inputText)
    {
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        _ = regex.IsMatch(input);
        _ = regex.Count(input);
        _ = regex.Match(input);
        foreach (var item in regex.EnumerateMatches(input)) { _ = item; }
        foreach (var item in regex.EnumerateSplits(input)) { _ = item; }
        Touch(regex.MatchDetailed(input));
        Touch(regex.MatchDetailedFromUtf16Offset(input, 0));

        _ = regex.Replace(input, "X");
        _ = regex.Replace(input, Encoding.UTF8.GetBytes("X"));
        _ = regex.ReplaceToString(input, "X");
        if (inputText.Length > 0)
        {
            // Empty-input evaluator Replace throws on the writer capacity (pre-existing).
            _ = regex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { writer.Append(Encoding.UTF8.GetBytes("X")); });
            _ = regex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "X");
        }

        var destination = new byte[Math.Max(input.Length * 4, 16)];
        _ = regex.TryReplace(input, "X", destination, out _);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern + " options=" + options.ToString() + " input=" + inputText);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated, "pattern=" + pattern + " options=" + options.ToString() + " input=" + inputText);
    }

    public static IEnumerable<object?[]> OwnedFromOffsetCases()
    {
        yield return new object?[] { "cat|horse", RegexOptions.CultureInvariant, "cat horse fish horse", 1 };
        yield return new object?[] { "cat|horse", RegexOptions.CultureInvariant, "cat horse fish horse", 5 };
        yield return new object?[] { "(cat|horse)", RegexOptions.CultureInvariant, "cat horse fish horse", 4 };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "cat horse fish horse", 4 };
        yield return new object?[] { "(?<w>cat)|(?<v>horse)", RegexOptions.CultureInvariant, "cat horse fish", 4 };
        yield return new object?[] { "(a)", RegexOptions.CultureInvariant, "baac", 2 };
        yield return new object?[] { "(?:cat|horse)(?=horse)", RegexOptions.CultureInvariant, "cathorsecathorse", 3 };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", RegexOptions.None, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 2 };
    }

    [Theory]
    [MemberData(nameof(OwnedFromOffsetCases))]
    public void OwnedFromOffsetConstructsNoFallbackEngine(string pattern, RegexOptions options, string inputText, int offset)
    {
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Touch(regex.MatchDetailedFromUtf16Offset(input, offset));
        foreach (var item in regex.EnumerateMatchesFromUtf16Offset(input, offset)) { _ = item; }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern + " offset=" + offset.ToString());
    }

    [Theory]
    [InlineData("\\b(?:cat|horse)\\b", "CAT horse Cat")]
    [InlineData("(?<=a)(b)|(c)", "xabc")]
    [InlineData("^abc", "xxabc")]
    public void OriginSensitiveFromOffsetConstructsFallbackEngine(string pattern, string inputText)
    {
        // From-offset origin shapes (anchors, boundaries, lookbehind) divert to full-input
        // BCL verification inside the shared enumerator (established taxonomy).
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        Touch(regex.MatchDetailedFromUtf16Offset(input, 1));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("[a-z]+", "xxabcxx")]
    [InlineData("(a)(b)", "xxabxx")]
    [InlineData("(a)+", "aaabaaa")]
    public void FallbackOperationsConstructFallbackEngine(string pattern, string inputText)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        Touch(regex.MatchDetailed(input));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^abc", "xxabc")]
    public void AnchoredFallbackConstructsAnchoredEngine(string pattern, string inputText)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        _ = regex.Match(input);
        // Match on anchored shapes runs verification through the anchored engine only:
        // the primary engine is never constructed by this operation.
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        Assert.True(regex.Inspection.DebugAnchoredFallbackRegexIsCreated, "pattern=" + pattern);
    }


    private static void Touch(Utf8MatchContext match)
    {
        _ = match.Success;
        _ = match.IndexInUtf16;
        _ = match.GetValueString();
        for (var i = 0; i < match.GroupCount; i++)
        {
            var group = match.GetGroup(i);
            _ = group.GetValueString();
            for (var j = 0; j < group.CaptureCount; j++)
            {
                _ = group.GetCapture(j).GetValueString();
            }
        }
    }
}
