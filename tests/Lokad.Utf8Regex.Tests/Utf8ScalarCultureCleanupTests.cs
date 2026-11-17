using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarCultureCleanupTests
{
    public static IEnumerable<object[]> UrlAndCellLanes()
    {
        foreach (var patternId in new[] { 0, 1, 2 })
        foreach (var culture in new[] { "en-US", "tr-TR" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [patternId, culture, compiled, rtl];
    }

    [Theory]
    [MemberData(nameof(UrlAndCellLanes))]
    public void AdmittedCellAndUrlFamiliesUseConstructionBoundScalarCaptures(int patternId, string culture, bool compiled, bool rtl)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var pattern = patternId switch
            {
                0 => @"^(?<col>[a-z])(?<row>(\d)+)$",
                1 => @"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\d+)/?\?t=(?<tab>[^ ?]+)(?<rest>.*)$",
                _ => @"^(?<before>.*)(https://go(\.testing)?\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \n]+)?(?<rest>.*)$",
            };
            var text = patternId switch
            {
                0 => "\u013023",
                1 => "\U0001f600https://go.lokad.com/d/42?t=\U0001f600",
                _ => "\U0001f600https://go.lokad.com/gateway/BigFiles/Browse/Download?hash=AB&name=\U0001f600",
            };
            var options = RegexOptions.IgnoreCase | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options);
            Assert.NotNull(regex.Inspection.AvailableOwnedSliceProgram);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
            var input = Encoding.UTF8.GetBytes(text);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(1, regex.Count(input));
            Assert.Equal(input.Length, (regex.Match(input).EndOffsetInBytes - regex.Match(input).StartOffsetInBytes));
            var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal((0, input.Length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(detailed.GetValueMemory().Span));
            Assert.Equal(patternId == 0 ? "\u0130" : "\U0001f600",
                System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(regex.GroupNumberFromName(patternId == 0 ? "col" : "before")).Span));
            Assert.Equal(patternId == 0 ? "23" : "\U0001f600",
                System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(regex.GroupNumberFromName(patternId == 0 ? "row" : patternId == 1 ? "tab" : "name")).Span));
            Assert.Equal("<" + text + ">", Encoding.UTF8.GetString(regex.Replace(input, "<$&>")));
            var cursor = regex.EnumerateMatches(input);
            Assert.True(cursor.MoveNext());
            Assert.Equal(input.Length, (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes));
            Assert.False(cursor.MoveNext());
            var splits = new List<string>();
            foreach (var split in regex.EnumerateSplits(input)) splits.Add(split.GetValueString());
            Assert.Equal(new[] { "", "" }, splits);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [InlineData("en-US", false, false)]
    [InlineData("en-US", true, false)]
    [InlineData("en-US", false, true)]
    [InlineData("en-US", true, true)]
    [InlineData("tr-TR", false, false)]
    [InlineData("tr-TR", true, false)]
    [InlineData("tr-TR", false, true)]
    [InlineData("tr-TR", true, true)]
    public void OwnedNonBacktrackingLoopsRetainTheirConstructionCultureOnBmpOverlap(string culture, bool compiled, bool scoped)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var pattern = scoped ? "(?i:(?<x>\u0130)+)" : "(?<x>\u0130)+";
            var options = RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (scoped ? RegexOptions.None : RegexOptions.IgnoreCase);
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.AvailableOwnedSliceProgram?.IsNonBacktracking);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture == "tr-TR" ? "en-US" : "tr-TR");
            foreach (var text in new[] { "i", "I", "\u0130", "\u0131", "iii", "aIai" })
            {
                var input = Encoding.UTF8.GetBytes(text);
                Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
                Assert.Equal(oracle.Count(text), regex.Count(input));
                var wanted = oracle.Match(text);
                var found = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                Assert.Equal(wanted.Success, found.Success);
                if (wanted.Success) Assert.Equal(wanted.Groups["x"].Value, System.Text.Encoding.UTF8.GetString(found.GetGroupMemory(regex.GroupNumberFromName("x")).Span));
                Assert.Equal(oracle.Replace(text, "<$1>"), Encoding.UTF8.GetString(regex.Replace(input, "<$1>")));
                var positions = new List<(int, int)>();
                foreach (var match in regex.EnumerateMatches(input)) positions.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
                Assert.Equal(oracle.Matches(text).Select(m => (System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, m.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(m.Index, m.Length)))), positions);
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }
}
