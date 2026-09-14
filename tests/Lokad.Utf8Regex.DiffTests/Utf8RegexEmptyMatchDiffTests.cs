using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexEmptyMatchDiffTests
{
    public static TheoryData<string, string> EmptyCapableCases => new()
    {
        { "a*", "" },
        { "a*", "a" },
        { "a*", "aa" },
        { "a*", "b" },
        { "a*", "aab" },
        { "b*", "aab" },
        { "a?", "" },
        { "a?", "aa" },
        { "^", "abc" },
        { "$", "abc" },
        { "^$", "" },
        { "^$", "a" },
        { "(?=a)", "banana" },
        { "(?!a)", "banana" },
        { "(?:)", "ab" },
        { "a*?", "aa" },
        { "é*", "café" },
        { "é?", "café" },
    };

    [Theory]
    [MemberData(nameof(EmptyCapableCases))]
    public void EmptyCapableCountMatchesRuntime(string pattern, string input)
    {
        // PLAN.md M1: global loops must advance identically after empty and
        // nonempty matches; the oracle defines the retry contract.
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(EmptyCapableCases))]
    public void EmptyCapableMatchMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertMatchParity();
    }

    [Theory]
    [MemberData(nameof(EmptyCapableCases))]
    public void EmptyCapableReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$&>");
    }

    [Theory]
    [MemberData(nameof(EmptyCapableCases))]
    public void EmptyCapableEnumerateMatchesRuntime(string pattern, string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, pattern, RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options);
            var actual = new List<(int Index, int Length)>();
            foreach (var match in regex.EnumerateMatches(bytes))
            {
                actual.Add((match.IndexInUtf16, match.LengthInUtf16));
            }

            Assert.Equal(expected, actual);
        }
    }
}
