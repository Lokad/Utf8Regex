using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R00 route-coverage matrix (core profile): for each feature row, pins the construction
/// route (ExecutionKind plus FallbackReason) and the IsMatch/Count BCL-verifier footprint
/// on a representative input. Row names carry feature, replacement owner, and covering tests.
/// Owners: R03 core engine; R04 adapters (adapter-profile matrices follow in their own test
/// projects). BCL dependence of Match/Replace/Split/Enumerate per row is pinned at file level
/// by EngineDependencyInventoryTests and behaviorally by DiffTests until R03 lands
/// per-operation owned execution with dedicated counters.
/// Nonzero verifier invocations can include owned native confirmation; BCL execution
/// must be assessed with the fallback construction flags as well.
/// The counter records verification attempts; whether BCL executed follows the construction flags (a created anchored fallback witnesses execution: its Lazy is only evaluated inside TryVerify paths).
/// </summary>
public sealed class EngineRouteCoverageMatrixTests
{
    public static IEnumerable<object?[]> RouteMatrix()
    {
        // Literals and alternations: owned exact-literal families (owner R03). Cover: ExactUtf8LiteralIsMatchTests, DotNetPerformanceRouteGuardrailTests.
        yield return new object?[] { "exact-ascii-literal", "abc", RegexOptions.CultureInvariant, "xxabcxx", "ExactAsciiLiteral", null, true, true };
        yield return new object?[] { "exact-unicode-ru", "Шерлок", RegexOptions.None, "prefix Шерлок suffix", "ExactUtf8Literal", null, true, true };
        yield return new object?[] { "exact-unicode-zh", "夏洛克", RegexOptions.None, "prefix夏洛克suffix", "ExactUtf8Literal", null, true, true };
        yield return new object?[] { "literal-alternation-en", "Sherlock Holmes|John Watson|Irene Adler", RegexOptions.None, "John Watson esq", "ExactUtf8Literals", null, true, true };
        // Case-pair classes expand to a native family without IgnoreCase (folding fix): must stay native and verifier-free, never silently fall back. Cover: Utf8EquivalentSpellingDiffTests.
        yield return new object?[] { "case-pair-class-family", "[Aa][Bb][Cc]", RegexOptions.CultureInvariant, "xxABCxx", "ExactUtf8Literals", null, true, true };
        yield return new object?[] { "single-upper-class-literal", "[A]", RegexOptions.CultureInvariant, "A", "ExactAsciiLiteral", null, true, true };
        yield return new object?[] { "case-pair-class-ignorecase", "[Aa][Bb][Cc]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxABCxx", "AsciiLiteralIgnoreCase", null, true, true };
        // Structural and multi-branch ignorecase families: native routes with per-op BCL verifier footprints pinned honestly (structural candidates verify on hits).
        yield return new object?[] { "structural-identifier-family", "\\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\\b", RegexOptions.CultureInvariant, "logger.LogError(\"bad\");", "AsciiStructuralIdentifierFamily", null, false, false };
        // Greedy Count shares the owned window verifier rather than the old trailing-anchor shortcut.
        yield return new object?[] { "structural-ordered-window", "Holmes.{0,25}Watson|Watson.{0,25}Holmes", RegexOptions.None, "Holmes and Watson", "AsciiOrderedLiteralWindow", null, true, false };
        yield return new object?[] { "structural-token-window", "[A-Za-z]{10}\\s+[\\s\\S]{0,100}Result[\\s\\S]{0,100}\\s+[A-Za-z]{10}", RegexOptions.None, "abcdefghij Result klmnopqrst", "AsciiStructuralTokenWindow", null, false, false };
        yield return new object?[] { "ignorecase-literals-abd", "a[bc]d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxabdxx", "AsciiLiteralIgnoreCaseLiterals", null, true, true };
        yield return new object?[] { "structural-repeated-segment", "(?:[A-Z][a-z]+\\s*){10,100}", RegexOptions.CultureInvariant, "Alpha Beta Gamma Delta Epsilon Zeta Eta Theta Iota Kappa ", "AsciiStructuralRepeatedSegment", null, false, false };
        yield return new object?[] { "structural-quoted-relation-aws", "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+", RegexOptions.Multiline, "\"AIDAABCDEFGHIJKLMNOP\"\nctx = 1\nctx = 2\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n\n\"AIDAABCDEFGHIJKLMNOP\"\nctx = 3\nctx = 4\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n", "AsciiStructuralQuotedRelation", null, false, false };
        // Unbounded class/word loops: BCL fallback with unsupported_loop (owner R03). digit-range stays AsciiSimplePattern but BCL-verifies both ops on hit input (Unicode digit semantics). Cover: Utf8RegexConstructionTests fallback-reason cases.
        yield return new object?[] { "char-class-plus", "[a-z]+", RegexOptions.CultureInvariant, "xxabcxx", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "negated-class", "[^0-9]+", RegexOptions.CultureInvariant, "xxabcxx", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "bounded-repeat", "[A-Za-z]{8,13}", RegexOptions.None, "xxabcdefghxx", "AsciiSimplePattern", null, true, true };
        yield return new object?[] { "digit-range", "\\d{3}-\\d{4}", RegexOptions.CultureInvariant, "call 555-1234 now", "AsciiSimplePattern", null, false, false };
        // Anchors and boundaries over literal/ASCII patterns (owner R03).
        yield return new object?[] { "start-anchor", "^abc", RegexOptions.CultureInvariant, "abcdef", "AsciiSimplePattern", null, true, true };
        yield return new object?[] { "end-anchor", "abc$", RegexOptions.CultureInvariant, "xxabc", "AsciiSimplePattern", null, true, true };
        yield return new object?[] { "word-boundary", "\\bword\\b", RegexOptions.CultureInvariant, "a word here", "ExactAsciiLiteral", null, true, true };
        // R05: single-sided line anchors ride the owned interpreter, whose search
        // candidates and verifier invocations are counted with no BCL construction
        // (see the line-anchored discovery battery in Utf8AnchoredLiteralFamilyTests).
        yield return new object?[] { "multiline-anchor", "^abc", RegexOptions.Multiline | RegexOptions.CultureInvariant, "xx\nabc", "AsciiSimplePattern", null, false, false };
        // Assertions and backreferences: BCL fallback construction with specific reasons, except single lookahead-pos which rides the literal family; slice-served assertions run IsMatch/Count verifier-free (owner R03).
        yield return new object?[] { "lookahead-pos", "a(?=b)", RegexOptions.CultureInvariant, "ab", "ExactAsciiLiteral", null, true, true };
        yield return new object?[] { "lookahead-neg", "a(?!b)", RegexOptions.CultureInvariant, "ac", "FallbackRegex", "unsupported_lookaround", true, true };
        yield return new object?[] { "lookbehind-pos", "(?<=a)b", RegexOptions.CultureInvariant, "ab", "FallbackRegex", "unsupported_lookaround", true, true };
        yield return new object?[] { "lookbehind-neg", "(?<!a)b", RegexOptions.CultureInvariant, "cb", "FallbackRegex", "unsupported_lookaround", true, true };
        yield return new object?[] { "backreference", "(a)\\1", RegexOptions.CultureInvariant, "aa", "FallbackRegex", "unsupported_backreference", true, true };
        yield return new object?[] { "named-backreference", "(?<w>a)\\k<w>", RegexOptions.CultureInvariant, "aa", "FallbackRegex", "unsupported_backreference", true, true };
        // Captures: literal-embedded captures stay native; quantified or word captures fall back (owner R03).
        yield return new object?[] { "repeated-capture", "(a)+", RegexOptions.CultureInvariant, "aaa", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "named-capture", "(?<word>\\w+)", RegexOptions.CultureInvariant, "hello", "FallbackRegex", "unsupported_loop", true, true };
        // Options and modes: RightToLeft literals stay native; culture-sensitive and non-default modes fall back (owner R03; adapters own their models in R04).
        // R03 dot slice: singleline-dot keeps its FallbackRegex construction kind but IsMatch/Count run verifier-free on the owned slice (ASCII input serves).
        yield return new object?[] { "right-to-left", "abc", RegexOptions.RightToLeft | RegexOptions.CultureInvariant, "xxabcxx", "ExactAsciiLiteral", null, true, true };
        yield return new object?[] { "ignorecase-invariant", "abc", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxABCxx", "AsciiLiteralIgnoreCase", null, true, true };
        yield return new object?[] { "ignorecase-culture", "abc", RegexOptions.IgnoreCase, "xxABCxx", "FallbackRegex", "culture_sensitive_ignore_case", true, true };
        yield return new object?[] { "explicit-capture", "(a)(b)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, "xxabxx", "ExactAsciiLiteral", null, true, true };
        yield return new object?[] { "whitespace-mode", "a b c", RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant, "abc", "FallbackRegex", "unsupported_pattern", true, true };
        yield return new object?[] { "singleline-dot", "a.*b", RegexOptions.Singleline | RegexOptions.CultureInvariant, "a\nb", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "ecmascript", "abc", RegexOptions.ECMAScript, "xxabcxx", "FallbackRegex", "unsupported_options", true, true };
        // NonBacktracking literals have owned construction admission and native
        // operations; general symbolic-loop execution remains migration debt.
        yield return new object?[] { "nonbacktracking", "abc", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, "xxabcxx", "ExactAsciiLiteral", null, true, true };
        // Empty-match and astral edge cases (owner R03).
        yield return new object?[] { "empty-star", "a*", RegexOptions.CultureInvariant, "bbb", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "astral-literal", "\U0001F600", RegexOptions.CultureInvariant, "\U0001F600", "ExactUtf8Literal", null, true, true };
        // Conditionals and balancing: BCL fallback with unsupported_conditional (owner R03).
        yield return new object?[] { "conditional", "(a)?(?(1)ab|cd)", RegexOptions.CultureInvariant, "aab", "FallbackRegex", "unsupported_conditional", true, true };
        yield return new object?[] { "balancing", "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))", RegexOptions.CultureInvariant, "BEGINEND", "FallbackRegex", "unsupported_conditional", true, true };
        // Canonical word intersections qualify ASCII token execution; Unicode
        // subjects retain owned scalar execution (owner R03).
        yield return new object?[] { "word-email-shape", "\\w+@\\w+\\.\\w+", RegexOptions.CultureInvariant, "mail me a@b.cd ok", "FallbackRegex", null, true, true };
        yield return new object?[] { "compiled-literal", "abc", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xxabcxx", "ExactAsciiLiteral", null, true, true };
        yield return new object?[] { "negated-shorthand-window-gap-miss", "a\\D{2,5}b", RegexOptions.CultureInvariant, "xa12b", "AsciiSimplePattern", null, false, false };
        yield return new object?[] { "negated-shorthand-window-gap-hit", "a\\D{2,5}b", RegexOptions.CultureInvariant, "xayzb", "AsciiSimplePattern", null, false, false };
        yield return new object?[] { "negated-space-window-gap-hit", "a[^\\s]{2,5}b", RegexOptions.CultureInvariant, "xayzb", "AsciiSimplePattern", null, false, false };
        yield return new object?[] { "divergent-ranged-count", "(?:a|ab){2,3}", RegexOptions.CultureInvariant, "xaabab", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "folded-shorthand-range-astral", "(?i)A\\D{2,}?B", RegexOptions.CultureInvariant, "xa\U0001F600b", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "negated-property-range-astral", "a\\P{L}{2,3}b", RegexOptions.CultureInvariant, "xa\U0001F600b", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "negated-shorthand-plus-astral", "a\\P{Nd}+b", RegexOptions.CultureInvariant, "xa\U0001F600b", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "word-range-astral", "a\\W{2,}?b", RegexOptions.CultureInvariant, "xa\U0001F600b", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "negated-shorthand-window-gap-miss-compiled", "a\\D{2,5}b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xa12b", "AsciiSimplePattern", null, false, false };
        yield return new object?[] { "negated-shorthand-window-gap-hit-compiled", "a\\D{2,5}b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xayzb", "AsciiSimplePattern", null, false, false };
        yield return new object?[] { "negated-space-window-gap-hit-compiled", "a[^\\s]{2,5}b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xayzb", "AsciiSimplePattern", null, false, false };
        yield return new object?[] { "divergent-ranged-count-compiled", "(?:a|ab){2,3}", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xaabab", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "negated-property-range-astral-compiled", "a\\P{L}{2,3}b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xa\U0001F600b", "FallbackRegex", "unsupported_loop", true, true };
        yield return new object?[] { "word-range-astral-compiled", "a\\W{2,}?b", RegexOptions.Compiled | RegexOptions.CultureInvariant, "xa\U0001F600b", "FallbackRegex", "unsupported_loop", true, true };
    }

    [Theory]
    [MemberData(nameof(RouteMatrix))]
    public void MatrixRowPinsRouteAndVerifierFootprint(
        string name,
        string pattern,
        RegexOptions options,
        string inputText,
        string expectedKindName,
        string? expectedFallbackReason,
        bool expectIsMatchVerifierFree,
        bool expectCountVerifierFree)
    {
        Assert.NotEmpty(name);
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expectedKindName, regex.Inspection.ExecutionKind.ToString());
        Assert.Equal(expectedFallbackReason, regex.Inspection.FallbackReason);
        Assert.Equal(expectIsMatchVerifierFree, regex.CollectIsMatchDiagnostics(input).VerifierInvocations == 0);
        Assert.Equal(expectCountVerifierFree, regex.CollectCountDiagnostics(input).VerifierInvocations == 0);
        if (name == "structural-ordered-window")
        {
            Assert.Equal(Regex.Count(inputText, pattern, options), regex.Count(input));
            Assert.Equal((0, inputText.Length), (regex.Match(input).IndexInBytes, regex.Match(input).LengthInBytes));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
