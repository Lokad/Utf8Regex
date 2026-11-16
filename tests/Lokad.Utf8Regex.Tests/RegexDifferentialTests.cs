using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class RegexDifferentialTests
{
    public static TheoryData<string> Patterns => new()
    {
        "Sherlock Holmes", @"Sherlock\s+Holmes", @"\w+\s+Holmes",
        @"\w+\s+\w+\s+Holmes", @"(?<word>\w+)\s+(Holmes|Watson)",
        @"\s[a-zA-Z]{0,12}ing\s", @"\s[a-zA-Z]{0,12}ing[\s.,;]",
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
        @"^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$",
        @"([0-9]{4}[- ]){3}[0-9]{3,4}", @"^[0-9]{1,2}/[0-9]{1,2}/[0-9]{2,4}$",
        @"^\d{1,2}/\d{1,2}/\d{4}$", @"^[a-z][a-z0-9_]*\z",
        @"^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$",
        @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+",
        @"\b(?:LogError|LogWarning|LogInformation)\s*\(",
        @"\bpublic\s+async\b", @"\bawait\b\s+.{0,60}\bConfigureAwait\b",
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
        string[] subjects = ["", "é\ting", " aabb ", "Sherlock Holmes", "word Holmes", "STARTSTARTSTOPSTOP", "BEGINBEGINENDEND",
            "a0f", "A0f", "A0f\n", "A0g", "A0ff", "A0f1c2", "A0f1c2\n", "A0f1cg", "A0f1c2x",
            "Ab0", "Ab0\n", "ab0", "A_0", "Ab01", "ab01", "ab01\n", "AB01", "ab0x", "ab001",
            "A01ZZ", "A0 1ZZ", "A0Z1ZZ", "A0Z 1ZZ", "AB01ZZ", "AB0 1ZZ", "AB0Z1ZZ", "AB0Z 1ZZ",
            "AB0Z 1ZZ\n", "ABC0Z 1ZZ", "AB0Z 1Z9", "éB0Z 1ZZ", "١/٢/٢٠٠١",
            "1/2/01", "1/12/012", "12/1/0123", "12/12/0123", "12/12/0123\n", "1/2/0", "123/1/0123", "12-12-0123", "12/xx/0123",
            "1234-5678-1234-456", "1234 5678 1234 4567", "1234-5678 1234-456", "1234-5678-1234-45",
            "1234-5678-1234-45678", "1234-5678_1234-456", "1234-5678-123x-456", "x1234-5678-1234-456",
            "class Alpha struct Beta record Gamma", "éclass Nope class Goodé", "public\tasync", "xpublic async",
            "LogError( LogWarning \t( LogInformation\n(", "xLogError( LogErrorx(", "await x.ConfigureAwait", "await\nConfigureAwait",
            .. Enumerable.Range(0, 96).Select(_ => string.Concat(Enumerable.Range(0, random.Next(1, 13))
                .Select(_ => Tokens[random.Next(Tokens.Length)])))];
        using var cancellation = new CancellationTokenSource();
        foreach (var mode in new[] { "default", "token", "timeout", "budget", "compiled" })
        {
            var options = RegexOptions.CultureInvariant | (mode == "compiled" ? RegexOptions.Compiled : 0);
            var regex = new Utf8Regex(pattern, options,
                matchTimeout: mode == "timeout" ? TimeSpan.FromSeconds(10) : null,
                resourcePolicy: mode == "budget" ? new() { MaxWorkSteps = 1_000_000, MaxDepth = 4096,
                    MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 } : default);
            var oracle = new Regex(pattern, options, TimeSpan.FromSeconds(10));
            var token = mode == "token" ? cancellation.Token : default;
            foreach (var text in subjects)
            {
                var input = Encoding.UTF8.GetBytes(text);
                var context = $"pattern={pattern}; mode={mode}; subject={System.Text.Json.JsonSerializer.Serialize(text)}";
                Assert.True(oracle.IsMatch(text) == regex.IsMatch(input, token), context);
                Assert.True(oracle.Count(text) == regex.Count(input, token), context);
                var expected = oracle.Match(text);
                var actual = regex.MatchDetailed(input, token);
                Assert.True(expected.Success == actual.Success, context);
                if (expected.Success)
                {
                    AssertRange(expected, actual.IndexInBytes, actual.LengthInBytes, text, context);
                    foreach (var number in oracle.GetGroupNumbers())
                    {
                        var group = actual.GetGroup(number);
                        Assert.True(expected.Groups[number].Success == group.Success, context);
                        if (group.Success)
                            AssertRange(expected.Groups[number], group.IndexInBytes, group.LengthInBytes, text, context);
                    }
                }
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
        foreach (var pattern in new[] { @"(?<x>a)+(b)?", @"(?:ab|a)+?", "", @"[^\n]*", @"(?<x>\w+): (\w+)" })
        {
            var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant,
                resourcePolicy: new() { MaxWorkSteps = 1_000_000 });
            var oracle = new Regex(pattern, options | RegexOptions.CultureInvariant);
            foreach (var text in new[] { "", "é aab ab\n", "é: Ω a: b", "abab", "\n\n" })
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
    public void SupplementaryScalarsUseOneOracleSymbolAndOriginalByteRanges(string pattern)
    {
        // Replace each emoji by one BMP symbol, then project oracle positions through
        // original UTF-8 scalar boundaries. No comparison with surrogate-half matches.
        using var cancellation = new CancellationTokenSource();
        var random = new Random(0x51ca1);
        string[] tokens = ["a", "é", "😀", "\n"];
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options, resourcePolicy: new() { MaxWorkSteps = 1_000_000 });
            var oracle = new Regex(pattern.Replace("😀", "\ue000"), options);
            for (var sample = 0; sample < 32; sample++)
            {
                var text = string.Concat(Enumerable.Range(0, random.Next(0, 12)).Select(_ => tokens[random.Next(tokens.Length)]));
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
        foreach (var validation in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        {
            var regex = new Utf8Regex(pattern, options, inputValidation: validation);
            Assert.Equal(expected, regex.Count(input));
            Assert.Equal(expected, regex.Count(input, cancellation.Token));
        }
    }
}
