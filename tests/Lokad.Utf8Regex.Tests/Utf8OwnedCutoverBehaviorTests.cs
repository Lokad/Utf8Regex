using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Owned-cutover differential: every remaining residual shape is compared
// behaviorally between the current product route and the BCL oracle over a fixed
// probe corpus plus a deterministic seeded fuzz stream (128 xorshift inputs over
// a metachar-heavy alphabet) and two astral inputs. Witness depth covers
// construction verdicts, group metadata, first-match positions, detailed group
// values and positions, counts, and whole-match replacement. Each row either
// agrees (null witness) or reproduces its documented divergence below (non-null
// witness). A vanished witness or a new witness both fail, forcing re-triage.
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

    // Machine-derived divergent rows (11 from the first run, 7 from the metadata
    // and newline extension, 5 from the leak-family sweep, 61 from the residual
    // census: 84 of 128; 77 of 121 after the 2026-09-26 leading-scope graduation
    // (40 clean rows serve BCL-exactly, 22 rejoin below under owned serving);
    // 5 of 43 after the 2026-09-26 numeric-lookbehind graduation (36 numeric plus
    // 2 phantom rows serve BCL-exactly with null witnesses, entries removed);
    // 3 of 43 after the 2026-09-26 parser flip (2 widening rows become BCL-exact
    // construction failures, entries removed).
    // - Forward-leak family (9): the fork folds trailing or wrapped content under a
    //   scoped option and marks trailing nodes, so the product over-matches shapes
    //   BCL scopes correctly. The flip moves these to BCL behavior (fewer matches).
    // - Fork-widening family (2, corrected at the 2026-09-26 flip): the fork served
    //   shapes BCL rejects at construction (balance pop under explicit capture;
    //   invalid group name inside lookbehind). Construction now throws BCL-exactly;
    //   Utf8CompileFailureDiffTests pins the errors and the parser batteries pin
    //   owned-decline plus the BCL verdicts.
    // - Cross-scope numbering family (5): groups after an n-establishment lose their
    //   numbers in fork metadata while BCL numbers them. The flip changes the group
    //   APIs; match outcomes already agree. Includes the unscoped-outside case (?n:a)(b).
    // - Trailing-dot family (3): a leaked option on a trailing dot makes it match a
    //   newline (observed for s/i/n marks on input a-newline-b) while BCL dots do not.
    //   The flip removes those matches.
    // - Scoped-dot family (6): dots directly under scoped i/m/n match newlines in fork
    //   behavior while BCL dots do not (singleline dots agree). Same quirk, leading form.
    // - Numeric-lookbehind family (36, graduated 2026-09-26): numeric-named groups
    //   after a lookbehind lost their numbers in fork metadata (phantom names) while
    //   BCL numbers them. The owned parser serves these BCL-exactly (metadata, values,
    //   counts, replacement over corpus plus fuzz plus astral all agree), so the flip
    //   changes nothing for them; the rows left the residual set with the entries.
    // - Folding-compound family (12): folding or dot leaks across alternation branches
    //   and into compound tails change what matches. The flip moves these to BCL behavior.
    // - Loop-alternation family (3): an n-loop alternation with a dot or second scope
    //   in the other branch diverges on newline inputs. The flip moves it to BCL behavior.
    // - Phantom-name family (graduated 2026-09-26 with the numeric family): lookbehind
    //   phantom numbering leaked garbage fork names into the public group APIs while BCL
    //   reports clean numbers. Owned serving is BCL-exact, so the flip changes nothing
    //   for them either.
    private static readonly HashSet<(string Pattern, int Options)> CutoverDivergentShapes = new()
    {
        ("(?i:[ab]){2}|(?:a.)", 512),
        ("(?m:\\d){2}|(?:a.)", 512),
        ("(?n:a){2}|(?:a.)", 512),
        // Owned-serving divergences (parser gate relaxation): these shapes parse
        // BCL-exactly but the lowered execution still leaks scope options onto trailing
        // content (case folding past i-scopes, Singleline past s-scopes, quantified dots).
        // They reproduce witnesses under owned serving exactly where the fork used to
        // diverge; the downstream scoping fix graduates them like the clean rows.
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
        if (CutoverDivergentShapes.Contains((pattern, (int)options)))
        {
            Assert.NotNull(witness);
        }
        else
        {
            Assert.Null(witness);
        }
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
