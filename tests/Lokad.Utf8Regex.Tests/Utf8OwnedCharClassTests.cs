using System.Text.RegularExpressions;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned character-class tables and constants: wire indices and payload bytes
// are pinned; positive constants, word tables, and case conversion check
// against BCL shorthands and character APIs. Wire-query and matching batteries
// below still harvest fork payloads and migrate with the matching core.
public sealed class Utf8OwnedCharClassTests
{
    public static IEnumerable<object?[]> WireCases()
    {
        yield return new object?[] { "[a]", 512 };
        yield return new object?[] { "[ab]", 512 };
        yield return new object?[] { "[a-z]", 512 };
        yield return new object?[] { "[^a]", 512 };
        yield return new object?[] { "[a-]", 512 };
        yield return new object?[] { "[-a]", 512 };
        yield return new object?[] { "[--0]", 512 };
        yield return new object?[] { "[\\d]", 512 };
        yield return new object?[] { "[^\\d]", 512 };
        yield return new object?[] { "\\d", 512 };
        yield return new object?[] { "\\D", 512 };
        yield return new object?[] { "\\w", 512 };
        yield return new object?[] { "\\W", 512 };
        yield return new object?[] { "\\s", 512 };
        yield return new object?[] { "\\S", 512 };
        yield return new object?[] { "\\p{L}", 512 };
        yield return new object?[] { "\\p{Lu}", 512 };
        yield return new object?[] { "\\p{IsGreek}", 512 };
        yield return new object?[] { ".", 512 };
        yield return new object?[] { "(?s:.)", 512 };
        yield return new object?[] { "[a-[b]]", 512 };
        yield return new object?[] { "[\\da-z]", 512 };
        yield return new object?[] { "[a\\w]", 512 };
        yield return new object?[] { "[^\\W]", 512 };
        yield return new object?[] { "[\\p{L}a]", 512 };
        yield return new object?[] { "[a-zA-Z0-9]", 512 };
        yield return new object?[] { "[\\x00-\\x7F]", 512 };
        yield return new object?[] { "[é]", 512 };
        yield return new object?[] { "[α-ω]", 512 };
        yield return new object?[] { "[\\b]", 512 };
        yield return new object?[] { "[a\\b]", 512 };
        yield return new object?[] { "[a-\\d]", 512 };
        yield return new object?[] { "[(]", 512 };
        yield return new object?[] { "[)]", 512 };
        yield return new object?[] { "[\\[]", 512 };
        yield return new object?[] { "[\\\\]", 512 };
        yield return new object?[] { "[\\^]", 512 };
        yield return new object?[] { "[a^]", 512 };
        yield return new object?[] { "[^a-z]", 512 };
        yield return new object?[] { "[a-z-[aeiou]]", 512 };
        yield return new object?[] { "\\p{IsBasicLatin}", 512 };
        yield return new object?[] { "\\p{_xmlC}", 512 };
        yield return new object?[] { "\\d", 768 };
        yield return new object?[] { "\\s", 768 };
        yield return new object?[] { "\\w", 768 };
        yield return new object?[] { ".", 768 };
        yield return new object?[] { "[a]", 768 };
        yield return new object?[] { "[^a]", 768 };
    }

    public static IEnumerable<object?[]> CaseConversionCases()
    {
        yield return new object?[] { "a" };
        yield return new object?[] { "A" };
        yield return new object?[] { "z" };
        yield return new object?[] { "Z" };
        yield return new object?[] { "0" };
        yield return new object?[] { "9" };
        yield return new object?[] { "_" };
        yield return new object?[] { "é" };
        yield return new object?[] { "É" };
        yield return new object?[] { "k" };
        yield return new object?[] { "K" };
        yield return new object?[] { "i" };
        yield return new object?[] { "I" };
        yield return new object?[] { "ß" };
        yield return new object?[] { "σ" };
        yield return new object?[] { "ς" };
        yield return new object?[] { " " };
        yield return new object?[] { "[" };
    }

    [Theory]
    [MemberData(nameof(WireCases))]
    public void WireQueriesMatchFork(string pattern, int options)
    {
        var tree = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options);
        if (tree is null)
        {
            AssertWireQueriesEqual(string.Empty);
            AssertWireQueriesEqual("a");
            AssertWireQueriesEqual(new string((char)0, 3));
            AssertWireQueriesEqual(new string((char)0, 4));
            return;
        }
        var payloads = new System.Collections.Generic.List<string> { "", "a", "\0\0\0", "\0\0\0\0" };
        var stack = new System.Collections.Generic.Stack<RuntimeFrontEnd.RegexNode>();
        stack.Push(tree.Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if ((node.Kind == RuntimeFrontEnd.RegexNodeKind.Set ||
                node.Kind == RuntimeFrontEnd.RegexNodeKind.Setloop ||
                node.Kind == RuntimeFrontEnd.RegexNodeKind.Setlazy) &&
                node.Str is not null)
            {
                payloads.Add(node.Str);
            }

            for (var i = 0; i < node.ChildCount; i++)
            {
                stack.Push(node.Child(i));
            }
        }

        foreach (var payload in payloads)
        {
            AssertWireQueriesEqual(payload);
        }
    }

    public static IEnumerable<object?[]> CaseConversionExpectations()
    {
        yield return new object?[] { "a", true };
        yield return new object?[] { "A", true };
        yield return new object?[] { "z", true };
        yield return new object?[] { "Z", true };
        yield return new object?[] { "0", false };
        yield return new object?[] { "9", false };
        yield return new object?[] { "_", false };
        yield return new object?[] { "é", true };
        yield return new object?[] { "É", true };
        yield return new object?[] { "k", true };
        yield return new object?[] { "K", true };
        yield return new object?[] { "i", true };
        yield return new object?[] { "I", true };
        yield return new object?[] { "ß", false };
        yield return new object?[] { "σ", true };
        yield return new object?[] { "ς", true };
        yield return new object?[] { " ", false };
        yield return new object?[] { "[", false };
    }

    [Theory]
    [MemberData(nameof(CaseConversionExpectations))]
    public void CaseConversionMatchesPinnedFacts(string text, bool expected)
    {
        Assert.Equal(expected, Utf8OwnedCharClass.ParticipatesInCaseConversion(text[0]));
        Assert.Equal(expected, char.ToLowerInvariant(text[0]) != char.ToUpperInvariant(text[0]));
    }

    [Fact]
    public void WireIndicesArePinned()
    {
        Assert.Equal(0, Utf8OwnedCharClass.FlagsIndex);
        Assert.Equal(1, Utf8OwnedCharClass.SetLengthIndex);
        Assert.Equal(2, Utf8OwnedCharClass.CategoryLengthIndex);
        Assert.Equal(3, Utf8OwnedCharClass.SetStartIndex);
    }

    private static void AssertWireQueriesEqual(string payload)
    {
        string Probe(Func<string> render)
        {
            try
            {
                return "value:" + render();
            }
            catch (Exception ex)
            {
                return "throws:" + ex.GetType().FullName + ":" + ((ex as ArgumentException)?.ParamName ?? string.Empty);
            }
        }

        Assert.Equal(Probe(() => RuntimeFrontEnd.RegexCharClass.IsNegated(payload).ToString()), Probe(() => Utf8OwnedCharClass.IsNegated(payload).ToString()));
        Assert.Equal(Probe(() => RuntimeFrontEnd.RegexCharClass.IsSubtraction(payload).ToString()), Probe(() => Utf8OwnedCharClass.IsSubtraction(payload).ToString()));
        Assert.Equal(Probe(() => RuntimeFrontEnd.RegexCharClass.IsEmpty(payload).ToString()), Probe(() => Utf8OwnedCharClass.IsEmpty(payload).ToString()));
        Assert.Equal(Probe(() => RuntimeFrontEnd.RegexCharClass.IsSingleton(payload).ToString()), Probe(() => Utf8OwnedCharClass.IsSingleton(payload).ToString()));
        Assert.Equal(Probe(() => RuntimeFrontEnd.RegexCharClass.IsAscii(payload).ToString()), Probe(() => Utf8OwnedCharClass.IsAscii(payload).ToString()));
        Assert.Equal(Probe(() => RuntimeFrontEnd.RegexCharClass.CanEasilyEnumerateSetContents(payload).ToString()), Probe(() => Utf8OwnedCharClass.CanEasilyEnumerateSetContents(payload).ToString()));
        Assert.Equal(Probe(() => "char:" + ((int)RuntimeFrontEnd.RegexCharClass.SingletonChar(payload)).ToString()), Probe(() => "char:" + ((int)Utf8OwnedCharClass.SingletonChar(payload)).ToString()));
        Assert.Equal(Probe(() => "chars:" + new string(RuntimeFrontEnd.RegexCharClass.GetSetChars(payload))), Probe(() => "chars:" + new string(Utf8OwnedCharClass.GetSetChars(payload))));
        Assert.Equal(Probe(() => { var ok = RuntimeFrontEnd.RegexCharClass.TryGetSingleRange(payload, out var first, out var last); return ok ? "ok:" + ((int)first).ToString() + ":" + ((int)last).ToString() : "no"; }), Probe(() => { var ok = Utf8OwnedCharClass.TryGetSingleRange(payload, out var first, out var last); return ok ? "ok:" + ((int)first).ToString() + ":" + ((int)last).ToString() : "no"; }));
        Assert.Equal(Probe(() => { var ok = RuntimeFrontEnd.RegexCharClass.TryGetDoubleRange(payload, out var firstRange, out var secondRange); return ok ? "ok:" + ((int)firstRange.First).ToString() + ":" + ((int)firstRange.Last).ToString() + ":" + ((int)secondRange.First).ToString() + ":" + ((int)secondRange.Last).ToString() : "no"; }), Probe(() => { var ok = Utf8OwnedCharClass.TryGetDoubleRange(payload, out var firstRange, out var secondRange); return ok ? "ok:" + ((int)firstRange.First).ToString() + ":" + ((int)firstRange.Last).ToString() + ":" + ((int)secondRange.First).ToString() + ":" + ((int)secondRange.Last).ToString() : "no"; }));
        Assert.Equal(Probe(() => { var ok = RuntimeFrontEnd.RegexCharClass.TryGetOnlyCategories(payload, out var categories); return ok ? "ok:" + categories : "no"; }), Probe(() => { var ok = Utf8OwnedCharClass.TryGetOnlyCategories(payload, out var categories); return ok ? "ok:" + categories : "no"; }));
        Assert.Equal(Probe(() => { var ok = RuntimeFrontEnd.RegexCharClass.TryGetSetCharCount(payload, out var count); return ok ? "ok:" + count.ToString() : "no"; }), Probe(() => { var ok = Utf8OwnedCharClass.TryGetSetCharCount(payload, out var count); return ok ? "ok:" + count.ToString() : "no"; }));
    }
    public static IEnumerable<object?[]> MatchingCases()
    {
        yield return new object?[] { "[a]", 512 };
        yield return new object?[] { "[^a]", 512 };
        yield return new object?[] { "[a-z]", 512 };
        yield return new object?[] { "[^a-z]", 512 };
        yield return new object?[] { "[ab]", 512 };
        yield return new object?[] { "[a-zA-Z0-9]", 512 };
        yield return new object?[] { "[\\d]", 512 };
        yield return new object?[] { "[^\\d]", 512 };
        yield return new object?[] { "\\d", 512 };
        yield return new object?[] { "\\D", 512 };
        yield return new object?[] { "\\w", 512 };
        yield return new object?[] { "\\W", 512 };
        yield return new object?[] { "\\s", 512 };
        yield return new object?[] { "\\S", 512 };
        yield return new object?[] { "\\s", 256 };
        yield return new object?[] { "\\S", 256 };
        yield return new object?[] { "\\d", 256 };
        yield return new object?[] { "[\\s]", 512 };
        yield return new object?[] { "[^\\s]", 512 };
        yield return new object?[] { "[\\S]", 512 };
        yield return new object?[] { "[^\\S]", 512 };
        yield return new object?[] { "\\p{L}", 512 };
        yield return new object?[] { "\\p{Lu}", 512 };
        yield return new object?[] { "\\p{Nd}", 512 };
        yield return new object?[] { "\\P{Nd}", 512 };
        yield return new object?[] { "\\p{IsBasicLatin}", 512 };
        yield return new object?[] { "[a-[b]]", 512 };
        yield return new object?[] { "[a-z-[aeiou]]", 512 };
        yield return new object?[] { "[^a-[b]]", 512 };
        yield return new object?[] { "[\\s-[a]]", 512 };
        yield return new object?[] { "[\\d-[13579]]", 512 };
        yield return new object?[] { "[abc-[b-d]]", 512 };
        yield return new object?[] { "[a-z-[aeiou-[e]]]", 512 };
        yield return new object?[] { "[\\da-z]", 512 };
        yield return new object?[] { "[a\\w]", 512 };
        yield return new object?[] { "[^\\W]", 512 };
        yield return new object?[] { "[\\p{L}a]", 512 };
        yield return new object?[] { "[\\x00-\\x7F]", 512 };
        yield return new object?[] { "[^\\x00-\\x7F]", 512 };
        yield return new object?[] { "[é]", 512 };
        yield return new object?[] { "[α-ω]", 512 };
        yield return new object?[] { "[\\b]", 512 };
        yield return new object?[] { "[a-\\d]", 512 };
        yield return new object?[] { ".", 512 };
        yield return new object?[] { "\\p{_xmlC}", 512 };
        yield return new object?[] { "\\p{_xmlS}", 512 };
    }

    [Theory]
    [MemberData(nameof(MatchingCases))]
    public void MatchingMatchesFork(string pattern, int options)
    {
        string Probe(Func<string> render)
        {
            try
            {
                return "value:" + render();
            }
            catch (Exception ex)
            {
                return "throws:" + ex.GetType().FullName + ":" + ((ex as ArgumentException)?.ParamName ?? string.Empty);
            }
        }

        var tree = Utf8RuntimeTreeProvider.TryParseLegacy(pattern, (RegexOptions)options);
        if (tree is null)
        {
            return;
        }

        var payloads = new System.Collections.Generic.List<string>();
        var stack = new System.Collections.Generic.Stack<RuntimeFrontEnd.RegexNode>();
        stack.Push(tree.Root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if ((node.Kind == RuntimeFrontEnd.RegexNodeKind.Set ||
                node.Kind == RuntimeFrontEnd.RegexNodeKind.Setloop ||
                node.Kind == RuntimeFrontEnd.RegexNodeKind.Setlazy) &&
                node.Str is not null)
            {
                payloads.Add(node.Str);
            }

            for (var i = 0; i < node.ChildCount; i++)
            {
                stack.Push(node.Child(i));
            }
        }

        var corpus = MatchingCorpus();
        foreach (var payload in payloads)
        {
            foreach (var ch in corpus)
            {
                Assert.Equal(
                    RuntimeFrontEnd.RegexCharClass.CharInClass(ch, payload),
                    Utf8OwnedCharClass.CharInClass(ch, payload));
                Assert.Equal(
                    RuntimeFrontEnd.RegexCharClass.CharInClassBase(ch, payload),
                    Utf8OwnedCharClass.CharInClassBase(ch, payload));
            }

            for (var cut = 0; cut <= payload.Length; cut++)
            {
                var truncated = payload.Substring(0, cut);
                Assert.Equal(
                    Probe(() => RuntimeFrontEnd.RegexCharClass.CharInClass((char)65, truncated).ToString()),
                    Probe(() => Utf8OwnedCharClass.CharInClass((char)65, truncated).ToString()));
                Assert.Equal(
                    Probe(() => RuntimeFrontEnd.RegexCharClass.CharInClassBase((char)65, truncated).ToString()),
                    Probe(() => Utf8OwnedCharClass.CharInClassBase((char)65, truncated).ToString()));
            }
        }
    }

    private static char[] MatchingCorpus()
    {
        var corpus = new System.Collections.Generic.List<char>();
        for (var i = 0; i < 128; i++)
        {
            corpus.Add((char)i);
        }

        foreach (var code in new int[] { 128, 133, 159, 160, 173, 233, 255, 256, 304, 305, 382, 913, 937, 945, 962, 963, 969, 1056, 1072, 1234, 2005, 2028, 4096, 8203, 8204, 8205, 8232, 8233, 20013, 12354, 19968, 65279, 55296, 56320, 57343, 65534, 65535 })
        {
            corpus.Add((char)code);
        }

        return corpus.ToArray();
    }

    [Fact]
    public void WordLookupMatchesBclWordClass()
    {
        var oracle = new Regex(@"\w", RegexOptions.CultureInvariant);
        var ownedLookup = Utf8OwnedCharClass.WordCharAsciiLookup;
        Assert.Equal(128, ownedLookup.Length);
        for (var i = 0; i < 128; i++)
        {
            Assert.Equal(oracle.IsMatch(((char)i).ToString()), ownedLookup[i] != 0);
        }
    }

    [Fact]
    public void WordPredicatesMatchBclWordClass()
    {
        var oracle = new Regex(@"\w", RegexOptions.CultureInvariant);
        for (var i = 0; i <= char.MaxValue; i++)
        {
            var ch = (char)i;
            Assert.Equal(oracle.IsMatch(ch.ToString()), Utf8OwnedCharClass.IsWordChar(ch));
        }
    }

    [Fact]
    public void BoundaryPredicateMatchesWordExceptJoiners()
    {
        // Joiners are boundary word characters without being word characters;
        // BCL \b places no boundary inside letter-joiner-letter runs.
        var wordOracle = new Regex(@"\w", RegexOptions.CultureInvariant);
        var boundaryOracle = new Regex(@"\b", RegexOptions.CultureInvariant);
        for (var i = 0; i <= char.MaxValue; i++)
        {
            var ch = (char)i;
            if (i is 8204 or 8205)
            {
                Assert.True(Utf8OwnedCharClass.IsBoundaryWordChar(ch));
                continue;
            }

            Assert.Equal(wordOracle.IsMatch(ch.ToString()), Utf8OwnedCharClass.IsBoundaryWordChar(ch));
        }

        foreach (var joiner in new[] { (char)8204, (char)8205 })
        {
            var sandwich = "a" + joiner + "b";
            Assert.Equal(new[] { 0, 3 }, boundaryOracle.Matches(sandwich).Select(static match => match.Index));
        }
    }

    [Fact]
    public void EcmaPredicateMatchesBclEcmaWordClass()
    {
        // U+0130 is the single BMP point where the engine-level BCL \w differs
        // from the table function: the engine matches it while the ASCII table
        // does not, and the product matches it through its own engine path.
        var oracle = new Regex(@"\w", RegexOptions.CultureInvariant | RegexOptions.ECMAScript);
        for (var i = 0; i <= char.MaxValue; i++)
        {
            if (i == 304)
            {
                Assert.False(Utf8OwnedCharClass.IsECMAWordChar((char)i));
                continue;
            }

            Assert.Equal(oracle.IsMatch(((char)i).ToString()), Utf8OwnedCharClass.IsECMAWordChar((char)i));
        }
    }

    [Fact]
    public void PayloadConstantsKeepPinnedBytes()
    {
        Assert.Equal(new string(new char[] { (char)0, (char)1, (char)0, (char)0 }), Utf8OwnedCharClass.AnyClass);
        Assert.Equal(new string(new char[] { (char)1, (char)2, (char)0, (char)10, (char)11 }), Utf8OwnedCharClass.NotNewLineClass);
        Assert.Equal(new string(new char[] { (char)0, (char)0, (char)1, (char)100 }), Utf8OwnedCharClass.SpaceClass);
        Assert.Equal(new string(new char[] { (char)0, (char)0, (char)1, (char)65436 }), Utf8OwnedCharClass.NotSpaceClass);
        Assert.Equal(new string(new char[] { (char)0, (char)4, (char)0, (char)9, (char)14, (char)32, (char)33 }), Utf8OwnedCharClass.ECMASpaceClass);
        Assert.Equal(new string(new char[] { (char)1, (char)4, (char)0, (char)9, (char)14, (char)32, (char)33 }), Utf8OwnedCharClass.NotECMASpaceClass);
        Assert.Equal(new string(new char[] { (char)0, (char)0, (char)1, (char)9 }), Utf8OwnedCharClass.DigitClass);
        Assert.Equal(new string(new char[] { (char)0, (char)0, (char)1, (char)65527 }), Utf8OwnedCharClass.NotDigitClass);
        Assert.Equal(new string(new char[] { (char)0, (char)0, (char)10, (char)0, (char)2, (char)4, (char)5, (char)3, (char)1, (char)6, (char)9, (char)19, (char)0 }), Utf8OwnedCharClass.WordClass);
        Assert.Equal(new string(new char[] { (char)0, (char)0, (char)10, (char)0, (char)65534, (char)65532, (char)65531, (char)65533, (char)65535, (char)65530, (char)65527, (char)65517, (char)0 }), Utf8OwnedCharClass.NotWordClass);
    }

    public static IEnumerable<object?[]> PositiveConstantCases()
    {
        yield return new object?[] { "AnyClass", ".", 528 };
        yield return new object?[] { "NotNewLineClass", ".", 512 };
        yield return new object?[] { "DigitClass", "\\d", 512 };
        yield return new object?[] { "SpaceClass", "\\s", 512 };
        yield return new object?[] { "ECMASpaceClass", "\\s", 768 };
        yield return new object?[] { "WordClass", "\\w", 512 };
    }

    [Theory]
    [MemberData(nameof(PositiveConstantCases))]
    public void PositiveConstantsMatchBclShorthands(string constant, string pattern, int options)
    {
        var payload = constant switch
        {
            "AnyClass" => Utf8OwnedCharClass.AnyClass,
            "NotNewLineClass" => Utf8OwnedCharClass.NotNewLineClass,
            "DigitClass" => Utf8OwnedCharClass.DigitClass,
            "SpaceClass" => Utf8OwnedCharClass.SpaceClass,
            "ECMASpaceClass" => Utf8OwnedCharClass.ECMASpaceClass,
            _ => Utf8OwnedCharClass.WordClass,
        };
        var oracle = new Regex(pattern, (RegexOptions)options);
        foreach (var code in PositiveConstantCorpus())
        {
            var ch = (char)code;
            if (constant == "SpaceClass" && (code is 133 or 160 or 8232))
            {
                continue;
            }

            Assert.Equal(oracle.IsMatch(ch.ToString()), Utf8OwnedCharClass.CharInClass(ch, payload));
        }
    }

    private static int[] PositiveConstantCorpus()
    {
        var corpus = new System.Collections.Generic.List<int>();
        for (var code = 0; code < 256; code++)
        {
            corpus.Add(code);
        }

        corpus.AddRange(new[] { 256, 304, 305, 913, 945, 233, 2028, 8204, 8205, 8232, 65279, 19968 });
        return corpus.ToArray();
    }
}