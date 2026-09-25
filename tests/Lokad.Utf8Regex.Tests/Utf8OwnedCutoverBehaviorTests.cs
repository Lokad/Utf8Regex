using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Cutover-readiness differential (choke-point evidence): every residual shape the
// owned parser declines while the fork serves is compared behaviorally between the
// current product route and the BCL oracle over a fixed probe corpus. Each row
// either agrees (null witness: safe to flip to BCL fallback at the choke point)
// or reproduces its documented divergence below (non-null witness). A vanished
// witness or a new witness both fail, forcing re-triage before the flip.
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
    // census: 84 of 128).
    // - Forward-leak family (9): the fork folds trailing or wrapped content under a
    //   scoped option and marks trailing nodes, so the product over-matches shapes
    //   BCL scopes correctly. The flip moves these to BCL behavior (fewer matches).
    // - Fork-widening family (2): the fork serves shapes BCL rejects at construction
    //   (balance pop under explicit capture; invalid group name inside lookbehind).
    //   The flip turns serving into construction failure: blocked on an explicit
    //   compatibility decision, never flipped silently.
    // - Cross-scope numbering family (5): groups after an n-establishment lose their
    //   numbers in fork metadata while BCL numbers them. The flip changes the group
    //   APIs; match outcomes already agree. Includes the unscoped-outside case (?n:a)(b).
    // - Trailing-dot family (3): a leaked option on a trailing dot makes it match a
    //   newline (observed for s/i/n marks on input a-newline-b) while BCL dots do not.
    //   The flip removes those matches.
    // - Scoped-dot family (6): dots directly under scoped i/m/n match newlines in fork
    //   behavior while BCL dots do not (singleline dots agree). Same quirk, leading form.
    // - Numeric-lookbehind family (36): a numeric-named group after a lookbehind loses
    //   its number in fork metadata while BCL numbers it. The flip changes the group
    //   APIs; match outcomes already agree.
    // - Folding-compound family (12): folding or dot leaks across alternation branches
    //   and into compound tails change what matches. The flip moves these to BCL behavior.
    // - Loop-alternation family (3): an n-loop alternation with a dot or second scope
    //   in the other branch diverges on newline inputs. The flip moves it to BCL behavior.
    // - Phantom-name family (3): lookbehind phantom numbering leaks garbage names
    //   (=(?<, =a)b\], =(?<1) into the public group APIs while BCL reports just 0.
    //   The flip cleans the group APIs.
    private static readonly HashSet<(string Pattern, int Options)> CutoverDivergentShapes = new()
    {
        ("(?i:k)K", 512),
        ("(?i:ab)cd", 512),
        ("(?i:a)(?-i:b)", 512),
        ("(?i:a)b(?i:c)", 512),
        ("(?i:a)|b", 512),
        ("(?i:a)(?m:b)", 512),
        ("(?i:a)(?m:b)c", 512),
        ("(?i:a)(b(?m)c)", 512),
        ("(?i-m:a)b", 512),
        ("(?n:(?<-x>a))", 512),
        ("(?<=(?<1a>b))c", 512),
        ("(?n:(a))(b)", 512),
        ("(?n:(a))(b)", 528),
        ("(?n:(?<x>a))(b)", 512),
        ("(?n:(a)(?<y>b))(c)", 512),
        ("(?<=(?<-0>a)b)", 512),
        ("(?<=a)b\\>", 512),
        ("(?<=(?<1>a))b", 512),
        ("(?i:a)(b)", 512),
        ("(?n:a)(b)", 512),
        ("(?s:a).", 512),
        ("(?i:a).", 512),
        ("(?n:a).", 512),
        ("(?i:.){1,2}", 512),
        ("(?i:.){2}", 512),
        ("(?m:.){1,2}", 512),
        ("(?m:.){2}", 512),
        ("(?n:.){1,2}", 512),
        ("(?n:.){2}", 512),
        ("(?i:[ab]){2}(?m:[ab]){2}", 512),
        ("(?i:[ab]){2}([ab])", 512),
        ("(?i:[ab]){2}|([ab])", 512),
        ("(?i:[ab]){2}|a", 512),
        ("(?i:[ab]){2}|(?:a.)", 512),
        ("(?i:[ab]){2}|.{1,2}", 512),
        ("(?i:[ab]){2}(?n:.){1,2}", 512),
        ("(?i:[ab]){2}|(?n:.){1,2}", 512),
        ("(?i:[ab]){2}.{1,2}", 512),
        ("(?m:\\d){2}|.{1,2}", 512),
        ("(?m:\\d){2}|(?:a.)", 512),
        ("(?m:\\d){2}|(?n:.){1,2}", 512),
        ("(?n:a){2}([ab])", 512),
        ("(?n:a){2}(\\d){1,2}", 512),
        ("(?n:a){2}|([ab])", 512),
        ("(?n:a){2}|(\\d){1,2}", 512),
        ("(?n:a){2}|(?:a.)", 512),
        ("(?n:a){2}|.{1,2}", 512),
        ("(?n:a){2}|(?n:.){1,2}", 512),
        ("(?<=.)(?<1>.)+?", 512),
        ("(?<=.)(?<1>\\d){2}", 512),
        ("(?<=.)(?<1>^)+", 512),
        ("(?<=.)+?(?<1>.)+?", 512),
        ("(?<=.)+?(?<1>\\d){2}", 512),
        ("(?<=.)+?(?<1>^)+", 512),
        ("(?<=.)+?|(?<1>.)+?", 512),
        ("(?<=.)+?|(?<1>\\d){2}", 512),
        ("(?<=.)+?|(?<1>^)+", 512),
        ("(?<=.)|(?<1>.)+?", 512),
        ("(?<=.)|(?<1>\\d){2}", 512),
        ("(?<=.)|(?<1>^)+", 512),
        ("(?<=[ab]){1,2}(?<1>.)+?", 512),
        ("(?<=[ab]){1,2}(?<1>\\d){2}", 512),
        ("(?<=[ab]){1,2}(?<1>^)+", 512),
        ("(?<=[ab]){1,2}|(?<1>.)+?", 512),
        ("(?<=[ab]){1,2}|(?<1>\\d){2}", 512),
        ("(?<=[ab]){1,2}|(?<1>^)+", 512),
        ("(?<=\\d)?(?<1>.)+?", 512),
        ("(?<=\\d)?(?<1>\\d){2}", 512),
        ("(?<=\\d)?(?<1>^)+", 512),
        ("(?<=\\d)?|(?<1>.)+?", 512),
        ("(?<=\\d)?|(?<1>\\d){2}", 512),
        ("(?<=\\d)?|(?<1>^)+", 512),
        ("(?<=$)+(?<1>.)+?", 512),
        ("(?<=$)+(?<1>\\d){2}", 512),
        ("(?<=$)+(?<1>^)+", 512),
        ("(?<=$)+|(?<1>.)+?", 512),
        ("(?<=$)+|(?<1>\\d){2}", 512),
        ("(?<=$)+|(?<1>^)+", 512),
        ("(?<=a)*(?<1>.)+?", 512),
        ("(?<=a)*(?<1>\\d){2}", 512),
        ("(?<=a)*(?<1>^)+", 512),
        ("(?<=a)*|(?<1>.)+?", 512),
        ("(?<=a)*|(?<1>\\d){2}", 512),
        ("(?<=a)*|(?<1>^)+", 512),
    };

    [Theory]
    [MemberData(nameof(Utf8OwnedTreeParserLookaroundTests.LookaroundResidualCases), MemberType = typeof(Utf8OwnedTreeParserLookaroundTests))]
    public void LookaroundResidualCutoverDisposition(string pattern, int options)
    {
        AssertCutoverDisposition(pattern, (RegexOptions)options);
    }

    [Theory]
    [MemberData(nameof(Utf8OwnedTreeParserIgnoreCaseTests.IgnoreCaseResidualCases), MemberType = typeof(Utf8OwnedTreeParserIgnoreCaseTests))]
    public void IgnoreCaseResidualCutoverDisposition(string pattern, int options)
    {
        AssertCutoverDisposition(pattern, (RegexOptions)options);
    }

    [Theory]
    [MemberData(nameof(Utf8OwnedTreeParserExplicitTests.ExplicitResidualCases), MemberType = typeof(Utf8OwnedTreeParserExplicitTests))]
    public void ExplicitResidualCutoverDisposition(string pattern, int options)
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
    public void ShorthandResidualCutoverDisposition(string pattern)
    {
        AssertCutoverDisposition(pattern, RegexOptions.CultureInvariant);
    }

    [Theory]
    [MemberData(nameof(Utf8OwnedTreeParserSubtractionTests.SubtractionResidualCases), MemberType = typeof(Utf8OwnedTreeParserSubtractionTests))]
    public void SubtractionResidualCutoverDisposition(string pattern)
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

            foreach (var input in ProbeCorpus)
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var expected = b.Match(input);
                var actual = p.Match(bytes);
                if (expected.Success != actual.Success)
                {
                    return "input '" + input + "':bcl-" + expected.Success + "/product-" + actual.Success;
                }

                if (expected.Success && (expected.Index != actual.IndexInUtf16 || expected.Length != actual.LengthInUtf16))
                {
                    return "input '" + input + "':range bcl=(" + expected.Index + "," + expected.Length + ")/product=(" + actual.IndexInUtf16 + "," + actual.LengthInUtf16 + ")";
                }

                var expectedCount = b.Count(input);
                var actualCount = p.Count(bytes);
                if (expectedCount != actualCount)
                {
                    return "input '" + input + "':count bcl=" + expectedCount + "/product=" + actualCount;
                }
            }

            return null;
        }

        return "unreachable:null-reference-after-successful-construction";
    }
}