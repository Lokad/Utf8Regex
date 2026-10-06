using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8ScalarCultureTests
{
    public static IEnumerable<object[]> CultureLanes()
    {
        foreach (var culture in new[] { "", "en-US", "tr-TR", "az-Latn-AZ" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var scoped in new[] { false, true })
            yield return [culture, compiled, rtl, scoped];
    }

    [Theory]
    [MemberData(nameof(CultureLanes))]
    public void BmpCultureOperationsRetainConstructionCulture(string culture, bool compiled, bool rtl, bool scoped)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            string[] patterns = ["i", "I", "\u0130", "\u0131", "[i]", "[^I]", "[I-i]", "[^I-\u00ff]",
                @"[\p{IsBasicLatin}-[i]]", @"(?<x>i)\k<x>", "(?<=i)i", "(?!I)i", "(?(?=I)I|i)", @"\111+", "(?-i:I)i"];
            string[] subjects = ["I", "i", "\u0130", "\u0131", "Ii", "I\u0131", "\u0130i", "\u0130\u0130", "\u0131I", "iI", "a"];
            using var source = new CancellationTokenSource();
            Span<byte> destination = stackalloc byte[64];
            foreach (var body in patterns)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var pattern = scoped ? "(?i:" + body + ")" : body;
                var options = Options(compiled, rtl, scoped);
                var oracle = new Regex(pattern, options);
                var regex = new Utf8Regex(pattern, options);
                Assert.NotNull(regex.Inspection.AvailableOwnedSliceProgram);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
                foreach (var text in subjects)
                {
                    var input = Encoding.UTF8.GetBytes(text);
                    Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
                    Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, source.Token));
                    Assert.Equal(oracle.Count(text), regex.Count(input));
                    var wanted = oracle.Match(text);
                    var found = regex.MatchDetailed(input);
                    Assert.Equal(wanted.Success, found.Success);
                    if (wanted.Success)
                    {
                        Assert.Equal((Encoding.UTF8.GetByteCount(text.AsSpan(0, wanted.Index)), Encoding.UTF8.GetByteCount(wanted.Value)),
                            (found.IndexInBytes, found.LengthInBytes));
                        Assert.Equal((wanted.Index, wanted.Length), (found.IndexInUtf16, found.LengthInUtf16));
                        foreach (var number in oracle.GetGroupNumbers())
                        {
                            var expectedGroup = wanted.Groups[number];
                            var group = found.GetGroup(number);
                            Assert.Equal(expectedGroup.Success, group.Success);
                            if (group.Success) Assert.Equal(expectedGroup.Value, group.GetValueString());
                        }
                    }
                    var positions = new List<(int Index, int Length)>();
                    foreach (var match in regex.EnumerateMatches(input)) positions.Add((match.IndexInUtf16, match.LengthInUtf16));
                    Assert.True(oracle.Matches(text).Select(m => (m.Index, m.Length)).SequenceEqual(positions),
                        $"Cursor ranges for pattern {pattern}, subject {text}, culture {culture}, options {options}");
                    var expectedSplits = new List<string>();
                    foreach (var range in oracle.EnumerateSplits(text, int.MaxValue)) expectedSplits.Add(text[range]);
                    var splits = new List<string>();
                    foreach (var split in regex.EnumerateSplits(input, int.MaxValue)) splits.Add(split.GetValueString());
                    Assert.Equal(expectedSplits, splits);
                    var output = oracle.Replace(text, "[$&]");
                    Assert.Equal(output, regex.ReplaceToString(input, "[$&]"));
                    Assert.Equal(Encoding.UTF8.GetBytes(output), regex.Replace(input, "[$&]"));
                    Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "[$&]", destination, out var written));
                    Assert.Equal(Encoding.UTF8.GetBytes(output), destination[..written].ToArray());
                    AssertOwned(regex);
                }
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [MemberData(nameof(CultureLanes))]
    public void SupplementaryOperationsUseCompleteScalarsWithCultureFolding(string culture, bool compiled, bool rtl, bool scoped)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            using var source = new CancellationTokenSource();
            foreach (var body in new[] { "i(.)", "i([\U0001f600])", "i(\U0001f600)", @"i(\p{So})", "i(.)(.)", "i((?:\U0001f600)+)" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var prefix = culture is "tr-TR" or "az-Latn-AZ" ? "\u0130" : "I";
                var text = prefix + "\U0001f600\U0001f600";
                var input = Encoding.UTF8.GetBytes(text);
                var regex = new Utf8Regex(scoped ? "(?i:" + body + ")" : body, Options(compiled, rtl, scoped));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
                var all = body.Contains('+') || body == "i(.)(.)";
                var length = Encoding.UTF8.GetByteCount(prefix) + (all ? 8 : 4);
                var value = prefix + "\U0001f600" + (all ? "\U0001f600" : "");
                Assert.True(regex.IsMatch(input, source.Token));
                Assert.Equal(1, regex.Count(input, source.Token));
                Assert.Equal(length, regex.Match(input).LengthInBytes);
                var found = regex.MatchDetailed(input, source.Token);
                Assert.Equal((0, length, 0, all ? 5 : 3), (found.IndexInBytes, found.LengthInBytes, found.IndexInUtf16, found.LengthInUtf16));
                Assert.Equal(value, found.GetValueString());
                Assert.Equal(body.Contains('+') ? "\U0001f600\U0001f600" : "\U0001f600", found.GetGroup(1).GetValueString());
                var cursor = regex.EnumerateMatches(input, source.Token);
                Assert.True(cursor.MoveNext());
                Assert.Equal(length, cursor.Current.LengthInBytes);
                Assert.False(cursor.MoveNext());
                var replacement = "<" + value + ">" + (all ? "" : "\U0001f600");
                Assert.Equal(replacement, regex.ReplaceToString(input, "<$&>", source.Token));
                Assert.Equal(Encoding.UTF8.GetBytes(replacement), regex.Replace(input, "<$&>", source.Token));
                Assert.Equal((body.Contains('+') ? "\U0001f600\U0001f600" : "\U0001f600") + (all ? "" : "\U0001f600"),
                    regex.ReplaceToString(input, 0,
                        static (in Utf8MatchContext m, ref int state) => m.GetGroup(1).GetValueString(), source.Token));
                var splits = new List<string>();
                foreach (var split in regex.EnumerateSplits(input, int.MaxValue, source.Token)) splits.Add(split.GetValueString());
                Assert.Equal(rtl ? new[] { all ? "" : "\U0001f600", "" } : new[] { "", all ? "" : "\U0001f600" }, splits);
                AssertOwned(regex);
            }

            foreach (var atom in new[] { "\U00010400", "[\U00010400]", "[\U00010400-\U00010401]" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var prefix = culture is "tr-TR" or "az-Latn-AZ" ? "\u0130" : "I";
                var body = "i(" + atom + ")";
                var regex = new Utf8Regex(scoped ? "(?i:" + body + ")" : body, Options(compiled, rtl, scoped));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
                var input = Encoding.UTF8.GetBytes(prefix + "\U00010428");
                var found = regex.MatchDetailed(input, source.Token);
                Assert.Equal(input.Length, found.LengthInBytes);
                Assert.Equal("\U00010428", found.GetGroup(1).GetValueString());
                Assert.Equal(1, regex.Count(input, source.Token));
                Assert.Equal("\U00010428", regex.ReplaceToString(input, "$1", source.Token));
                AssertOwned(regex);
            }

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var referenceBody = rtl ? "i\\k<x>(?<x>\U00010400)" : "i(?<x>\U00010400)\\k<x>";
            var referenceRegex = new Utf8Regex(scoped ? "(?i:" + referenceBody + ")" : referenceBody, Options(compiled, rtl, scoped));
            var referencePrefix = culture is "tr-TR" or "az-Latn-AZ" ? "\u0130" : "I";
            var referenceInput = Encoding.UTF8.GetBytes(referencePrefix +
                (rtl ? "\U00010400\U00010428" : "\U00010428\U00010400"));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
            var referenceMatch = referenceRegex.MatchDetailed(referenceInput, source.Token);
            Assert.Equal(referenceInput.Length, referenceMatch.LengthInBytes);
            Assert.Equal("\U00010428", referenceMatch.GetGroup("x").GetValueString());
            AssertOwned(referenceRegex);
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [InlineData("en-US", false)]
    [InlineData("en-US", true)]
    [InlineData("tr-TR", false)]
    [InlineData("tr-TR", true)]
    public void CultureInvariantAndFlatNonBacktrackingKeepOwnedExecution(string culture, bool compiled)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (var invariant in new[] { false, true })
            {
                var options = Options(compiled, false, false) | RegexOptions.NonBacktracking |
                    (invariant ? RegexOptions.CultureInvariant : RegexOptions.None);
                var regex = new Utf8Regex("i(.)", options);
                var prefix = culture == "tr-TR" && !invariant ? "\u0130" : "I";
                var input = Encoding.UTF8.GetBytes(prefix + "\U0001f600");
                AssertOwned(regex);
                var found = regex.MatchDetailed(input);
                Assert.Equal(input.Length, found.LengthInBytes);
                Assert.Equal("\U0001f600", found.GetGroup(1).GetValueString());
                Assert.Equal(!invariant && culture == "en-US",
                    regex.IsMatch(Encoding.UTF8.GetBytes((prefix == "I" ? "\u0130" : "I") + "\U0001f600")));
                AssertOwned(regex);
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [InlineData("en-US", "tr-TR")]
    [InlineData("tr-TR", "en-US")]
    public void StaticScopedCultureCacheUsesScalarOperationsInBothOrders(string first, string second)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            Utf8RegexCache.ResetForTests();
            foreach (var culture in new[] { first, second })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var prefix = culture == "tr-TR" ? "\u0130" : "I";
                var input = Encoding.UTF8.GetBytes(prefix + "\U0001f600");
                Assert.Equal(input.Length, Utf8Regex.Match(input, "(?i:i(.))", RegexOptions.None).LengthInBytes);
                Assert.Equal(1, Utf8Regex.Count(input, "(?i:i(.))", RegexOptions.None));
                Assert.Equal("<\U0001f600>", Encoding.UTF8.GetString(Utf8Regex.Replace(input, "(?i:i(.))", "<$1>", RegexOptions.None)));
                AssertOwned(Utf8RegexCache.GetOrAdd("(?i:i(.))", RegexOptions.None));
            }
        }
        finally { Utf8RegexCache.ResetForTests(); CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [InlineData("en-US", false, false)]
    [InlineData("en-US", false, true)]
    [InlineData("en-US", true, false)]
    [InlineData("en-US", true, true)]
    [InlineData("tr-TR", false, false)]
    [InlineData("tr-TR", false, true)]
    [InlineData("tr-TR", true, false)]
    [InlineData("tr-TR", true, true)]
    public void PreparedAdapterRoutesRetainCultureAndWholeScalarRanges(string culture, bool compiled, bool scoped)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var prefix = culture == "tr-TR" ? "\u0130" : "I";
            var regex = new Utf8Regex(scoped ? "(?i:i(.))" : "i(.)", Options(compiled, false, scoped));
            var firstSegment = "a" + prefix + "\U0001f600b";
            var input = Utf8ValidatedInput.Create(Encoding.UTF8.GetBytes(firstSegment + prefix + "\U0001f600"));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
            var zero = input.GetBytePosition(0, "start");
            Assert.True(regex.ByteOffsetExecution.TryIsMatchPrepared(input, zero, out var hit));
            Assert.True(hit);
            Assert.Equal(2, regex.ByteOffsetExecution.CountPrepared(input, zero));
            var second = input.GetBytePosition(Encoding.UTF8.GetByteCount(firstSegment), "start");
            Assert.Equal(1, regex.ByteOffsetExecution.CountPrepared(input, second));
            var found = regex.ByteOffsetExecution.MatchDetailed(input, second);
            Assert.Equal((second.Value, Encoding.UTF8.GetByteCount(prefix) + 4), (found.IndexInBytes, found.LengthInBytes));
            Assert.Equal("\U0001f600", found.GetGroup(1).GetValueString());
            AssertOwned(regex);
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    private static RegexOptions Options(bool compiled, bool rtl, bool scoped) =>
        (compiled ? RegexOptions.Compiled : RegexOptions.None) |
        (rtl ? RegexOptions.RightToLeft : RegexOptions.None) |
        (scoped ? RegexOptions.None : RegexOptions.IgnoreCase);

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
