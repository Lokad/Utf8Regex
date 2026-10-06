using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeepScalarAdmissionTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var depth in new[] { 65, 257, 4097 })
            foreach (var family in new[] { "capture", "scope", "atomic", "ahead", "behind", "conditional" })
                foreach (var compiled in new[] { false, true })
                    foreach (var rtl in new[] { false, true })
                        yield return [depth, family, compiled, rtl];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeepGrammarUsesCompleteScalarCapturesAcrossPublicOperations(int depth, string family, bool compiled, bool rtl)
    {
        var opener = family switch
        {
            "capture" => "(",
            "scope" => "(?i:",
            "atomic" => "(?>",
            "ahead" => "(?=",
            "behind" => "(?<=",
            _ => rtl ? "(?(?<=.)" : "(?(?=.)",
        };
        var pattern = string.Concat(Enumerable.Repeat(opener, depth)) + "(?<leaf>.)" +
            string.Concat(Enumerable.Repeat(family == "conditional" ? "|x)" : ")", depth)) +
            (family == "ahead" ? "." : family == "behind" ? "x" : "");
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options);
        Assert.NotNull(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        var text = family == "behind" ? "\U0001f600x" : "\U0001f600";
        var input = Encoding.UTF8.GetBytes(text);
        var index = family == "behind" ? 4 : 0;
        var length = family == "behind" ? 1 : 4;
        Assert.True(regex.IsMatch(input));
        Assert.False(regex.IsMatch(""u8));
        Assert.Equal(1, regex.Count(input));
        Assert.Equal((index, length), (regex.Match(input).IndexInBytes, regex.Match(input).LengthInBytes));
        var detailed = regex.MatchDetailed(input);
        Assert.Equal((index, length), (detailed.IndexInBytes, detailed.LengthInBytes));
        var leaf = detailed.GetGroup("leaf");
        Assert.Equal((0, 4), (leaf.IndexInBytes, leaf.LengthInBytes));
        Assert.Equal("\U0001f600", leaf.GetValueString());
        Assert.Equal(family == "behind" ? "\U0001f600<\U0001f600>" : "<\U0001f600>",
            regex.ReplaceToString(input, "<${leaf}>"));
        var cursor = regex.EnumerateMatches(input);
        Assert.True(cursor.MoveNext());
        Assert.Equal((index, length), (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
        Assert.False(cursor.MoveNext());
        var splits = new List<string>();
        foreach (var split in regex.EnumerateSplits(input)) splits.Add(split.GetValueString());
        Assert.Equal(family == "behind" ? rtl ? new[] { "", "\U0001f600" } : new[] { "\U0001f600", "" }
            : new[] { "", "" }, splits);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
