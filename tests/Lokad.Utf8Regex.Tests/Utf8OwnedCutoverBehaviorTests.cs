using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Residual grammar keeps construction, metadata, BMP corpus and seeded fuzz
// checks against the overlapping BCL contract. The three dot-compound witnesses
// use the qualified scalar comparator on the two supplementary subjects.
public sealed class Utf8OwnedCutoverBehaviorTests
{
    private static readonly string[] ProbeCorpus =
    {
        "", "a", "A", "b", "B", "ab", "aB", "Ab", "AB", "abc", "ABC", "aBc",
        "k", "K", "kK", "Kk", "aa", "AA", "aA", "Aa", "aab", "aba", "abba",
        "axxb", "1", "12", "a1", "1a", " ", "a b", "aBcDe", "AaBb", "x",
        "ax", "xa", "ababa", "aabbaa", ">", "a>", "a>b", "(a)", "(a)(b)",
        "\n", "a\nb", "ab\ncd", "\n\n", "a\rb", "\r\n", "a\n", "\nb",
    };

    [Theory]
    [MemberData(nameof(Utf8OwnedTreeParserIgnoreCaseTests.IgnoreCaseResidualCases), MemberType = typeof(Utf8OwnedTreeParserIgnoreCaseTests))]
    public void IgnoreCaseResidualCutoverDisposition(string pattern, int options)
    {
        AssertCutoverDisposition(pattern, (RegexOptions)options);
    }

    [Theory]
    [MemberData(nameof(Utf8OwnedTreeParserBalanceTests.BalanceResidualCases), MemberType = typeof(Utf8OwnedTreeParserBalanceTests))]
    public void BalanceResidualCutoverDisposition(string pattern, int options)
    {
        AssertCutoverDisposition(pattern, (RegexOptions)options);
    }

    [Theory]
    [MemberData(nameof(Utf8OwnedTreeParserShorthandTests.ShorthandResidualCases), MemberType = typeof(Utf8OwnedTreeParserShorthandTests))]
    public void ShorthandResidualCutoverDisposition(string pattern, RegexParseError error, int offset)
    {
        AssertCutoverDisposition(pattern, RegexOptions.CultureInvariant);
    }

    [Theory]
    [MemberData(nameof(Utf8OwnedTreeParserSubtractionTests.SubtractionResidualCases), MemberType = typeof(Utf8OwnedTreeParserSubtractionTests))]
    public void SubtractionResidualCutoverDisposition(string pattern, RegexParseError error, int offset)
    {
        AssertCutoverDisposition(pattern, RegexOptions.CultureInvariant);
    }

    private static void AssertCutoverDisposition(string pattern, RegexOptions options)
    {
        var witness = FindCutoverWitness(pattern, options);
        Assert.Null(witness);
    }

    // Null when the current product route agrees with BCL over the whole corpus
    // (construction verdicts plus first-match positions and counts), else a short
    // machine-observed witness description.
    private static string? FindCutoverWitness(string pattern, RegexOptions options)
    {
        Regex? bcl = null;
        Utf8Regex? product = null;
        Exception? bclError = null;
        Exception? productError = null;
        try
        {
            bcl = new Regex(pattern, options);
        }
        catch (Exception ex)
        {
            bclError = ex;
        }

        try
        {
            product = new Utf8Regex(pattern, options);
        }
        catch (Exception ex)
        {
            productError = ex;
        }

        if (bclError is not null || productError is not null)
        {
            if (bclError is null || productError is null)
            {
                return "construct:bcl-" + (bclError is null ? "serves" : "throws-" + bclError.GetType().Name)
                    + "/product-" + (productError is null ? "serves" : "throws-" + productError.GetType().Name);
            }

            if (bclError is RegexParseException && productError.GetType() == typeof(ArgumentException) &&
                productError is ArgumentException { ParamName: "pattern" } &&
                productError.Message.Contains("UTF-16 pattern offset", StringComparison.Ordinal)) return null;

            if (bclError.GetType() != productError.GetType())
            {
                return "construct:throw-type " + bclError.GetType().Name + " vs " + productError.GetType().Name;
            }

            return null;
        }

        if (bcl is Regex b && product is Utf8Regex p)
        {
            var expectedNames = b.GetGroupNames();
            var actualNames = p.GetGroupNames();
            if (!expectedNames.SequenceEqual(actualNames))
            {
                return "metadata:names bcl=[" + string.Join(",", expectedNames) + "]/product=[" + string.Join(",", actualNames) + "]";
            }

            var expectedNumbers = b.GetGroupNumbers();
            var actualNumbers = p.GetGroupNumbers();
            if (!expectedNumbers.SequenceEqual(actualNumbers))
            {
                return "metadata:numbers bcl=[" + string.Join(",", expectedNumbers) + "]/product=[" + string.Join(",", actualNumbers) + "]";
            }

            string? CheckInput(string input, string tag)
            {
                // These exported dot compounds distinguish scalar matching from
                // code-unit matching on both original astral subjects. The helper
                // keeps captures, bytes, derived ranges, consumers and cancellation.
                if (input.Any(char.IsSurrogate) && pattern is
                    "(?n:a){2}|(?:a.)" or "(?i:[ab]){2}|(?:a.)" or "(?m:\\d){2}|(?:a.)")
                {
                    Utf8AsciiScalarOperationAssertions.Operations(pattern, options, input);
                    return null;
                }
                var bytes = Encoding.UTF8.GetBytes(input);
                var expected = b.Match(input);
                var actual = p.Match(bytes);
                if (expected.Success != actual.Success)
                {
                    return tag + "input '" + input + "':bcl-" + expected.Success + "/product-" + actual.Success;
                }

                if (expected.Success && (expected.Index != actual.IndexInUtf16 || expected.Length != actual.LengthInUtf16))
                {
                    return tag + "input '" + input + "':range bcl=(" + expected.Index + "," + expected.Length + ")/product=(" + actual.IndexInUtf16 + "," + actual.LengthInUtf16 + ")";
                }

                if (expected.Success)
                {
                    var actualDetailed = p.MatchDetailed(bytes);
                    for (var g = 0; g < expectedNumbers.Length; g++)
                    {
                        var expectedGroup = expected.Groups[g];
                        var actualGroup = actualDetailed.GetGroup(g);
                        if (expectedGroup.Success != actualGroup.Success)
                        {
                            return tag + "group " + g + " success bcl=" + expectedGroup.Success + "/product=" + actualGroup.Success + " on input [" + input + "]";
                        }

                        if (expectedGroup.Success && (!string.Equals(expectedGroup.Value, actualGroup.GetValueString(), StringComparison.Ordinal) || expectedGroup.Index != actualGroup.IndexInUtf16 || expectedGroup.Length != actualGroup.LengthInUtf16))
                        {
                            return tag + "group " + g + " value bcl=[" + expectedGroup.Value + "]/product=[" + actualGroup.GetValueString() + "] on input [" + input + "]";
                        }
                    }
                }

                var expectedCount = b.Count(input);
                var actualCount = p.Count(bytes);
                if (expectedCount != actualCount)
                {
                    return tag + "input '" + input + "':count bcl=" + expectedCount + "/product=" + actualCount;
                }

                var expectedReplaced = b.Replace(input, "$0");
                var actualReplaced = Encoding.UTF8.GetString(p.Replace(bytes, "$0"));
                if (!string.Equals(expectedReplaced, actualReplaced, StringComparison.Ordinal))
                {
                    return tag + "input '" + input + "':replace bcl=[" + expectedReplaced + "]/product=[" + actualReplaced + "]";
                }

                return null;
            }

            foreach (var input in ProbeCorpus)
            {
                var witness = CheckInput(input, string.Empty);
                if (witness is not null)
                {
                    return witness;
                }
            }

            uint stream = 0x9E3779B9u;
            const string alphabet = "aAbB09 \n\r#()[]{}>.+*?|^$\\éyZ";
            for (var i = 0; i < 128; i++)
            {
                var chars = new char[i % 9];
                for (var k = 0; k < chars.Length; k++)
                {
                    stream ^= stream << 13;
                    stream ^= stream >> 17;
                    stream ^= stream << 5;
                    chars[k] = alphabet[(int)(stream % (uint)alphabet.Length)];
                }

                var witness = CheckInput(new string(chars), "fuzz#" + i + " ");
                if (witness is not null)
                {
                    return witness;
                }
            }

            foreach (var input in new[] { "\U0001F600", "a\U00010400b" })
            {
                var witness = CheckInput(input, "astral ");
                if (witness is not null)
                {
                    return witness;
                }
            }

            return null;
        }

        return "unreachable:null-reference-after-successful-construction";
    }
}
