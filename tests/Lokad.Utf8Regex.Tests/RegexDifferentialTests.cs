using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class RegexDifferentialTests
{
    public static TheoryData<string> Patterns => new()
    {
        "Sherlock Holmes", @"Sherlock\s+Holmes", @"\w+\s+Holmes",
        @"A.{0,1}B|B.{0,1}A|A.{2,3}B|B.{2,3}A",
        @"(?s:A.{1,2}B|B.{1,2}A|A.{3,4}B|B.{3,4}A)",
        @"(?s:(?:A.{1,2}B|A.{3,4}B)|(?:B.{1,2}A|B.{3,4}A))",
        @"(?:A.{0,1}B|A.{2,3}B)|(?:B.{0,1}A|B.{2,3}A)",
        @"(?<x>A.{0,1}B)|(?<x>B.{0,1}A)|(?<x>A.{2,3}B)|(?<x>B.{2,3}A)",
        @"\w+\s+\w+\s+Holmes", @"(?<word>\w+)\s+(Holmes|Watson)",
        @"\s[a-zA-Z]{0,12}ing\s", @"\s[a-zA-Z]{0,12}ing[\s.,;]",
        @"\s[a-z]{0,1}ing\s|\s[a-z]{3,4}ing\s",
        @"(?<g>\s[a-z]{0,1}ing\s|\s[a-z]{3,4}ing\s)",
        @"\b(?:the|and|that|have|for)\b", @"\d+(?:\.\d+)?",
        @"(?<=\s)[A-Z][a-z]+(?=\s)", ".*?,", @"[a-z]+ing|[a-z]+ed|[a-z]+ly",
        @"\b\w+ly\b", @"(?m)^\s*\w+:", @"(?<x>\w)\k<x>",
        @"""[^""\n]*""", @"(?<key>\w+): (?<value>\w+)",
        @"\b(?:\d{1,3}\.){3}\d{1,3}\b", @"https?://[^\s/?#]+[^\s?#]*",
        @"[\w.+-]+@[\w.-]+\.[\w.-]+", @"^([a-z]+)(\d+)$",
        @"(?<x>a)+(b)?", @"(?<7>a)|(?<name>b)", @"(?:(a)|(b))*", "",
        @"[^\n]*", @"(?:ab|a)+?", @"(?>a+)a|a+", @"\G[a-z]+",
        @"START(?:(?<open>START)|(?<-open>STOP)|(?:(?!START|STOP)[\s\S]))*STOP(?(open)(?!))",
        @"BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\s\S]))*END(?(open)(?!))",
        @"^[a-f0-9]{3}$", @"^ab[0-9]{2}$", @"^[A-Z][A-Za-z0-9_]{2}$",
        @"^[a-z][a-z0-9_]*$", @"^(?<head>[a-z])(?<tail>[a-z0-9_]*)$",
        @"^ab[a-f0-9]{2,4}$", @"(?m:^[a-z]{1,3}$)", @"\A[a-f0-9]{3}\z",
        @"^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$",
        @"^(?<head>[A-Z]{1,2})(?<number>[0-9][A-Z0-9]?) ?[0-9](?<tail>[A-Z]{2})$",
        @"^(?:[A-Z][0-9][0-9][A-Z]{2}|[A-Z][0-9][A-Z0-9][0-9][A-Z]{2}|[A-Z][0-9] [0-9][A-Z]{2}|[A-Z]{2}[0-9][A-Z0-9] [0-9][A-Z]{2})$",
        @"([0-9]{4}[- ]){3}[0-9]{3,4}", @"^[0-9]{1,2}/[0-9]{1,2}/[0-9]{2,4}$",
        @"^(?:[0-9]{4}[- ]){1}(?:[0-9]{1}|[0-9]{3})$",
        @"^(?<chunk>[0-9]{4}[- ])(?<tail>[0-9]{1}|[0-9]{3})$",
        @"^\d{1,2}/\d{1,2}/\d{4}$", @"^[a-z][a-z0-9_]*\z",
        @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", @"\b\d{1,2}\-\d{1,2}\-\d{2,4}\b",
        @"\b\d{4}\-\d{1,2}\-\d{1,2}\b", @"\b\d{1,2}\.\d{1,2}\.\d{2,4}\b",
        @"\b\d{4}\.\d{1,2}\.\d{1,2}\b", @"(?m:^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$)",
        @"^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$",
        @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+",
        @"\b(?:LogError|LogWarning|LogInformation)\s*\(",
        @"\bpublic\s+async\b", @"\bawait\b\s+.{0,60}\bConfigureAwait\b",
        @"ab[0-9]d", @"ab.[0-9]d", @"a[A-Za-z]cd", @"ab[0-9]cd", @"abc[0-9]defg", @"ab[0-9][0-9]cd",
        @"(?<x>(?:ab|aba){2})d", @"(?<x>ab|aba){2}d",
    };

    private static readonly string[] Tokens =
    [
        "a", "b", "Z", "0", "12", " ", "\t", "\n", "\r", ".", ",", ":", "@",
        "é", "Ω", "中", "٣", "\u0301", "\u00a0", "\u2003", "ing", "sing",
        "Sherlock", "Holmes", "Watson", "the", "and", "https://a.b/", "a@b.c",
        "12.1.2.3", "key: value", "\"word\"", "START", "STOP", "BEGIN", "END",
    ];

    // Bounded, reproducible generation; all BMP characters here are complete scalars.
    // The BCL is an oracle only for this overlapping language, never for UTF-16 splits.
    [Theory]
    [MemberData(nameof(Patterns))]
    public void GeneratedSubjectsAgreeAcrossOperationsAndCallModes(string pattern)
    {
        var random = new Random(0x5ca1a);
        string[] subjects = ["", "ABAB", "A1BA1B", "A\nB", "BABA", "AxxBAB", "A\nBABA", "BAAAB", "éABAB中 BABA", "é\ting", " aabb ", "Sherlock Holmes", "word Holmes", "STARTSTARTSTOPSTOP", "BEGINBEGINENDEND",
            "a0f", "A0f", "A0f\n", "A0g", "A0ff", "A0f1c2", "A0f1c2\n", "A0f1cg", "A0f1c2x",
            "Alpha_42", "alpha_42", "alpha_42\n", "1alpha", "alphé", "alphaK", "ab0f", "ab0f1", "ab0f12", "x\nabc\nz",
            "Ab0", "Ab0\n", "ab0", "A_0", "Ab01", "ab01", "ab01\n", "AB01", "ab0x", "ab001",
            "A01ZZ", "A0 1ZZ", "A0Z1ZZ", "A0Z 1ZZ", "AB01ZZ", "AB0 1ZZ", "AB0Z1ZZ", "AB0Z 1ZZ",
            "AB0Z 1ZZ\n", "ABC0Z 1ZZ", "AB0Z 1Z9", "éB0Z 1ZZ", "١/٢/٢٠٠١",
            " ing ", " aing ", " abing ", " abcing ", " abcding ", " abcdeing ",
            "é abing Ω", "\u2003abcing\u00a0",
            "SW1 1ZZ", "W1A 0AX", "M11AE", "B338TH", "CR2 6XH", "DN55 1PT",
            "A1 9A", "SW11ZZ", "SW1 1Z1", "SW1  1ZZ", "SW1é1ZZ",
            "1/2/01", "1/12/012", "12/1/0123", "12/12/0123", "12/12/0123\n", "1/2/0", "123/1/0123", "12-12-0123", "12/xx/0123",
            "1-2-20", "2020-01-02", "2020.1.2", "1.2.2020", "١٢/٢/٢٠٠١", "a1/2/33b", "\n1/2/2020\n",
            "1234-5678-1234-456", "1234 5678 1234 4567", "1234-5678 1234-456", "1234-5678-1234-45",
            "1234-5678-1234-45678", "1234-5678_1234-456", "1234-5678-123x-456", "x1234-5678-1234-456",
            "1234-1", "1234-12", "1234-123", "1234-12\n", "1234 123", "é1234-12",
            "class Alpha struct Beta record Gamma", "éclass Nope class Goodé", "public\tasync", "xpublic async",
            "ab1d-ab2d-ab3d", "ab1cd-ab7cd-", "abc1defg-abc7defg-", "ab12cd ab34cd ab5cd ab567cd", "aZcd ab_3d ab!7d",
            "ab\n3d abé3d ab_٣d", "ab0d", "ab0", "ab_3", "ab0d\n",
            "ababd", "abaabad", "éabaabadax ababaz", "abaabax",
            "LogError( LogWarning \t( LogInformation\n(", "xLogError( LogErrorx(", "await x.ConfigureAwait", "await\nConfigureAwait",
            .. Enumerable.Range(0, 96).Select(_ => string.Concat(Enumerable.Range(0, random.Next(1, 13))
                .Select(_ => Tokens[random.Next(Tokens.Length)])))];
        using var cancellation = new CancellationTokenSource();
        foreach (var mode in new[] { "default", "token", "timeout", "budget", "compiled" })
        {
            var options = RegexOptions.CultureInvariant | (mode == "compiled" ? RegexOptions.Compiled : 0);
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = mode == "timeout" ? TimeSpan.FromSeconds(10) : null,
                ResourcePolicy = mode == "budget" ? new()
                {
                    MaxWorkSteps = 1_000_000,
                    MaxDepth = 4096,
                    MaxWorkingBytes = 16 << 20,
                    MaxResultBytes = 16 << 20
                }

                : default
            });
            var oracle = new Regex(pattern, options, TimeSpan.FromSeconds(10));
            var token = mode == "token" ? cancellation.Token : default;
            var captures = new Utf8CaptureRange[(regex.CaptureGroupCount + 1)];
            foreach (var text in subjects)
            {
                var input = Encoding.UTF8.GetBytes(text);
                var context = $"pattern={pattern}; mode={mode}; subject={System.Text.Json.JsonSerializer.Serialize(text)}";
                Assert.True(oracle.IsMatch(text) == regex.IsMatch(input, token), context);
                Assert.True(oracle.Count(text) == regex.Count(input, token), context);
                var expected = oracle.Match(text);
                var actual = regex.Match(input, 0, captures, token);
                Assert.True(expected.Success == actual.Success, context);
                if (expected.Success)
                {
                    AssertRange(expected, actual.StartOffsetInBytes,
                        actual.EndOffsetInBytes - actual.StartOffsetInBytes, text, context);
                    for (var slot = 0; slot < captures.Length; slot++)
                    {
                        var number = regex.GroupNumberFromCaptureSlot(slot);
                        var group = captures[slot];
                        Assert.True(expected.Groups[number].Success == group.Success, context);
                        if (group.Success)
                            AssertRange(expected.Groups[number], group.StartOffsetInBytes,
                                group.EndOffsetInBytes - group.StartOffsetInBytes, text, context);
                    }
                }
                else Assert.All(captures, capture => Assert.False(capture.Success, context));
                // Exercise original-prefix context and EOF, rather than slicing off the prefix.
                foreach (var start in new[] { 0, text.Length / 2, text.Length }.Distinct())
                    AssertCursor(regex, oracle, text, text, start, token, context);
            }
        }
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void DirectionAndLinearOptionsPreserveGeneratedCaptureResults(RegexOptions options)
    {
        using var cancellation = new CancellationTokenSource();
        foreach (var pattern in new[] { @"(?<x>a)+(b)?", @"(?:ab|a)+?", "", @"[^\n]*", @"(?<x>\w+): (\w+)",
            @"(?<day>[0-9]{1,2})/[0-9]{1,2}/[0-9]{4}" })
        {
            var regex = new Utf8Regex(pattern, options: options | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1_000_000
                }
            });
            var oracle = new Regex(pattern, options | RegexOptions.CultureInvariant);
            foreach (var text in new[] { "", "é aab ab\n", "é: Ω a: b", "abab", "\n\n", "é1/2/2020", "12/12/2020\n" })
                foreach (var start in Enumerable.Range(0, text.Length + 1))
                AssertCursor(regex, oracle, text, text, start, cancellation.Token, $"{pattern}; {options}; start={start}");
        }
    }

    [Theory]
    [InlineData("(?<x>.)")]
    [InlineData("(?<x>.)+?")]
    [InlineData("[^a]+")]
    [InlineData(@"[^\n]*")]
    [InlineData("(?:a|.){1,2}")]
    [InlineData("(?:😀|é)+")]
    [InlineData(@"ab.[0-9]d")]
    [InlineData(@"A.{0,1}B|B.{0,1}A|A.{2,3}B|B.{2,3}A")]
    [InlineData(@"(?:A.{0,1}B|A.{2,3}B)|(?:B.{0,1}A|B.{2,3}A)")]
    [InlineData(@"(?<x>A.{0,1}B)|(?<x>B.{0,1}A)|(?<x>A.{2,3}B)|(?<x>B.{2,3}A)")]
    public void SupplementaryScalarsUseOneOracleSymbolAndOriginalByteRanges(string pattern)
    {
        // Replace each emoji by one BMP symbol, then project oracle positions through
        // original UTF-8 scalar boundaries. No comparison with surrogate-half matches.
        using var cancellation = new CancellationTokenSource();
        var random = new Random(0x51ca1);
        string[] tokens = ["a", "é", "😀", "\n"];
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new()
                {
                    MaxWorkSteps = 1_000_000
                }
            });
            var oracle = new Regex(pattern.Replace("😀", "\ue000"), options);
            for (var sample = 0; sample < 32; sample++)
            {
                var text = sample == 0 ? "ab😀3d abé3d ab\n3d ABAB A😀B" :
                    string.Concat(Enumerable.Range(0, random.Next(0, 12)).Select(_ => tokens[random.Next(tokens.Length)]));
                var symbols = text.Replace("😀", "\ue000");
                foreach (var start in new[] { 0, symbols.Length / 2, symbols.Length }.Distinct())
                    AssertCursor(regex, oracle, text, symbols, start, cancellation.Token, $"{pattern}; {options}; {text}");
            }
        }
    }

    private static void AssertCursor(Utf8Regex regex, Regex oracle, string text, string symbols, int start,
        CancellationToken token, string context)
    {
        var input = Encoding.UTF8.GetBytes(text);
        // Symbols are either the original BMP subject or the scalar-mapped oracle text.
        var boundaries = new int[symbols.Length + 1];
        var scalar = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            boundaries[scalar + 1] = boundaries[scalar] + rune.Utf8SequenceLength;
            scalar++;
        }
        using var cursor = regex.CreateMatchCursor(input, boundaries[start], token);
        foreach (Match expected in oracle.Matches(symbols, start))
        {
            Assert.True(cursor.MoveNext(out var actual), context);
            Assert.True(actual.Value.StartOffsetInBytes == boundaries[expected.Index] &&
                actual.Value.EndOffsetInBytes == boundaries[expected.Index + expected.Length], context);
            foreach (var number in oracle.GetGroupNumbers())
            {
                Assert.True(actual.TryGetGroup(number, out var group), context);
                var expectedGroup = expected.Groups[number];
                Assert.True(group.Success == expectedGroup.Success, context);
                if (group.Success)
                    Assert.True(group.StartOffsetInBytes == boundaries[expectedGroup.Index] &&
                        group.EndOffsetInBytes == boundaries[expectedGroup.Index + expectedGroup.Length], context);
                var captures = actual.GetCaptures(number);
                Assert.True(captures.Length == expectedGroup.Captures.Count, context);
                for (var i = 0; i < captures.Length; i++)
                {
                    var capture = expectedGroup.Captures[i];
                    Assert.True(captures[i].StartOffsetInBytes == boundaries[capture.Index] &&
                        captures[i].EndOffsetInBytes == boundaries[capture.Index + capture.Length], context);
                }
            }
        }
        Assert.False(cursor.MoveNext(out _), context);
    }

    [Theory]
    [InlineData("A.{1,2}B|B.{1,2}A|A.{3,4}B|B.{3,4}A")]
    [InlineData("(?:A.{1,2}B|A.{3,4}B)|(?:B.{1,2}A|B.{3,4}A)")]
    public void SplitGapAlternativesPreservePriorityAndSingleline(string pattern)
    {
        foreach (var options in new[] { RegexOptions.Singleline, RegexOptions.Singleline | RegexOptions.Compiled })
            foreach (var text in new[] { "A1BA1B", "A\nB", "B\nA", "éA\nB中A1BA1B" })
            {
                var regex = new Utf8Regex(pattern, options: options);
                var oracle = new Regex(pattern, options);
                var input = Encoding.UTF8.GetBytes(text);
                Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
                Assert.Equal(oracle.Count(text), regex.Count(input));
                var expected = oracle.Match(text);
                var actual = regex.Match(input);
                Assert.Equal(expected.Success, actual.Success);
                if (expected.Success) AssertRange(expected, actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes, text, pattern);
            }
    }

    private static void AssertRange(Capture expected, int start, int length, string text, string context)
    {
        Assert.True(start == Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)) &&
            length == Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length)), context);
    }

    [Theory]
    [InlineData("é\ting")]
    [InlineData("é\tg")]
    [InlineData("😀\ting")]
    [InlineData("é\u00a0sing")]
    [InlineData("é\ting ")]
    [InlineData("é\ting g")]
    [InlineData("é\ting\u2003g")]
    [InlineData("é\ting\u2003")]
    [InlineData("é\ting\u2003\ting")]
    [InlineData("é\tabcdefghijklmning")]
    public void LiteralAtSubjectEndCannotReadMissingSuffix(string text)
    {
        const string pattern = @"\s[a-zA-Z]{0,12}ing\s";
        var input = Encoding.UTF8.GetBytes(text);
        var expected = Regex.Count(text, pattern, RegexOptions.CultureInvariant);
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected, regex.Count(input, cancellation.Token));
        }
    }

    [Theory]
    // Atomic groups
    [InlineData("(?>a+)ab", "aaab", false)]
    [InlineData("(?>a|ab)c", "abc", false)]
    [InlineData("(?>a+)b", "aaab", true)]
    [InlineData("(?>(a+))b", "aaab", true)]
    [InlineData("(?>a+)+b", "aaab", true)]
    [InlineData("(?>)a", "a", true)]
    [InlineData("x(?>y|y)z", "xyz", true)]
    [InlineData("(?>a+)ab", "aab", false)]
    [InlineData("(?>ab|a)+z", "abababz", true)]
    [InlineData("(?>a)?b", "ab", true)]
    [InlineData("(?>a)?b", "b", true)]
    [InlineData("(?>\\p{L}+)1", "aa1", true)]
    [InlineData("(?>\\p{L}+)b", "ab", false)]
    // Contiguous anchors
    [InlineData("\\Ga+", "aabaa", true)]
    [InlineData("\\Ga+", "baa", false)]
    [InlineData("a\\Gb", "aab", false)]
    [InlineData("\\Ga*", "aaa", true)]
    [InlineData("\\Gx*", "abc", true)]
    [InlineData("\\G(?:a|b)+", "abxab", true)]
    [InlineData("\\Gb", "ab", false)]
    [InlineData("\\Ga+", "aay\U0001F600", true)]
    [InlineData("\\G(a|ab)+", "aab\U0001F600", true)]
    [InlineData("\\Ga+\\b", "aay\U0001F600", false)]
    [InlineData("a+\\G", "aya\U0001F600", false)]
    // Escapes
    [InlineData("\\x41", "A", true)]
    [InlineData("\\x41", "B", false)]
    [InlineData("\\x41+", "AAB", true)]
    [InlineData("\\u0041", "A", true)]
    [InlineData("\\u004A", "J", true)]
    [InlineData("\\0", "\u0000", true)]
    [InlineData("\\00", "\u0000", true)]
    [InlineData("\\000", "\u0000", true)]
    [InlineData("\\07", "\u0007", true)]
    [InlineData("\\12", "\n", true)]
    [InlineData("\\12B", "\nB", true)]
    [InlineData("\\08", "\u0000" + "8", true)]
    [InlineData("a\\0", "a\u0000", true)]
    [InlineData("[\\x41]", "A", true)]
    [InlineData("[\\x41]", "B", false)]
    [InlineData("[\\x41-\\x5A]", "M", true)]
    [InlineData("[\\x41-\\x5A]", "a", false)]
    [InlineData("[\\101-\\132]", "S", true)]
    [InlineData("[\\u0041]", "A", true)]
    [InlineData("[\\0]", "\u0000", true)]
    [InlineData("[\\7]", "\u0007", true)]
    [InlineData("[\\12]", "\n", true)]
    [InlineData("[\\77]", "?", true)]
    [InlineData("(\\x41)(\\x42)", "AB", true)]
    [InlineData("\\n", "\n", true)]
    [InlineData("\\n", "n", false)]
    [InlineData("\\t", "\t", true)]
    [InlineData("\\r", "\r", true)]
    [InlineData("\\a", "\u0007", true)]
    [InlineData("\\a", "a", false)]
    [InlineData("\\f", "\f", true)]
    [InlineData("\\v", "\v", true)]
    [InlineData("\\e", "\u001B", true)]
    [InlineData("\\e", "e", false)]
    [InlineData("\\cA", "\u0001", true)]
    [InlineData("\\cA", "A", false)]
    [InlineData("\\cZ", "\u001A", true)]
    [InlineData("\\c@", "\u0000", true)]
    [InlineData("\\n+", "\n\n\n", true)]
    [InlineData("\\cA+", "\u0001\u0001", true)]
    [InlineData("(?<=\\n)x", "\nx", true)]
    [InlineData("(?<=\\n)x", "ax", false)]
    [InlineData("\\cM", "M", false)]
    [InlineData("\\cm", "m", false)]
    public void FeatureIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
            }
        }
    }


    [Theory]
    // Atomic groups
    [InlineData("(?>a+)b", "aaab aaab", 2)]
    [InlineData("(?>a+)ab", "aaab aaab", 0)]
    // Backreferences
    [InlineData("(a)\\1", "aaabaaa", 2)]
    [InlineData("(a|b)+\\1", "abb abb", 2)]
    [InlineData("(x)?b\\1", "bbb", 0)]
    // Balancing groups
    [InlineData("(?<open>a)+(?<-open>b)+", "aab aab", 2)]
    [InlineData("(?<o>a)(?<-o>b)", "ab ab", 2)]
    [InlineData("(?<o>\\p{L}+)(?<-o>\\d+)", "ab12 cd34", 2)]
    // Unicode categories in sets
    [InlineData("[\\p{L}a]+", "1a\u03B1", 1)]
    [InlineData("[\\p{L}\\p{Nd}_]+", "a1_-", 1)]
    [InlineData("[^\\p{L}a]+", "a1 b", 1)]
    [InlineData("[\\P{L}-x]+", "a-x1", 1)]
    [InlineData("[\\p{L}-x]+", "a-x1", 1)]
    [InlineData("[\\p{L}\\D]+", "a15 ", 2)]
    [InlineData("[^\\p{L}\\d]+", "a1 ", 1)]
    [InlineData("[\\p{IsGreek}a]+", "a\u03B1b1", 1)]
    [InlineData("[^\\p{IsGreek}a]+", "a\u03B1b1", 1)]
    [InlineData("[\\d-\\p{L}]+", "5-a", 1)]
    // Classes
    [InlineData("[a-z]+", "abUG12cd", 2)]
    [InlineData("[^a-z]+", "abUG12", 1)]
    [InlineData("[a-c-e]+", "abcdef-", 3)]
    [InlineData("[abc]+", "xxcabbayy", 1)]
    [InlineData("[\\n-\\r]+", "a\n\rb", 1)]
    // Dot
    [InlineData("a.*b", "aab aabb", 1)]
    [InlineData("(a.c)+", "abc ayc", 2)]
    [InlineData("a.*?b", "a1b a22b", 2)]
    [InlineData("a.*b", "xyz", 0)]
    // Escapes
    [InlineData("\\x41+", "aAb", 1)]
    [InlineData("[\\x41-\\x5A]+", "ABc", 1)]
    [InlineData("\\t+", "\ta\t", 2)]
    [InlineData("\\cA+", "\u0001x\u0001", 2)]
    [InlineData("(?<=\\n)x", "ax\nx", 1)]
    // Compound patterns
    [InlineData("[\\da]+", "a1b22", 2)]
    [InlineData("[^\\da]+", "a-b", 1)]
    [InlineData("[\\w-]+", "a-b c", 2)]
    // Lookaround
    [InlineData("(?=a)a", "aaa", 3)]
    [InlineData("(?<=a)b", "aab ab", 2)]
    [InlineData("(?!b)a", "ba ba", 2)]
    [InlineData("(?<=\\p{L}{2})x", "abxabx", 2)]
    [InlineData("(?<=a{2})x", "aaxaa", 1)]
    [InlineData("(?<=(a))b", "xa\U0001F600b", 0)]
    [InlineData("(?<=(a))b", "xaab\U0001F600", 1)]
    // Named groups
    [InlineData("(?<v>a)+", "aa aa", 2)]
    [InlineData("(?<a>x)+", "xxx", 1)]
    // Shorthands
    [InlineData("\\d+", "a12b34", 2)]
    [InlineData("\\d+", "a\u0661\u0662b", 1)]
    [InlineData("\\D+", "a1b2", 2)]
    [InlineData("\\w+", "a b_c1", 2)]
    [InlineData("\\s+", "a\t b\nc", 2)]
    [InlineData("\\S+", "a b", 2)]
    [InlineData("(\\d+)", "a12b", 1)]
    public void FeatureCountMatchesOracleOnOwnedSlice(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }


    [Theory]
    // Backreferences
    [InlineData("(a)\\1", "aa", true)]
    [InlineData("(a)\\1", "ab", false)]
    [InlineData("(a|b)+\\1", "abb", true)]
    [InlineData("(a|b)+\\1", "aba", false)]
    [InlineData("(a*)\\1", "aaa", true)]
    [InlineData("(x)?b\\1", "b", false)]
    [InlineData("(x)?b\\1", "bxb", false)]
    [InlineData("(a)(b)\\2\\1", "abba", true)]
    [InlineData("((a)b)\\1", "abb", false)]
    [InlineData("((a)b)\\1", "abab", true)]
    [InlineData("([a-z]+)\\1", "abcabc", true)]
    [InlineData("(a+)\\1", "aaabaaa", true)]
    [InlineData("(a?)\\1b", "b", true)]
    [InlineData("(\\p{L}+)\\1", "abab", true)]
    [InlineData("(\\p{L}+)\\1", "ab ab", false)]
    [InlineData("(\\p{IsBasicLatin}+)\\1", "HiHi", true)]
    [InlineData("([^\\p{L}]+)\\1", "11", true)]
    [InlineData("([\\p{L}a]+)\\1", "aBaB", true)]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)\\8", "abcdefghh", true)]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)\\8", "abcdefgha", false)]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)(i)\\9", "abcdefghii", true)]
    [InlineData("(a)(b)(c)(d)(e)(f)(g)(h)(i)\\9", "abcdefghia", false)]
    // Balancing groups
    [InlineData("(?<open>a)+(?<-open>b)+", "aab", true)]
    [InlineData("(?<open>a)+(?<-open>b)+", "bbb", false)]
    [InlineData("(?<open>a)(?<-open>b)", "ab", true)]
    [InlineData("(?<open>a)(?<-open>b)", "ac", false)]
    [InlineData("(?<a>x)(?<b-a>y)", "xy", true)]
    [InlineData("(?<a>x)(?<b-a>y)", "xx", false)]
    [InlineData("(?<a>x)(?<b-a>y)(?<c-b>z)", "xyz", true)]
    [InlineData("(?<o>a)(?<-o>b)?b", "ab", true)]
    [InlineData("(?<o>a)+(?<-o>b)+(?<-o>c)", "aabc", true)]
    [InlineData("((?<o>a)+(?<-o>b)+)", "aab", true)]
    [InlineData("(?<o>a)+(?<-o>b)+\\1", "aabaa", true)]
    [InlineData("(?<a>x)(?'b-a'y)", "xy", true)]
    [InlineData("(?<o>\\p{L}+)(?<-o>\\d+)", "ab12", true)]
    [InlineData("(?<o>\\p{L}+)(?<-o>\\d+)", "ab1", true)]
    [InlineData("(?<o>\\p{L}+)(?<-o>\\d+)", "ab", false)]
    [InlineData("(?<o>\\p{L}+)(?<-o>\\p{Nd}+)", "ab12", true)]
    [InlineData("(?<o>\\p{L}+)(?<-o>\\p{Nd}+)", "ab", false)]
    // Word boundaries
    [InlineData("x\\b!", "ax!by", true)]
    [InlineData("x\\b!", "axby", false)]
    [InlineData("x\\Bx", "axxb", true)]
    [InlineData("x\\Bx", "ax bx", false)]
    [InlineData("\\A.*\\z", "ab", true)]
    [InlineData("\\A.*\\z", "xab\n", false)]
    [InlineData("\\A.*\\Z", "ab", true)]
    [InlineData("\\A.*\\Z", "ab\n", true)]
    [InlineData("\\b", "a\u2060b", true)]
    [InlineData("\\b", "a\u200Db", true)]
    [InlineData("\\b\\p{L}+\\b", "\u00E9x", true)]
    [InlineData("\\b\\w+\\b", "caf\u00E9", true)]
    // Classes
    [InlineData("[a-z]+", "xxabyy", true)]
    [InlineData("[a-z]+", "XXABYY", false)]
    [InlineData("[^a-z]+", "xxAYyy", true)]
    [InlineData("[abc]+", "xxcabbayy", true)]
    [InlineData("[a-c-e]+", "xxdyy", false)]
    [InlineData("[a-c-e]+", "xxeyy", true)]
    [InlineData("[a-c-e]+", "xx-yy", true)]
    [InlineData("[A-z]+", "xx[\\]^_`yy", true)]
    [InlineData("[]a]+", "xx]yy", true)]
    [InlineData("[-a]+", "xx-yy", true)]
    [InlineData("[a-]+", "xx-yy", true)]
    [InlineData("[a\\-z]+", "xx-yy", true)]
    [InlineData("[a\\-z]+", "xxmyy", false)]
    [InlineData("[[a]+", "xx[yy", true)]
    [InlineData("[a[]+", "xx[yy", true)]
    [InlineData("[+--.]+", "xx,yy", true)]
    [InlineData("[+--.]+", "xx*yy", false)]
    [InlineData("[--a]+", "xx/yy", true)]
    [InlineData("[--0]+", "xx/yy", true)]
    [InlineData("[--0]+", "xxayy", false)]
    [InlineData("[a-b-c]+", "xx-yy", true)]
    [InlineData("[\\n-\\r]+", "x\ny\rz", true)]
    [InlineData("[\\b]+", "x\by", true)]
    [InlineData("[\\0]+", "x\0y", true)]
    [InlineData("[\\e]+", "x\ey", true)]
    [InlineData("[\\--0]+", "xx/yy", false)]
    [InlineData("[\\--0]+", "xx-yy", true)]
    [InlineData("[^]a]+", "xxyy", true)]
    [InlineData("[^]a]+", "]]aa", false)]
    [InlineData("[]-a]+", "xx]yy", true)]
    [InlineData("[]-a]+", "xx_yy", true)]
    [InlineData("[]-a]+", "xxAyy", false)]
    // Dot
    [InlineData("a.*b", "xxa1bzz", true)]
    [InlineData("a.*b", "xxayy", false)]
    [InlineData("a.*b", "xxabyy", true)]
    [InlineData("a.*b", "", false)]
    [InlineData(".*b", "xxb", true)]
    [InlineData("(a.c)+", "xxabcaycyy", true)]
    [InlineData("(a.c)+", "xxayy", false)]
    [InlineData("a.*?b", "xxa1b2byy", true)]
    [InlineData("x*|y.", "ay", true)]
    [InlineData("a|.*", "zz", true)]
    // Compound patterns
    [InlineData("[\\da]+", "a1", true)]
    [InlineData("[\\da]+", "xy", false)]
    [InlineData("[\\da]+", "5", true)]
    [InlineData("[\\da]+", "b", false)]
    [InlineData("[a\\d]+", "b", false)]
    [InlineData("[a\\d]+", "5", true)]
    [InlineData("[\\Da]+", "5", false)]
    [InlineData("[\\Da]+", "x", true)]
    [InlineData("[^\\da]+", "5", false)]
    [InlineData("[^\\da]+", "-", true)]
    [InlineData("[\\w-]+", "-", true)]
    [InlineData("[\\w-]+", " ", false)]
    [InlineData("[^\\w\\s]+", "-", true)]
    [InlineData("[^\\w\\s]+", "_", false)]
    [InlineData("[^\\D\\s]+", "5", true)]
    [InlineData("[^\\D\\s]+", " ", false)]
    [InlineData("[\\d-e]+", "5", true)]
    [InlineData("[\\d-e]+", "-", true)]
    [InlineData("[\\d-e]+", "e", true)]
    [InlineData("[\\d-e]+", "d", false)]
    [InlineData("[\\d-x]+", "5", true)]
    [InlineData("[\\d-x]+", "e", false)]
    [InlineData("[\\d-x]+", "-", true)]
    [InlineData("[-\\d]+", "-", true)]
    [InlineData("[-\\d]+", "7", true)]
    [InlineData("[\\s\\S]+", "x", true)]
    // Lookaround
    [InlineData("(?=a)a", "ba", true)]
    [InlineData("(?=a)b", "ab", false)]
    [InlineData("(?!b)a", "ba", true)]
    [InlineData("(?!b)a", "b", false)]
    [InlineData("(?!a)a", "a", false)]
    [InlineData("(?!)a", "a", false)]
    [InlineData("(?<=a)b", "ab", true)]
    [InlineData("(?<=a)b", "cb", false)]
    [InlineData("(?<=(a))b", "xa\U0001F600b", false)]
    [InlineData("(?<=(a))b", "xaab\U0001F600", true)]
    [InlineData("(?<!a)b", "cb", true)]
    [InlineData("(?<!a)b", "ab", false)]
    [InlineData("((?=a)a)\\1", "aaa", true)]
    [InlineData("x(?=a*)", "xa", true)]
    [InlineData("x(?=a+)", "xa", true)]
    [InlineData("x(?=a+)", "xx", false)]
    [InlineData("x(?=[a-z]*)", "xa", true)]
    [InlineData("x(?=\\p{L}*)", "xa", true)]
    [InlineData("a(?<=a)b", "ab", true)]
    [InlineData("(?<=^a)b", "ab", true)]
    [InlineData("(?<=a$)b", "ab", false)]
    [InlineData("(?=(a)|ab)b", "ab", false)]
    [InlineData("(?=a)b|ab", "ab", true)]
    [InlineData("(?=a(?=b))", "ab", true)]
    [InlineData("(?<=(?<=a)a)b", "aab", true)]
    [InlineData("(?<=a)+b", "ab", true)]
    [InlineData("(?<!\\p{Nd})x", "ax", true)]
    [InlineData("(?<=\\b\\p{L})x", "axb", true)]
    [InlineData("(?<=\\B\\p{L})x", "axb", false)]
    [InlineData("(?<=(?=\\p{L})\\p{L})x", "axb", true)]
    [InlineData("(?<=\\p{L}{2})x", "abx", true)]
    [InlineData("(?<=\\p{L}{2})x", "axb", false)]
    [InlineData("(?<=a{2})x", "aax", true)]
    [InlineData("((?=a)a)(b)", "ab", true)]
    // Named groups
    [InlineData("(?<word>a)+", "xaa", true)]
    [InlineData("(?<word>a)+", "xb", false)]
    [InlineData("(?<a>x)+", "xx", true)]
    [InlineData("(?<a>x)(?<b>y)+", "xyy", true)]
    [InlineData("(?<b>a)(c)+", "acc", true)]
    [InlineData("((?<a>x)+)", "xx", true)]
    [InlineData("(?<word>\\d+)+", "12", true)]
    [InlineData("(?<b>a)(c)\\2", "aca", true)]
    [InlineData("(?<b>a)(c)\\1", "aca", false)]
    [InlineData("(?<a>ab|cd)+", "abcd", true)]
    [InlineData("(?<a>ab|cd)+", "ax", false)]
    // Shorthands
    [InlineData("\\d+", "a1b", true)]
    [InlineData("\\d+", "aab", false)]
    [InlineData("\\d+", "a\u0661b", true)]
    [InlineData("\\D+", "a b", true)]
    [InlineData("\\w+", "a_b1", true)]
    [InlineData("\\w+", "a\u0301b", true)]
    [InlineData("\\w+", "a\u203Fb", true)]
    [InlineData("\\w+", "a\u200Db", true)]
    [InlineData("\\W+", "a b", true)]
    [InlineData("\\W+", "ab", false)]
    [InlineData("\\s+", "a\t b", true)]
    [InlineData("\\s+", "a\u0085b", true)]
    [InlineData("\\s+", "a\u00A0b", true)]
    [InlineData("\\s+", "a\u2000b", true)]
    [InlineData("\\s+", "a\u001Cb", false)]
    [InlineData("\\s+", "a\u202Fb", true)]
    [InlineData("\\s+", "a\uFEFFb", false)]
    [InlineData("\\S+", "a b", true)]
    [InlineData("[\\d]+", "a1b", true)]
    [InlineData("[^\\d]+", "a1b", true)]
    [InlineData("[^\\d]+", "123", false)]
    [InlineData("\\d+:\\w+", "12:ab", true)]
    public void FeatureIsMatchMatchesOracleOnOwnedSlice(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
        }
    }
}
