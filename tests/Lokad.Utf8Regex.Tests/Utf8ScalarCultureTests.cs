using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

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
                var regex = new Utf8Regex(pattern, options: options);
                Assert.NotNull(regex.Inspection.AvailableOwnedSliceProgram);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
                foreach (var text in subjects)
                {
                    var input = Encoding.UTF8.GetBytes(text);
                    Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
                    Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, source.Token));
                    Assert.Equal(oracle.Count(text), regex.Count(input));
                    var wanted = oracle.Match(text);
                    var found = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                    Assert.Equal(wanted.Success, found.Success);
                    if (wanted.Success)
                    {
                        Assert.Equal((Encoding.UTF8.GetByteCount(text.AsSpan(0, wanted.Index)), Encoding.UTF8.GetByteCount(wanted.Value)),
                            (found.Value.StartOffsetInBytes, (found.Value.EndOffsetInBytes - found.Value.StartOffsetInBytes)));
                        foreach (var number in oracle.GetGroupNumbers())
                        {
                            var expectedGroup = wanted.Groups[number];
                            Assert.True(found.TryGetGroup(number, out var group));
                            Assert.Equal(expectedGroup.Success, group.Success);
                            if (group.Success) Assert.Equal(expectedGroup.Value, System.Text.Encoding.UTF8.GetString(found.GetGroupMemory(number).Span));
                        }
                    }
                    var positions = new List<(int Index, int Length)>();
                    foreach (var match in regex.EnumerateMatches(input)) positions.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
                    Assert.True(oracle.Matches(text).Select(m => (System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, m.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(m.Index, m.Length)))).SequenceEqual(positions),
                        $"Cursor ranges for pattern {pattern}, subject {text}, culture {culture}, options {options}");
                    var expectedSplits = new List<string>();
                    foreach (var range in oracle.EnumerateSplits(text, int.MaxValue)) expectedSplits.Add(text[range]);
                    var splits = new List<string>();
                    foreach (var split in regex.EnumerateSplits(input, int.MaxValue)) splits.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
                    Assert.Equal(expectedSplits, splits);
                    var output = oracle.Replace(text, "[$&]");
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
                var regex = new Utf8Regex(scoped ? "(?i:" + body + ")" : body, options: Options(compiled, rtl, scoped));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
                var all = body.Contains('+') || body == "i(.)(.)";
                var length = Encoding.UTF8.GetByteCount(prefix) + (all ? 8 : 4);
                var value = prefix + "\U0001f600" + (all ? "\U0001f600" : "");
                Assert.True(regex.IsMatch(input, source.Token));
                Assert.Equal(1, regex.Count(input, source.Token));
                Assert.Equal(length, (regex.Match(input).EndOffsetInBytes - regex.Match(input).StartOffsetInBytes));
                var found = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: source.Token));
                Assert.Equal((0, length), (found.Value.StartOffsetInBytes, found.Value.EndOffsetInBytes));
                Assert.Equal(value, System.Text.Encoding.UTF8.GetString(found.GetValueMemory().Span));
                Assert.Equal(body.Contains('+') ? "\U0001f600\U0001f600" : "\U0001f600", System.Text.Encoding.UTF8.GetString(found.GetGroupMemory(1).Span));
                var cursor = regex.EnumerateMatches(input, cancellationToken: source.Token);
                Assert.True(cursor.MoveNext());
                Assert.Equal(length, (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes));
                Assert.False(cursor.MoveNext());
                var replacement = "<" + value + ">" + (all ? "" : "\U0001f600");
                Assert.Equal(Encoding.UTF8.GetBytes(replacement), regex.Replace(input, "<$&>", source.Token));
                var splits = new List<string>();
                foreach (var split in regex.EnumerateSplits(input, int.MaxValue, source.Token)) splits.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
                Assert.Equal(rtl ? new[] { all ? "" : "\U0001f600", "" } : new[] { "", all ? "" : "\U0001f600" }, splits);
                AssertOwned(regex);
            }

            foreach (var atom in new[] { "\U00010400", "[\U00010400]", "[\U00010400-\U00010401]" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var prefix = culture is "tr-TR" or "az-Latn-AZ" ? "\u0130" : "I";
                var body = "i(" + atom + ")";
                var regex = new Utf8Regex(scoped ? "(?i:" + body + ")" : body, options: Options(compiled, rtl, scoped));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
                var input = Encoding.UTF8.GetBytes(prefix + "\U00010428");
                var found = Utf8MatchSequence.First(regex.CreateMatchCursor(input, cancellationToken: source.Token));
                Assert.Equal((0, input.Length), (found.Value.StartOffsetInBytes, found.Value.EndOffsetInBytes));
                Assert.Equal(input.Length, (found.Value.EndOffsetInBytes - found.Value.StartOffsetInBytes));
                Assert.Equal("\U00010428", System.Text.Encoding.UTF8.GetString(found.GetGroupMemory(1).Span));
                Assert.Equal(1, regex.Count(input, source.Token));
                Assert.Equal("\U00010428", Encoding.UTF8.GetString(regex.Replace(input, "$1", source.Token)));
                AssertOwned(regex);
            }

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var referenceBody = rtl ? "i\\k<x>(?<x>\U00010400)" : "i(?<x>\U00010400)\\k<x>";
            var referenceRegex = new Utf8Regex(scoped ? "(?i:" + referenceBody + ")" : referenceBody, options: Options(compiled, rtl, scoped));
            var referencePrefix = culture is "tr-TR" or "az-Latn-AZ" ? "\u0130" : "I";
            var referenceInput = Encoding.UTF8.GetBytes(referencePrefix +
                (rtl ? "\U00010400\U00010428" : "\U00010428\U00010400"));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
            var referenceMatch = Utf8MatchSequence.First(referenceRegex.CreateMatchCursor(referenceInput, cancellationToken: source.Token));
            Assert.Equal(referenceInput.Length, (referenceMatch.Value.EndOffsetInBytes - referenceMatch.Value.StartOffsetInBytes));
            Assert.Equal("\U00010428", System.Text.Encoding.UTF8.GetString(referenceMatch.GetGroupMemory(referenceRegex.GroupNumberFromUtf8Name("x"u8)).Span));
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
                var regex = new Utf8Regex("i(.)", options: options);
                var prefix = culture == "tr-TR" && !invariant ? "\u0130" : "I";
                var input = Encoding.UTF8.GetBytes(prefix + "\U0001f600");
                AssertOwned(regex);
                var found = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                Assert.Equal(input.Length, (found.Value.EndOffsetInBytes - found.Value.StartOffsetInBytes));
                Assert.Equal("\U0001f600", System.Text.Encoding.UTF8.GetString(found.GetGroupMemory(1).Span));
                Assert.Equal(!invariant && culture == "en-US",
                    regex.IsMatch(Encoding.UTF8.GetBytes((prefix == "I" ? "\u0130" : "I") + "\U0001f600")));
                AssertOwned(regex);
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }
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
            var regex = new Utf8Regex(scoped ? "(?i:i(.))" : "i(.)", options: Options(compiled, false, scoped));
            var firstSegment = "a" + prefix + "\U0001f600b";
            var input = Utf8ValidatedInput.Create(Encoding.UTF8.GetBytes(firstSegment + prefix + "\U0001f600"));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
            var zero = input.GetBytePosition(0, "start");
            Assert.True(regex.ByteOffsetExecution.IsMatchPrepared(input.Bytes));
            Assert.Equal(2, regex.ByteOffsetExecution.CountPrepared(input, zero));
            var second = input.GetBytePosition(Encoding.UTF8.GetByteCount(firstSegment), "start");
            Assert.Equal(1, regex.ByteOffsetExecution.CountPrepared(input, second));
            var found = Utf8MatchSequence.First(regex.CreateMatchCursor(input.Bytes.ToArray(), second.Value));
            Assert.Equal((second.Value, input.Bytes.Length), (found.Value.StartOffsetInBytes, found.Value.EndOffsetInBytes));
            Assert.Equal("😀"u8.ToArray(), found.GetGroupMemory(1).ToArray());
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
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
