using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8WindowByteBoundsTests
{
    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft)]
    public void FoldedAndSupplementaryWindowsKeepOwnedByteOperations(RegexOptions options)
    {
        foreach (var row in new[]
        {
            (Pattern: "(first|second)(?i:k){1,2}last", Text: "!firstKlast!secondkKlast!", First: 12, Second: 14),
            (Pattern: "(first|second)[😀-🙏]{1,2}last", Text: "!first😀last!second😀🙏last!", First: 13, Second: 18),
            (Pattern: "(first|second).{1,2}last", Text: "!first😀last!secondé😀last!", First: 13, Second: 16),
        })
        {
            var regex = new Utf8Regex(row.Pattern, options: options);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
            var input = Encoding.UTF8.GetBytes(row.Text);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(2, regex.Count(input));
            var rtl = (options & RegexOptions.RightToLeft) != 0;
            var match = regex.Match(input);
            Assert.True(match.Success);
            Assert.Equal(rtl ? row.First + 2 : 1, match.StartOffsetInBytes);
            Assert.Equal(rtl ? row.Second : row.First, (match.EndOffsetInBytes - match.StartOffsetInBytes));
            var context = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal(match.StartOffsetInBytes, context.Value.StartOffsetInBytes);
            Assert.Equal((match.EndOffsetInBytes - match.StartOffsetInBytes), (context.Value.EndOffsetInBytes - context.Value.StartOffsetInBytes));
            Assert.Equal(rtl ? "second" : "first", System.Text.Encoding.UTF8.GetString(context.GetGroupMemory(1).Span));
            var enumerator = regex.EnumerateMatches(input);
            var count = 0;
            while (enumerator.MoveNext())
            {
                var second = rtl ? count == 0 : count == 1;
                Assert.Equal(second ? row.First + 2 : 1, enumerator.Current.StartOffsetInBytes);
                Assert.Equal(second ? row.Second : row.First, (enumerator.Current.EndOffsetInBytes - enumerator.Current.StartOffsetInBytes));
                count++;
            }
            Assert.Equal(2, count);
            Assert.Equal("!X!X!", Encoding.UTF8.GetString(regex.Replace(input, "X"u8)));
            var splits = new List<string>();
            foreach (var split in regex.EnumerateSplits(input)) splits.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
            Assert.Equal(new[] { "!", "!", "!" }, splits);
            Assert.False(regex.IsMatch("firstlast"u8));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }
}
