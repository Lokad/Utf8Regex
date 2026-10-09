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
        var pattern = Patterns[patternId];
        // These two patterns put the halves in separate capture atoms. Neither
        // atom denotes a valid scalar; preserve every declaration and public
        // operation through an explicitly impossible comparator with the same slots.
        var oraclePattern = patternId is 38 or 39
            ? pattern.Replace("\ud83d", "(?!)", StringComparison.Ordinal)
                .Replace("\ude00", "(?!)", StringComparison.Ordinal)
            : pattern;
        AssertOwnedOperations(pattern, options, Texts, oraclePattern, options);
    }

    internal static void AssertOwnedOperations(string pattern, RegexOptions options, string[] texts, RegexOptions oracleOptions)
        => AssertOwnedOperations(pattern, options, texts, pattern, oracleOptions);

    internal static void AssertOwnedOperations(string pattern, RegexOptions options, string[] texts,
        string oraclePattern, RegexOptions oracleOptions)
    {
        Regex oracle;
        try { oracle = new Regex(oraclePattern, oracleOptions, TimeSpan.FromSeconds(10)); }
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
        AssertOwnedOperations(regex, oracle, texts);
    }

    // Reuse complete public-operation qualification for native engines that do
    // not lower through the scalar VM. The caller must establish its route guard.
    internal static void AssertOwnedOperations(Utf8Regex regex, Regex oracle, string[] texts)
    {
        oracle = Utf8ScalarCaptureFixture.Project(oracle);
        var numbers = oracle.GetGroupNumbers();
        Assert.Equal(numbers, regex.GetGroupNumbers());
        Assert.Equal(oracle.GetGroupNames(), regex.GetGroupNames());
        foreach (var number in numbers.Concat(new[] { -1, 0, 1, 2, 5, 7, 12, 13, int.MaxValue }).Distinct())
            Assert.Equal(oracle.GroupNameFromNumber(number), regex.GroupNameFromNumber(number));
        foreach (var name in oracle.GetGroupNames().Concat(new[] { "00", "01", "05", "012", "1", "x", "12", "missing" }))
            Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromName(name));
        using var source = new CancellationTokenSource();
        foreach (var text in texts)
        {
            var subject = new Utf8ScalarCaptureFixture.Subject(text);
            var input = subject.Input;
            Assert.Equal(oracle.IsMatch(subject.Projected), regex.IsMatch(input, source.Token));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "IsMatch");
            Assert.Equal(oracle.Count(subject.Projected), regex.Count(input, source.Token));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "Count");
            AssertValue(oracle.Match(subject.Projected), regex.Match(input, source.Token), subject);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "Match");
            AssertDetailed(oracle, oracle.Match(subject.Projected), regex.MatchDetailed(input, source.Token), subject);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "Detailed");
            for (var offset = 0; offset <= text.Length; offset++)
            {
                var scalarOffset = Array.BinarySearch(subject.Units, offset);
                if (scalarOffset < 0)
                {
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatchFromUtf16Offset(input, offset, source.Token)).ParamName);
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(() => regex.CountFromUtf16Offset(input, offset, source.Token)).ParamName);
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchFromUtf16Offset(input, offset, source.Token)).ParamName);
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchDetailedFromUtf16Offset(input, offset, source.Token)).ParamName);
                    Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(() => regex.EnumerateMatchesFromUtf16Offset(input, offset)).ParamName);
                    continue;
                }
                Assert.Equal(oracle.IsMatch(subject.Projected.AsSpan(), scalarOffset), regex.IsMatchFromUtf16Offset(input, offset, source.Token));
                Assert.Equal(oracle.Count(subject.Projected.AsSpan(), scalarOffset), regex.CountFromUtf16Offset(input, offset, source.Token));
                AssertValue(oracle.Match(subject.Projected, scalarOffset), regex.MatchFromUtf16Offset(input, offset, source.Token), subject);
                AssertDetailed(oracle, oracle.Match(subject.Projected, scalarOffset), regex.MatchDetailedFromUtf16Offset(input, offset, source.Token), subject);
                var expected = oracle.EnumerateMatches(subject.Projected.AsSpan(), scalarOffset);
                var actual = regex.EnumerateMatchesFromUtf16Offset(input, offset);
                while (expected.MoveNext())
                {
                    Assert.True(actual.MoveNext());
                    Assert.Equal(subject.UnitRange(expected.Current.Index, expected.Current.Length),
                        (actual.Current.IndexInUtf16, actual.Current.LengthInUtf16));
                    Assert.True(actual.Current.TryGetByteRange(out var index, out var length));
                    Assert.Equal(subject.ByteRange(expected.Current.Index, expected.Current.Length), (index, length));
                }
                Assert.False(actual.MoveNext());
            }
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "Offsets");
            foreach (var count in new[] { 0, 1, 2 })
            foreach (var liveToken in new[] { false, true })
            {
                var actual = liveToken ? regex.EnumerateSplits(input, count, source.Token)
                    : regex.EnumerateSplits(input, count);
                if (count == 1)
                {
                    // .NET 10.0.12 ValueSplitEnumerator can project a negative
                    // endpoint for count one. Regex.Split retains the stable
                    // single whole-input result; do not copy the enumerator bug.
                    Assert.Equal(new[] { subject.Projected }, oracle.Split(subject.Projected, count));
                    Assert.True(actual.MoveNext());
                    Assert.True(actual.Current.IsByteAligned);
                    Assert.Equal((0, input.Length), (actual.Current.IndexInBytes, actual.Current.LengthInBytes));
                    Assert.Equal(0, actual.Current.IndexInUtf16);
                    Assert.Equal(text.Length, actual.Current.LengthInUtf16);
                    Assert.Equal(text, actual.Current.GetValueString());
                    Assert.False(actual.MoveNext());
                    continue;
                }
                var expected = oracle.EnumerateSplits(subject.Projected, count);
                while (expected.MoveNext())
                {
                    Assert.True(actual.MoveNext());
                    var (index, length) = expected.Current.GetOffsetAndLength(subject.Projected.Length);
                    Assert.True(actual.Current.IsByteAligned);
                    Assert.Equal(subject.ByteRange(index, length), (actual.Current.IndexInBytes, actual.Current.LengthInBytes));
                    Assert.Equal(subject.UnitRange(index, length), (actual.Current.IndexInUtf16, actual.Current.LengthInUtf16));
                    Assert.Equal(subject.Value(index, length), actual.Current.GetValueString());
                }
                Assert.False(actual.MoveNext());
            }
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "Splits");
            foreach (var template in new[] { "Q", "$&", "<$1>|$" + "{5}|$12|$" + "{12}|$" + "{x}|$2147483647",
                "$00|$01|$" + "{012}|$+", "$" + (char)96 + "|$'|$_", "\ud83d$" + "{12}\ude00" })
            {
                // The comparator receives the scalar form of each literal;
                // string APIs no longer retain halves across substitutions.
                var expected = Utf8ScalarCaptureFixture.Subject.Restore(oracle.Replace(subject.Projected, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(template))));
                Assert.Equal(expected, regex.ReplaceToString(input, template, source.Token));
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "Text template " + template);
                var expectedBytes = Encoding.UTF8.GetBytes(expected);
                Assert.Equal(expectedBytes, regex.Replace(input, template, source.Token));
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "Byte template " + template);
                var destination = new byte[expectedBytes.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, source.Token));
                Assert.Equal(expectedBytes, destination.AsSpan(0, written).ToArray());
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "Destination template " + template);
                var utf8Template = Encoding.UTF8.GetBytes(template);
                var expectedUtf8Template = Encoding.UTF8.GetBytes(Utf8ScalarCaptureFixture.Subject.Restore(oracle.Replace(subject.Projected, Encoding.UTF8.GetString(utf8Template))));
                Assert.Equal(expectedUtf8Template, regex.Replace(input, utf8Template, source.Token));
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "UTF-8 template");
                var utf8Destination = new byte[expectedUtf8Template.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, utf8Template, utf8Destination, out var utf8Written, source.Token));
                Assert.Equal(expectedUtf8Template, utf8Destination.AsSpan(0, utf8Written).ToArray());
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "UTF-8 destination template");
            }
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "Templates");
            var expectedCalls = new List<string>();
            var expectedString = Utf8ScalarCaptureFixture.Subject.Restore(oracle.Replace(subject.Projected, match => { expectedCalls.Add(Signature(oracle, match, subject)); return "[" + match.Groups[numbers[^1]].Value + "]"; }));
            var state = (Numbers: numbers, Calls: new List<string>(), Subject: subject, Oracle: oracle,
                Matches: oracle.Matches(subject.Projected).Cast<Match>().ToArray());
            var actualString = regex.ReplaceToString(input, state,
                static (in Utf8MatchContext context, ref (int[] Numbers, List<string> Calls, Utf8ScalarCaptureFixture.Subject Subject, Regex Oracle, Match[] Matches) callback) =>
                {
                    AssertDetailed(callback.Oracle, callback.Matches[callback.Calls.Count], context, callback.Subject);
                    callback.Calls.Add(Signature(in context, callback.Numbers));
                    return "[" + context.GetGroup(callback.Numbers[^1]).GetValueString() + "]";
                }, source.Token);
            Assert.Equal(expectedString, actualString);
            Assert.Equal(expectedCalls, state.Calls);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "String callback");
            var expectedByteCalls = new List<string>();
            var expectedByteText = oracle.Replace(subject.Projected, match =>
            {
                var range = subject.UnitRange(match.Index, match.Length);
                Assert.True(IsBoundary(text, range.Index) && IsBoundary(text, range.Index + range.Length),
                    "The caller must provide a comparator whose match boundaries overlap scalar semantics.");
                expectedByteCalls.Add(Signature(oracle, match, subject));
                return "[" + match.Groups[numbers[^1]].Value + "]";
            });
            var byteState = (Numbers: numbers, Calls: new List<string>(), Subject: subject, Oracle: oracle,
                Matches: oracle.Matches(subject.Projected).Cast<Match>().ToArray());
            var actualBytes = regex.Replace(input, byteState,
                static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer,
                    ref (int[] Numbers, List<string> Calls, Utf8ScalarCaptureFixture.Subject Subject, Regex Oracle, Match[] Matches) callback) =>
                {
                    AssertDetailed(callback.Oracle, callback.Matches[callback.Calls.Count], context, callback.Subject);
                    callback.Calls.Add(Signature(in context, callback.Numbers));
                    writer.Append(("[" + context.GetGroup(callback.Numbers[^1]).GetValueString() + "]").AsSpan());
                }, source.Token);
            Assert.Equal(Encoding.UTF8.GetBytes(Utf8ScalarCaptureFixture.Subject.Restore(expectedByteText)), actualBytes);
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

    internal static void AssertValue(Match expected, Utf8ValueMatch actual, Utf8ScalarCaptureFixture.Subject? subject = null)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(subject?.UnitRange(expected.Index, expected.Length) ?? (expected.Index, expected.Length),
            (actual.IndexInUtf16, actual.LengthInUtf16));
        if (subject is not null && expected.Success)
        {
            Assert.True(actual.TryGetByteRange(out var index, out var length));
            Assert.Equal(subject.ByteRange(expected.Index, expected.Length), (index, length));
        }
    }

    internal static void AssertDetailed(Regex oracle, Match expected, Utf8MatchContext actual, Utf8ScalarCaptureFixture.Subject? subject = null)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(subject?.UnitRange(expected.Index, expected.Length) ?? (expected.Index, expected.Length),
            (actual.IndexInUtf16, actual.LengthInUtf16));
        if (subject is not null && expected.Success)
        {
            Assert.True(actual.IsByteAligned);
            Assert.Equal(subject.ByteRange(expected.Index, expected.Length), (actual.IndexInBytes, actual.LengthInBytes));
        }
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        Assert.Equal(subject?.Value(expected.Index, expected.Length) ?? expected.Value, actual.GetValueString());
        var numbers = expected.Success ? oracle.GetGroupNumbers() : new[] { 0 };
        foreach (var number in numbers.Concat(new[] { -1, 1, 2, 5, 7, 12, int.MaxValue }).Distinct())
        {
            var group = actual.GetGroup(number);
            var expectedGroup = expected.Groups[number];
            Assert.Equal(expectedGroup.Success, group.Success);
            Assert.Equal(subject?.UnitRange(expectedGroup.Index, expectedGroup.Length) ?? (expectedGroup.Index, expectedGroup.Length),
                (group.IndexInUtf16, group.LengthInUtf16));
            Assert.Equal(subject?.Value(expectedGroup.Index, expectedGroup.Length) ?? expectedGroup.Value, group.GetValueString());
            if (subject is not null && expectedGroup.Success)
            {
                Assert.True(group.IsByteAligned);
                Assert.Equal(subject.ByteRange(expectedGroup.Index, expectedGroup.Length), (group.IndexInBytes, group.LengthInBytes));
            }
            Assert.Equal(expectedGroup.Captures.Count, group.CaptureCount);
            Assert.Equal(numbers.Contains(number), actual.TryGetGroup(number, out var tryGroup));
            Assert.Equal(group.GetValueString(), tryGroup.GetValueString());
            for (var capture = 0; capture < expectedGroup.Captures.Count; capture++)
            {
                var projected = group.GetCapture(capture);
                var wanted = expectedGroup.Captures[capture];
                Assert.Equal(subject?.UnitRange(wanted.Index, wanted.Length) ?? (wanted.Index, wanted.Length),
                    (projected.IndexInUtf16, projected.LengthInUtf16));
                Assert.Equal(subject?.Value(wanted.Index, wanted.Length) ?? wanted.Value, projected.GetValueString());
                if (subject is not null)
                {
                    Assert.True(projected.IsByteAligned);
                    Assert.Equal(subject.ByteRange(wanted.Index, wanted.Length), (projected.IndexInBytes, projected.LengthInBytes));
                }
            }
        }
        foreach (var name in oracle.GetGroupNames().Concat(new[] { "00", "01", "05", "012", "missing" }))
            Assert.Equal(subject is null ? expected.Groups[name].Value : Utf8ScalarCaptureFixture.Subject.Restore(expected.Groups[name].Value), actual.GetGroup(name).GetValueString());
    }

    private static string Signature(Regex oracle, Match match, Utf8ScalarCaptureFixture.Subject subject)
    {
        var range = subject.UnitRange(match.Index, match.Length);
        var result = new StringBuilder($"{range.Index}:{range.Length};");
        foreach (var number in oracle.GetGroupNumbers())
        {
            result.Append(number).Append('=');
            foreach (Capture capture in match.Groups[number].Captures)
            {
                var mapped = subject.UnitRange(capture.Index, capture.Length);
                result.Append($"{mapped.Index}:{mapped.Length}:{subject.Value(capture.Index, capture.Length)};");
            }
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
