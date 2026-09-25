using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// Owned character-class wire differential: every pure decoder over the set
// payload encoding must match the fork exactly. Payloads are harvested live
// from legacy-parsed trees (never hand-typed); expectations come from the fork
// at run time. Matching (CharInClass), category payloads, and builders migrate
// with the matching core, not here.
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

    [Theory]
    [MemberData(nameof(CaseConversionCases))]
    public void CaseConversionMatchesFork(string text)
    {
        Assert.Equal(
            RuntimeFrontEnd.RegexCharClass.ParticipatesInCaseConversion(text[0]),
            Utf8OwnedCharClass.ParticipatesInCaseConversion(text[0]));
    }

    [Fact]
    public void WireIndicesMatchFork()
    {
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.FlagsIndex, Utf8OwnedCharClass.FlagsIndex);
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.SetLengthIndex, Utf8OwnedCharClass.SetLengthIndex);
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.CategoryLengthIndex, Utf8OwnedCharClass.CategoryLengthIndex);
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.SetStartIndex, Utf8OwnedCharClass.SetStartIndex);
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
    public void WordTablesMatchFork()
    {
        var ownedLookup = Utf8OwnedCharClass.WordCharAsciiLookup;
        var forkLookup = RuntimeFrontEnd.RegexCharClass.WordCharAsciiLookup;
        Assert.Equal(forkLookup.Length, ownedLookup.Length);
        for (var i = 0; i < forkLookup.Length; i++)
        {
            Assert.Equal(forkLookup[i], ownedLookup[i]);
        }

        for (var i = 0; i <= char.MaxValue; i++)
        {
            var ch = (char)i;
            Assert.Equal(
                RuntimeFrontEnd.RegexCharClass.IsWordChar(ch),
                Utf8OwnedCharClass.IsWordChar(ch));
            Assert.Equal(
                RuntimeFrontEnd.RegexCharClass.IsBoundaryWordChar(ch),
                Utf8OwnedCharClass.IsBoundaryWordChar(ch));
            Assert.Equal(
                RuntimeFrontEnd.RegexCharClass.IsECMAWordChar(ch),
                Utf8OwnedCharClass.IsECMAWordChar(ch));
        }
    }

    [Fact]
    public void PayloadConstantsMatchFork()
    {
        Assert.Equal(RuntimeFrontEnd.RegexCharClass.AnyClass, Utf8OwnedCharClass.AnyClass);
    }
}