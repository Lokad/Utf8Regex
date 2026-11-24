using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScopedPatternBehaviorTests
{
    private static readonly string[] ProbeCorpus =
    {
        "", "a", "A", "b", "B", "ab", "aB", "Ab", "AB", "abc", "ABC", "aBc",
        "k", "K", "kK", "Kk", "aa", "AA", "aA", "Aa", "aab", "aba", "abba",
        "axxb", "1", "12", "a1", "1a", " ", "a b", "aBcDe", "AaBb", "x",
        "ax", "xa", "ababa", "aabbaa", ">", "a>", "a>b", "(a)", "(a)(b)",
        "\n", "a\nb", "ab\ncd", "\n\n", "a\rb", "\r\n", "a\n", "\nb",
        "xy", "xxy", "yxy",
    };

    [Theory]
    [InlineData("(?i:[ab]){2}|(?:a.)", RegexOptions.CultureInvariant)]
    [InlineData("(?m:\\d){2}|(?:a.)", RegexOptions.CultureInvariant)]
    [InlineData("(?n:a){2}|(?:a.)", RegexOptions.CultureInvariant)]
    [MemberData(nameof(Utf8OwnedTreeParserBalanceTests.BalanceExplicitCaptureCases), MemberType = typeof(Utf8OwnedTreeParserBalanceTests))]
    public void PublicOperationsAgreeWithTheScalarOracle(string pattern, RegexOptions options)
    {
        var oracle = new Regex(pattern, options);
        using var regex = new Utf8Regex(pattern, options: options);

        Assert.Equal(oracle.GetGroupNumbers(),
            Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot));
        var names = oracle.GetGroupNames();
        for (var slot = 0; slot < names.Length; slot++)
        {
            var expected = Encoding.UTF8.GetBytes(names[slot]);
            var actual = new byte[expected.Length];
            Assert.Equal(OperationStatus.Done, regex.TryCopyGroupNameUtf8(slot, actual, out var written));
            Assert.Equal(expected.Length, written);
            Assert.Equal(expected, actual);
        }

        void CheckInput(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var expected = oracle.Match(input);
            var actual = regex.Match(bytes);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success)
            {
                Utf8MatchSequence.AssertRange(actual, input, expected.Index, expected.Length);
                Utf8MatchSequence.AssertCaptures(expected, Utf8MatchSequence.First(regex.CreateMatchCursor(bytes)), bytes, input);
            }
            Assert.Equal(oracle.Count(input), regex.Count(bytes));
            Assert.Equal(oracle.Replace(input, "<$0>"), Encoding.UTF8.GetString(regex.Replace(bytes, "<$0>"u8)));
        }

        foreach (var input in ProbeCorpus) CheckInput(input);

        uint stream = 0x9E3779B9u;
        const string alphabet = "aAbB09 \n\r#()[]{}>.+*?|^$\\éyZ";
        for (var index = 0; index < 128; index++)
        {
            var chars = new char[index % 9];
            for (var position = 0; position < chars.Length; position++)
            {
                stream ^= stream << 13;
                stream ^= stream >> 17;
                stream ^= stream << 5;
                chars[position] = alphabet[(int)(stream % (uint)alphabet.Length)];
            }
            CheckInput(new string(chars));
        }

        foreach (var input in new[] { "\U0001F600", "a\U00010400b" })
        {
            // Project supplementary scalars before comparing the dot alternatives.
            if (pattern is "(?i:[ab]){2}|(?:a.)" or "(?m:\\d){2}|(?:a.)" or "(?n:a){2}|(?:a.)")
            {
                Utf8AsciiScalarOperationAssertions.Operations(pattern, options, input);
            }
            else
            {
                CheckInput(input);
            }
        }
    }
}
