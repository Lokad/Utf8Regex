using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaretPrefixScopeTests
{
    [Theory]
    [InlineData("(?m:^(abc))", "x\nabc")]
    [InlineData("(?m)^((abc))", "x\nabc")]
    [InlineData("(?m:^((abc)))", "abc\nabc")]
    [InlineData("(?m:^)(abc)", "x\nabc")]
    [InlineData("(?m:^(abc))", "😀\nabc")]
    [InlineData("(?m:(?-m:^(abc)))", "x\nabc")]
    [InlineData("(?m:(?-m:^(abc)))", "abc😀")]
    [InlineData("(?i:^(abc))", "ABC")]
    [InlineData("^(abc)", "abc😀")]
    [InlineData("^(abc)", "x\nabc")]
    [InlineData(@"\A(abc)", "abc\nabc")]
    public void ScopedCaretKeepsItsLineBoundaryAcrossPublicOperations(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
            var oracle = new Regex(pattern, options);
            var policy = finite ? new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000, MaxDepth = 128,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
            } : Utf8ResourcePolicy.Unlimited;
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, policy);
            var expected = oracle.Match(text);
            Assert.Equal(expected.Success, regex.IsMatch(input));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            var match = regex.Match(input);
            Assert.Equal(expected.Success, match.Success);
            if (expected.Success)
            {
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), match.IndexInBytes);
                Assert.Equal(expected.Value, regex.MatchDetailed(input).GetValueString());
            }
            var ranges = new List<(int Start, int Length)>();
            foreach (var item in regex.EnumerateMatches(input))
                ranges.Add((item.IndexInBytes, item.LengthInBytes));
            Assert.Equal(oracle.Matches(text).Select(item =>
                (Encoding.UTF8.GetByteCount(text.AsSpan(0, item.Index)), Encoding.UTF8.GetByteCount(item.Value))), ranges);
            Assert.Equal(oracle.Replace(text, "<$1>"), regex.ReplaceToString(input, "<$1>"));
            // EnumerateSplits omits captured delimiters: derive the gaps from
            // oracle matches rather than using Regex.Split's capture insertion.
            var expectedSplits = new List<string>();
            var end = 0;
            foreach (Match item in oracle.Matches(text))
            {
                expectedSplits.Add(text[end..item.Index]);
                end = item.Index + item.Length;
            }
            expectedSplits.Add(text[end..]);
            var splits = new List<string>();
            foreach (var item in regex.EnumerateSplits(input))
                splits.Add(item.GetValueString());
            Assert.Equal(expectedSplits, splits);
        }
    }
}
