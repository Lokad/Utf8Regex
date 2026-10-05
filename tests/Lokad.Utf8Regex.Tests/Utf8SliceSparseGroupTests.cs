using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceSparseGroupTests
{
    private static readonly string[] Patterns = [
        @"(?<1>a)", @"(?<12>a)", @"(?'12'a)+", @"(?<2147483647>a)",
        @"(?<12>a)(b)+", @"(?<12>a)(?<x>b)+", @"(?<12>a)(?<2>b)(?<x>c)",
        @"(?<1>a)(b)", @"(a)(?<1>b)", @"(?<12>a)(?<12>b)",
        @"(?<12>c(?<12>a)b)", @"(?:(?<12>a)|(?<12>b))+", @"(?<12>a)(?<12>b)?",
        @"(?<12>a+)\k<12>", @"\k<12>(?<12>a)", @"(?<7>a)\7",
        @"(?<12>a)(?<12>b\k<12>)", @"(?<12>a)(?<x>b)\k<12>\k<x>",
        @"(?<12>a)?(?(12)b|c)", @"(?(12)a|b)(?<12>a)",
        @"(?<2147483647>a)(?(2147483647)b|c)", @"(?<12>a)(?<-12>b)",
        @"(?<-12>a)(?<12>b)", @"(?<12>a)(?<5-12>b)", @"(?<12>a)(?<12-12>b)",
        @"(?<12>a)(?<05-12>b)(?<5>c)", @"(?<x>a)(?<12-x>b)",
        @"(?<12>a)(?<x-12>b)", @"(?<a-a>x)", @"(?<a-1>x)",
        @"(?<01>a)(b)", @"(?<1>a)(?<01>b)\k<01>", @"(?<01>a)(?<1>b)",
        @"(?<01>(a))", @"(?<02>a)(b)(c)", @"(?<01>a)(?<x>b)",
        "(?<12>\u00e9+)\\k<12>", "(?<12>\U0001f600)", "(?<12>\ud83d)(?<5>\ude00)",
        "(?<12>\ud83d)|(?<5>\ude00)", @"(?<12>[a-z-[e]]+)\k<12>",
        @"(?=(?<12>a))\k<12>", @"(?<=(?<12>a))b", @"(?>(?<12>a|ab))\k<12>",
    ];
    private static readonly string[] Texts = ["", "a", "ab", "aba", "abb", "abcabc cabcab",
        "aa abab abbcb cc abba", "x\u00e9\u00e9\u00c9", "\U0001f600", "a\U0001f600b\U0001f600",
        "\U0001f600\U0001f600", "a\nab\nb"];

    public static IEnumerable<object[]> Cases()
    {
        for (var pattern = 0; pattern < Patterns.Length; pattern++)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var extra in new[] { RegexOptions.None, RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase })
            yield return [pattern, RegexOptions.CultureInvariant | extra |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SparseNumbersRetainOperationsHistoriesAndPublicIdentity(int patternId, RegexOptions options)
    {
        AssertOwnedOperations(Patterns[patternId], options, Texts, options);
    }

    internal static void AssertOwnedOperations(string pattern, RegexOptions options, string[] texts, RegexOptions oracleOptions)
    {
        Regex oracle;
        try { oracle = new Regex(pattern, oracleOptions, TimeSpan.FromSeconds(10)); }
        catch (RegexParseException expected)
        {
            var actual = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10)));
            Assert.Equal(expected.Error, actual.Error);
            Assert.Equal(expected.Offset, actual.Offset);
            Assert.Equal(expected.Message, actual.Message);
            Assert.Null(Utf8SliceParser.TryParse(pattern, options));
            return;
        }
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var numbers = oracle.GetGroupNumbers();
        Assert.Equal(numbers, program.GroupNumbers);
        Assert.Equal(numbers.Length - 1, program.GroupCount);
        Assert.Equal(numbers, regex.GetGroupNumbers());
        Assert.Equal(oracle.GetGroupNames(), regex.GetGroupNames());
        foreach (var number in numbers.Concat(new[] { -1, 0, 1, 2, 5, 7, 12, 13, int.MaxValue }).Distinct())
            Assert.Equal(oracle.GroupNameFromNumber(number), regex.GroupNameFromNumber(number));
        foreach (var name in oracle.GetGroupNames().Concat(new[] { "00", "01", "05", "012", "1", "x", "12", "missing" }))
            Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromName(name));
        using var source = new CancellationTokenSource();
        foreach (var text in texts)
        {
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, source.Token));
            Assert.Equal(oracle.Count(text), regex.Count(input, source.Token));
            AssertValue(oracle.Match(text), regex.Match(input, source.Token));
            AssertDetailed(oracle, oracle.Match(text), regex.MatchDetailed(input, source.Token));
            for (var offset = 0; offset <= text.Length; offset++)
            {
                Assert.Equal(oracle.IsMatch(text.AsSpan(), offset), regex.IsMatchFromUtf16Offset(input, offset, source.Token));
                Assert.Equal(oracle.Count(text.AsSpan(), offset), regex.CountFromUtf16Offset(input, offset, source.Token));
                AssertValue(oracle.Match(text, offset), regex.MatchFromUtf16Offset(input, offset, source.Token));
                AssertDetailed(oracle, oracle.Match(text, offset), regex.MatchDetailedFromUtf16Offset(input, offset, source.Token));
                var expected = oracle.EnumerateMatches(text.AsSpan(), offset);
                var actual = regex.EnumerateMatchesFromUtf16Offset(input, offset);
                while (expected.MoveNext())
                {
                    Assert.True(actual.MoveNext());
                    Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
                    Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
                }
                Assert.False(actual.MoveNext());
            }
            foreach (var count in new[] { 0, 2 })
            {
                var expected = oracle.EnumerateSplits(text, count);
                var actual = regex.EnumerateSplits(input, count, source.Token);
                while (expected.MoveNext())
                {
                    Assert.True(actual.MoveNext());
                    Assert.Equal(expected.Current.Start.Value, actual.Current.IndexInUtf16);
                    Assert.Equal(expected.Current.End.Value - expected.Current.Start.Value, actual.Current.LengthInUtf16);
                    Assert.Equal(text[expected.Current], actual.Current.GetValueString());
                }
                Assert.False(actual.MoveNext());
            }
            foreach (var template in new[] { "Q", "$&", "<$1>|$" + "{5}|$12|$" + "{12}|$" + "{x}|$2147483647",
                "$00|$01|$" + "{012}|$+", "$" + (char)96 + "|$'|$_", "\ud83d$" + "{12}\ude00" })
            {
                var expected = oracle.Replace(text, template);
                Assert.Equal(expected, regex.ReplaceToString(input, template, source.Token));
                var expectedBytes = Encoding.UTF8.GetBytes(expected);
                Assert.Equal(expectedBytes, regex.Replace(input, template, source.Token));
                var destination = new byte[expectedBytes.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, source.Token));
                Assert.Equal(expectedBytes, destination.AsSpan(0, written).ToArray());
            }
            var expectedCalls = new List<string>();
            var expectedString = oracle.Replace(text, match => { expectedCalls.Add(Signature(oracle, match)); return "[" + match.Groups[numbers[^1]].Value + "]"; });
            var state = (Numbers: numbers, Calls: new List<string>());
            var actualString = regex.ReplaceToString(input, state,
                static (in Utf8MatchContext context, ref (int[] Numbers, List<string> Calls) callback) =>
                {
                    callback.Calls.Add(Signature(in context, callback.Numbers));
                    return "[" + context.GetGroup(callback.Numbers[^1]).GetValueString() + "]";
                }, source.Token);
            Assert.Equal(expectedString, actualString);
            Assert.Equal(expectedCalls, state.Calls);
            var expectedByteCalls = new List<string>();
            string? expectedByteText = null;
            var unaligned = false;
            try
            {
                expectedByteText = oracle.Replace(text, match =>
                {
                    if (!IsBoundary(text, match.Index) || !IsBoundary(text, match.Index + match.Length))
                        throw new InvalidOperationException("Oracle match splits a scalar.");
                    expectedByteCalls.Add(Signature(oracle, match));
                    return "[" + match.Groups[numbers[^1]].Value + "]";
                });
            }
            catch (InvalidOperationException) { unaligned = true; }
            var byteState = (Numbers: numbers, Calls: new List<string>());
            byte[]? actualBytes = null;
            var byteFailure = Record.Exception(() => actualBytes = regex.Replace(input, byteState,
                static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer,
                    ref (int[] Numbers, List<string> Calls) callback) =>
                {
                    callback.Calls.Add(Signature(in context, callback.Numbers));
                    writer.Append(("[" + context.GetGroup(callback.Numbers[^1]).GetValueString() + "]").AsSpan());
                }, source.Token));
            if (unaligned)
            {
                var failure = Assert.IsType<InvalidOperationException>(byteFailure);
                Assert.Equal("The evaluator match is not aligned to UTF-8 scalar boundaries.", failure.Message);
            }
            else
            {
                Assert.Null(byteFailure);
                Assert.Equal(Encoding.UTF8.GetBytes(expectedByteText ?? ""), actualBytes);
            }
            Assert.Equal(expectedByteCalls, byteState.Calls);
            Assert.Null(Utf8SliceByteOutputScope.Current);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
    }

    private static bool IsBoundary(string text, int index)
        => index == 0 || index == text.Length || !char.IsHighSurrogate(text[index - 1]) || !char.IsLowSurrogate(text[index]);

    [Theory]
    [InlineData("(?<12>a)(?<5>b)", "ab")]
    [InlineData("(?<2147483647>a)(?<x>b)", "ab")]
    [InlineData("(?<12>a)(?<5>b)?", "a")]
    [InlineData("ab", "ab")]
    public void BothReplacementInterpretersRetainPublicNumbersAndLastOrdinal(string pattern, string text)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var match = oracle.Match(text);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var context = regex.MatchDetailed(Encoding.UTF8.GetBytes(text));
        foreach (var template in new[] { "$+", "$12|$5|$2147483647", "$00|$01|$" + "{012}" })
        {
            var parsed = Utf8ReplacementPattern.Parse(template).ResolveGroupReferences(oracle.GetGroupNumbers(), oracle.GetGroupNames());
            var plan = Utf8ReplacementPlanLowerer.Lower(parsed);
            Assert.Equal(match.Result(template), Utf8ReplacementPlanInterpreter.Apply(plan, match, text, oracle.GetGroupNumbers()[^1]));
            Assert.Equal(match.Result(template), Utf8ReplacementPlanInterpreter.ApplyOwned(plan, text, in context));
        }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("(?<12>a)", "a")]
    [InlineData("(?<12>a)?b", "b")]
    [InlineData("(?<2147483647>a)(?<x>b)", "ab")]
    public void TransitionalContextDistinguishesSparseGroupsFromHoles(string pattern, string text)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(text);
        var match = oracle.Match(text);
        var context = new Utf8MatchContext(input, text, match, Utf8BoundaryMap.Create(input), oracle.GetGroupNames());
        AssertDetailed(oracle, match, context);
    }

    [Theory]
    [InlineData("(?<0>a)")]
    [InlineData("(?<00>a)")]
    [InlineData("(?<01>a)")]
    [InlineData("(?<1a>a)")]
    [InlineData("(?<2147483648>a)")]
    [InlineData(@"(?<12>a)(?(13)b|c)")]
    [InlineData(@"(?<12>a)\k<13>")]
    [InlineData(@"(?<12>a)(?<-13>b)")]
    [InlineData(@"(?<12>a)(?<05-12>b)")]
    public void InvalidNumbersKeepExactConstructionFailures(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options));
            Assert.Equal(expected.Error, actual.Error);
            Assert.Equal(expected.Offset, actual.Offset);
            Assert.Equal(expected.Message, actual.Message);
            Assert.Null(Utf8SliceParser.TryParse(pattern, options));
        }
    }

    private static void AssertValue(Match expected, Utf8ValueMatch actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    private static void AssertDetailed(Regex oracle, Match expected, Utf8MatchContext actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        Assert.Equal(expected.Value, actual.GetValueString());
        var numbers = expected.Success ? oracle.GetGroupNumbers() : new[] { 0 };
        foreach (var number in numbers.Concat(new[] { -1, 1, 2, 5, 7, 12, int.MaxValue }).Distinct())
        {
            var group = actual.GetGroup(number);
            var expectedGroup = expected.Groups[number];
            Assert.Equal(expectedGroup.Success, group.Success);
            Assert.Equal(expectedGroup.Index, group.IndexInUtf16);
            Assert.Equal(expectedGroup.Length, group.LengthInUtf16);
            Assert.Equal(expectedGroup.Value, group.GetValueString());
            Assert.Equal(expectedGroup.Captures.Count, group.CaptureCount);
            Assert.Equal(numbers.Contains(number), actual.TryGetGroup(number, out var tryGroup));
            Assert.Equal(group.GetValueString(), tryGroup.GetValueString());
            for (var capture = 0; capture < expectedGroup.Captures.Count; capture++)
            {
                var projected = group.GetCapture(capture);
                Assert.Equal(expectedGroup.Captures[capture].Index, projected.IndexInUtf16);
                Assert.Equal(expectedGroup.Captures[capture].Length, projected.LengthInUtf16);
                Assert.Equal(expectedGroup.Captures[capture].Value, projected.GetValueString());
            }
        }
        foreach (var name in oracle.GetGroupNames().Concat(new[] { "00", "01", "05", "012", "missing" }))
            Assert.Equal(expected.Groups[name].Value, actual.GetGroup(name).GetValueString());
    }

    private static string Signature(Regex oracle, Match match)
    {
        var result = new StringBuilder($"{match.Index}:{match.Length};");
        foreach (var number in oracle.GetGroupNumbers())
        {
            result.Append(number).Append('=');
            foreach (Capture capture in match.Groups[number].Captures)
                result.Append($"{capture.Index}:{capture.Length}:{capture.Value};");
        }
        return result.ToString();
    }

    private static string Signature(in Utf8MatchContext context, int[] numbers)
    {
        var result = new StringBuilder($"{context.IndexInUtf16}:{context.LengthInUtf16};");
        foreach (var number in numbers)
        {
            result.Append(number).Append('=');
            var group = context.GetGroup(number);
            for (var capture = 0; capture < group.CaptureCount; capture++)
            {
                var item = group.GetCapture(capture);
                result.Append($"{item.IndexInUtf16}:{item.LengthInUtf16}:{item.GetValueString()};");
            }
        }
        return result.ToString();
    }
}
