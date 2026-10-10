using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Canonical class and scalar membership witnesses. BCL is an interpreted
// comparator only for complete BMP scalars; supplementary membership and
// byte ranges have explicit scalar expectations. Historical input rows stay.
public sealed class Utf8OwnedCharClassTests
{
    public static IEnumerable<object?[]> ClassCases()
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


    [Theory]
    [MemberData(nameof(ClassCases))]
    public void CanonicalClassesMatchScalarWitnesses(string pattern, int options)
    {
        AssertScalarMembership(pattern, (RegexOptions)options, MatchingCorpus());
    }

    [Fact]
    public void SinglelineDotUsesExplicitScalarRangePayload()
    {
        var tree = Utf8OwnedTreeParser.TryParse("(?s:.)", RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        Assert.Equal(new[] { 0, 2, 0, 0, 0x110000 }, Assert.Single(HarvestClasses(tree.Root)).Data);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("(?s:.)", options);
            foreach (var code in new[] { 0, 10, 0xffff, 0x10400, 0x1f600, 0x10ffff })
            {
                var input = Encoding.UTF8.GetBytes(new Rune(code).ToString());
                var match = regex.Match(input);
                Assert.True(match.Success);
                Assert.Equal(0, match.IndexInBytes);
                Assert.Equal(input.Length, match.LengthInBytes);
                Assert.Equal(1, regex.Count(input));
            }
        }
    }

    private static List<Utf8ScalarClass> HarvestClasses(Utf8TreeNode root)
    {
        var classes = new List<Utf8ScalarClass>();
        var stack = new Stack<Utf8TreeNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Kind is Utf8TreeNodeKind.Set or Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic)
            {
                Assert.NotNull(node.ScalarClass);
                Assert.Null(node.Str);
                classes.Add(node.ScalarClass);
            }
            for (var index = 0; index < node.ChildCount; index++) stack.Push(node.Child(index));
        }
        return classes;
    }

    private static void AssertScalarMembership(string pattern, RegexOptions options, IEnumerable<int> corpus)
    {
        var parsed = Utf8OwnedTreeParser.TryParse(pattern, options);
        if (parsed is null)
        {
            var failure = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options));
            Assert.Equal("pattern", failure.ParamName);
            return;
        }
        Assert.NotEmpty(HarvestClasses(parsed.Root));
        var program = Utf8OwnedTreeSliceLowerer.TryLower(parsed, options);
        Assert.NotNull(program);
        // The interpreted comparator avoids compiled U+FFFF class-loop bugs.
        // Neither comparator behavior nor surrogate halves define scalar matching.
        var oracle = new Regex(@"\A(?:" + pattern + @")\z", options & ~RegexOptions.Compiled);
        var regexes = new[] { new Utf8Regex(pattern, options), new Utf8Regex(pattern, options | RegexOptions.Compiled) };
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        foreach (var code in corpus)
        {
            if (!Rune.IsValid(code))
            {
                // Preserve old surrogate corpus entries as malformed UTF-8 cases.
                var invalid = new byte[] { (byte)(0xe0 | code >> 12), (byte)(0x80 | (code >> 6 & 0x3f)), (byte)(0x80 | (code & 0x3f)) };
                foreach (var regex in regexes) Assert.Throws<ArgumentException>(() => regex.Count(invalid));
                continue;
            }
            var scalar = new Rune(code);
            var input = Encoding.UTF8.GetBytes(scalar.ToString());
            var expected = scalar.IsBmp ? oracle.IsMatch(scalar.ToString()) : SupplementaryExpected(pattern, options, scalar);
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
            var found = Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, true, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, found.Breach);
            Assert.Equal(expected, found.IsMatch);
            if (expected) Assert.Equal(input.Length, found.EndIndex);
            foreach (var regex in regexes)
            {
                Assert.Equal(expected ? 1 : 0, regex.Count(input));
                var match = regex.Match(input);
                Assert.Equal(expected, match.Success);
                if (expected)
                {
                    Assert.Equal(0, match.IndexInBytes);
                    Assert.Equal(input.Length, match.LengthInBytes);
                }
            }
        }
    }

    private static bool SupplementaryExpected(string pattern, RegexOptions options, Rune scalar)
    {
        Assert.False(scalar.IsBmp);
        var category = Rune.GetUnicodeCategory(scalar);
        var ecma = (options & RegexOptions.ECMAScript) != 0;
        var digit = !ecma && category == UnicodeCategory.DecimalDigitNumber;
        var word = !ecma && category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or
            UnicodeCategory.NonSpacingMark or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
        var letter = Rune.IsLetter(scalar);
        return pattern switch
        {
            "." or "[^a]" or "[^a-z]" or "[^a-[b]]" or @"[^\x00-\x7F]" or @"[\s\S]" => true,
            @"\d" or @"[\d]" or @"[^\D]" or @"[\da-z]" or @"[\d-[13579]]" => digit,
            @"\D" or @"[^\d]" or @"[\D]" => !digit,
            @"\w" or @"[^\W]" or @"[a\w]" => word,
            @"\W" or @"[\W]" => !word,
            @"\s" or @"[\s]" or @"[^\S]" or @"[\s-[a]]" => false,
            @"\S" or @"[^\s]" or @"[\S]" => true,
            @"\p{L}" or @"[\p{L}a]" => letter,
            @"\P{L}" => !letter,
            @"\p{Lu}" => category == UnicodeCategory.UppercaseLetter,
            @"\P{Lu}" => category != UnicodeCategory.UppercaseLetter,
            @"\p{Nd}" => category == UnicodeCategory.DecimalDigitNumber,
            @"\P{Nd}" => category != UnicodeCategory.DecimalDigitNumber,
            "[a]" or "[ab]" or "[a-z]" or "[a-]" or "[-a]" or "[--0]" or "[a-[b]]" or
            "[a-zA-Z0-9]" or "[é]" or "[α-ω]" or "[(]" or "[)]" or "[a^]" or
            "[a-z-[aeiou]]" or "[abc-[b-d]]" or "[a-z-[aeiou-[e]]]" or
            @"[\x00-\x7F]" or @"[\b]" or @"[a\b]" or @"[\[]" or @"[\\]" or @"[\^]" or
            @"\p{IsGreek}" or @"\p{IsBasicLatin}" or @"\p{_xmlC}" => false,
            _ => throw new InvalidOperationException("Missing explicit supplementary witness: " + pattern),
        };
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
        var scalar = Rune.GetRuneAt(text, 0);
        Assert.Equal(expected, Rune.ToLowerInvariant(scalar) != Rune.ToUpperInvariant(scalar));
        Assert.Equal(expected, char.ToLowerInvariant(text[0]) != char.ToUpperInvariant(text[0]));
    }

    [Fact]
    public void ClassRetainsOnlyCanonicalIntegerStorage()
    {
        var fields = typeof(Utf8ScalarClass).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.Equal(typeof(int[]), Assert.Single(fields).FieldType);
        Assert.DoesNotContain(typeof(Utf8ScalarClass).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            property => property.PropertyType == typeof(string));
        Assert.Null(typeof(Utf8ScalarClass).GetMethod("TryProject", BindingFlags.Static | BindingFlags.NonPublic));
        Assert.Null(typeof(Utf8ScalarClass).Assembly.GetType("Lokad.Utf8Regex.Internal.FrontEnd.Utf8OwnedCharClass"));
        Assert.Null(typeof(Utf8ScalarClass).GetMethod("TryImport", BindingFlags.Static | BindingFlags.NonPublic));
        Assert.Null(typeof(Utf8ScalarClass).GetMethod("ImportCategories", BindingFlags.Static | BindingFlags.NonPublic));
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
        yield return new object?[] { "[\\D]", 512 };
        yield return new object?[] { "[^\\D]", 512 };
        yield return new object?[] { "[\\W]", 512 };
        yield return new object?[] { "[\\s\\S]", 512 };
        yield return new object?[] { "\\P{Lu}", 512 };
        yield return new object?[] { "\\P{L}", 512 };
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
        yield return new object?[] { ".", 512 };
        yield return new object?[] { "\\p{_xmlC}", 512 };
        yield return new object?[] { "\\p{_xmlS}", 512 };
    }

    [Theory]
    [MemberData(nameof(MatchingCases))]
    public void OwnedMatchingAgreesWithBcl(string pattern, int options)
    {
        AssertScalarMembership(pattern, (RegexOptions)options, MatchingCorpus());
    }

    private static int[] MatchingCorpus()
    {
        var corpus = new System.Collections.Generic.List<int>();
        for (var i = 0; i < 128; i++)
        {
            corpus.Add(i);
        }

        foreach (var code in new int[] { 128, 133, 159, 160, 173, 233, 255, 256, 304, 305, 382, 913, 937, 945, 962, 963, 969, 1056, 1072, 1234, 2005, 2028, 4096, 8203, 8204, 8205, 8232, 8233, 20013, 12354, 19968, 65279, 55296, 56320, 57343, 65534, 65535 })
        {
            corpus.Add(code);
        }

        corpus.AddRange(new[] { 0x10400, 0x10428, 0x1d7ce, 0x1f600, 0x10ffff });
        return corpus.ToArray();
    }

    [Fact]
    public void WordLookupMatchesBclWordClass()
    {
        var oracle = new Regex(@"\w", RegexOptions.CultureInvariant);
        var ownedLookup = Utf8SliceScalarClasses.WordCharAsciiLookup;
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
            if (!Rune.IsValid(i)) continue;
            var scalar = new Rune(i);
            Assert.Equal(oracle.IsMatch(scalar.ToString()), Utf8SliceScalarClasses.IsWord(scalar, false, false));
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
            if (!Rune.IsValid(i)) continue;
            var scalar = new Rune(i);
            if (i is 8204 or 8205)
            {
                Assert.True(Utf8SliceScalarClasses.IsWord(scalar, true, false));
                continue;
            }

            Assert.Equal(wordOracle.IsMatch(scalar.ToString()), Utf8SliceScalarClasses.IsWord(scalar, true, false));
        }

        foreach (var joiner in new[] { (char)8204, (char)8205 })
        {
            var sandwich = "a" + joiner + "b";
            Assert.Equal(new[] { 0, 3 }, boundaryOracle.Matches(sandwich).Select(static match => match.Index));
            var regex = new Utf8Regex(@"\b", RegexOptions.CultureInvariant);
            var positions = new List<int>();
            foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(sandwich))) positions.Add(match.IndexInBytes);
            Assert.Equal(new[] { 0, 5 }, positions);
        }
    }

    [Fact]
    public void EcmaPredicateMatchesBclEcmaWordClass()
    {
        var oracle = new Regex(@"\w", RegexOptions.CultureInvariant | RegexOptions.ECMAScript);
        for (var value = 0; value <= char.MaxValue; value++)
        {
            if (!Rune.IsValid(value)) continue;
            var scalar = new Rune(value);
            Assert.Equal(oracle.IsMatch(scalar.ToString()), Utf8SliceScalarClasses.IsWord(scalar, false, true));
        }
        Assert.True(Utf8SliceScalarClasses.IsWord(new Rune(0x0130), false, true));
        Assert.False(Utf8SliceScalarClasses.IsWord(new Rune(0x10400), false, true));
    }

    [Fact]
    public void CanonicalShorthandsKeepScalarDefinitions()
    {
        (string Pattern, int[] Expected)[] definitions =
        [
            ("(?s:.)", [0, 2, 0, 0, 0x110000]),
            (".", [1, 2, 0, '\n', '\n' + 1]),
            (@"\d", [0, 0, 1, 9]),
            (@"\D", [0, 0, 1, -9]),
            (@"\s", [0, 0, 1, 100]),
            (@"\S", [0, 0, 1, -100]),
            (@"\w", [0, 0, 10, 0, 2, 4, 5, 3, 1, 6, 9, 19, 0]),
            (@"\W", [0, 0, 10, 0, -2, -4, -5, -3, -1, -6, -9, -19, 0]),
        ];
        foreach (var definition in definitions)
        {
            var parsed = Utf8OwnedTreeParser.TryParse(definition.Pattern, RegexOptions.CultureInvariant);
            Assert.NotNull(parsed);
            Assert.Equal(definition.Expected, Assert.Single(HarvestClasses(parsed.Root)).Data);
        }
    }

    public static IEnumerable<object?[]> ShorthandCases()
    {
        yield return new object?[] { "AnyClass", ".", 528 };
        yield return new object?[] { "NotNewLineClass", ".", 512 };
        yield return new object?[] { "DigitClass", "\\d", 512 };
        yield return new object?[] { "SpaceClass", "\\s", 512 };
        yield return new object?[] { "ECMASpaceClass", "\\s", 768 };
        yield return new object?[] { "WordClass", "\\w", 512 };
    }

    [Theory]
    [MemberData(nameof(ShorthandCases))]
    public void ShorthandsMatchScalarMembership(string constant, string pattern, int options)
    {
        Assert.Equal(pattern, constant switch
        {
            "AnyClass" or "NotNewLineClass" => ".",
            "DigitClass" => @"\d",
            "SpaceClass" or "ECMASpaceClass" => @"\s",
            "WordClass" => @"\w",
            _ => throw new InvalidOperationException(constant),
        });
        AssertScalarMembership(pattern, (RegexOptions)options, PositiveConstantCorpus().Concat(new[] { 0x10400, 0x1d7ce, 0x1f600 }));
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

    public static System.Collections.Generic.IEnumerable<object?[]> DecoderSweepCases()
    {
        string[] patterns = [@"\d{2,5}", @"\D{2,5}", @"\s{2,5}", @"\S{2,5}", @"\w{2,5}", @"\W{2,5}", @"[^\s]{2,5}", @"[^\S]{2,5}", @"[^\d]{2,5}", @"[^\W]{2,5}", @"[\D]{2,5}", @"[\s\S]{2,5}", @"\p{Nd}{2,5}", @"\P{Nd}{2,5}", @"\p{L}{2,5}", @"\P{L}{2,5}", @"a\D{2,5}b", @"a\W{2,5}b", @"a[^\s]{2,5}b", @"a\D+b", @"a\D+?b", @"a\W+b", @"a\D*b", @"a\D*?b", @"a\D{2,}?b", @"(?i)a\D{2,}?b", @"(?i)a\W{2,}?b"];
        string[] texts = ["xa12b", "xayzb", "xa  b", "xa\U0001F600b", "12", "x"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(DecoderSweepCases))]
    public void DecoderEndToEndSweepMatchesScalarContract(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
    }
}
