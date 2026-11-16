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
            var actual = Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10)));
            Assert.Equal("pattern", actual.ParamName);
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

    // Share public-operation assertions across the qualified scalar fixture.
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
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "IsMatch");
            Assert.Equal(oracle.Count(subject.Projected), regex.Count(input, source.Token));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "Count");
            AssertValue(oracle.Match(subject.Projected), regex.Match(input,
                (regex.Options & RegexOptions.RightToLeft) != 0 ? input.Length : 0, [], source.Token), subject);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "Match");
            Utf8MatchSequence.AssertCaptures(oracle.Match(subject.Projected),
                Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: source.Token)),
                input, index => subject.Bytes[index]);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "Capture cursor");
            for (var offset = 0; offset <= text.Length; offset++)
            {
                var scalarOffset = Array.BinarySearch(subject.Units, offset);
                if (scalarOffset < 0)
                {
                    Assert.Equal("startOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match(input, subject.Bytes[~scalarOffset - 1] + 1, [], source.Token).Success).ParamName);
                    Assert.Equal("startOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(input, subject.Bytes[~scalarOffset - 1] + 1, source.Token))).ParamName);
                    Assert.Equal("startOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() => regex.EnumerateMatches(input, subject.Bytes[~scalarOffset - 1] + 1)).ParamName);
                    continue;
                }
                Assert.Equal(oracle.IsMatch(subject.Projected.AsSpan(), scalarOffset), regex.Match(input, subject.Bytes[scalarOffset], [], source.Token).Success);
                Assert.Equal(oracle.Count(subject.Projected.AsSpan(), scalarOffset), Utf8MatchSequence.Count(regex.EnumerateMatches(input, subject.Bytes[scalarOffset], source.Token)));
                AssertValue(oracle.Match(subject.Projected, scalarOffset), regex.Match(input, subject.Bytes[scalarOffset], [], source.Token), subject);
                Utf8MatchSequence.AssertCaptures(oracle.Match(subject.Projected, scalarOffset),
                    Utf8MatchSequence.First(regex.CreateMatchCursor(input, subject.Bytes[scalarOffset], source.Token)),
                    input, index => subject.Bytes[index]);
                var expected = oracle.EnumerateMatches(subject.Projected.AsSpan(), scalarOffset);
                var actual = regex.EnumerateMatches(input, subject.Bytes[scalarOffset]);
                while (expected.MoveNext())
                {
                    Assert.True(actual.MoveNext());
                    Assert.True(actual.Current.TryGetByteRange(out var index, out var length));
                    Assert.Equal(subject.ByteRange(expected.Current.Index, expected.Current.Length), (index, length));
                }
                Assert.False(actual.MoveNext());
            }
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "Offsets");
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
                    Assert.Equal(subject.Value(index, length), actual.Current.GetValueString());
                }
                Assert.False(actual.MoveNext());
            }
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "Splits");
            foreach (var template in new[] { "Q", "$&", "<$1>|$" + "{5}|$12|$" + "{12}|$" + "{x}|$2147483647",
                "$00|$01|$" + "{012}|$+", "$" + (char)96 + "|$'|$_", "\ud83d$" + "{12}\ude00" })
            {
                // The comparator receives the scalar form of each literal;
                // string APIs no longer retain halves across substitutions.
                var expected = Utf8ScalarCaptureFixture.Subject.Restore(oracle.Replace(subject.Projected, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(template))));
                var expectedBytes = Encoding.UTF8.GetBytes(expected);
                Assert.Equal(expectedBytes, regex.Replace(input, template, source.Token));
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "Byte template " + template);
                var destination = new byte[expectedBytes.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, source.Token));
                Assert.Equal(expectedBytes, destination.AsSpan(0, written).ToArray());
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "Destination template " + template);
                var utf8Template = Encoding.UTF8.GetBytes(template);
                var expectedUtf8Template = Encoding.UTF8.GetBytes(Utf8ScalarCaptureFixture.Subject.Restore(oracle.Replace(subject.Projected, Encoding.UTF8.GetString(utf8Template))));
                Assert.Equal(expectedUtf8Template, regex.Replace(input, utf8Template, source.Token));
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "UTF-8 template");
                var utf8Destination = new byte[expectedUtf8Template.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, utf8Template, utf8Destination, out var utf8Written, source.Token));
                Assert.Equal(expectedUtf8Template, utf8Destination.AsSpan(0, utf8Written).ToArray());
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "UTF-8 destination template");
            }
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "Templates");
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }

    [Theory]
    [InlineData("(?<12>a)(?<5>b)", "ab")]
    [InlineData("(?<2147483647>a)(?<x>b)", "ab")]
    [InlineData("(?<12>a)(?<5>b)?", "a")]
    [InlineData("ab", "ab")]
    public void PublicTemplatesRetainPublicNumbersAndLastOrdinal(string pattern, string text)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var match = oracle.Match(text);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        foreach (var template in new[] { "$+", "$12|$5|$2147483647", "$00|$01|$" + "{012}" })
        {
            Assert.Equal(match.Result(template), Encoding.UTF8.GetString(regex.Replace(Encoding.UTF8.GetBytes(text), template)));
            Assert.Equal(Encoding.UTF8.GetBytes(match.Result(template)), regex.Replace(Encoding.UTF8.GetBytes(text), Encoding.UTF8.GetBytes(template)));
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData("(?<12>a)", "a")]
    [InlineData("(?<12>a)?b", "b")]
    [InlineData("(?<2147483647>a)(?<x>b)", "ab")]
    public void CaptureCursorDistinguishesSparseGroupsFromHoles(string pattern, string text)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(text);
        var match = oracle.Match(text);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(match, actual, input, text);
        var numbers = oracle.GetGroupNumbers();
        Assert.Equal(numbers, actual.GroupNumbers.ToArray());
        foreach (var number in new[] { -1, 0, 1, 2, 5, 7, 12, int.MaxValue })
            Assert.Equal(numbers.Contains(number), actual.TryGetGroup(number, out _));
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
    public void InvalidNumbersHaveOwnedConstructionFailures(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options));
            Assert.Equal("pattern", actual.ParamName);
            Assert.Null(Utf8SliceParser.TryParse(pattern, options));
        }
    }

    internal static void AssertValue(Match expected, Utf8CaptureRange actual, Utf8ScalarCaptureFixture.Subject subject)
    {
        Assert.Equal(expected.Success, actual.Success);
        var (start, length) = subject.ByteRange(expected.Index, expected.Length);
        Assert.Equal((start, start + length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
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


}
