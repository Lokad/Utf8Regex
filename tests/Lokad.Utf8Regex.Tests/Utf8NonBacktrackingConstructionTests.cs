using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NonBacktrackingConstructionTests
{
    public static IEnumerable<object[]> ZeroWidthCases()
    {
        string[] patterns =
        [
            "(?<x>){3}", "(?<x>){0,4}", "(?<x>){0,4}?",
            "(?<x>^|$){2,4}", "(?<x>\\b){3}",
            "(?:(?<x>^)?(?<y>$)?){2,5}", "(?:(?<x>^)|(?<y>$)){0,3}",
            "(?:(?<x>)|(?<y>)){2,4}", "(?<x>\ud800){2,4}", "(?<x>\ud800){0,4}",
            "(?:(?<x>()){3}){2,4}", "(?<x>a{0}){2,4}",
        ];
        foreach (var pattern in patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var text in new[] { "", "a😀b\nc" })
            yield return [pattern, text, compiled];
    }

    [Theory]
    [MemberData(nameof(ZeroWidthCases))]
    public void CompactedZeroWidthRepeatsPreservePredicatesAndLastCapture(string pattern, string text, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        // The ordinary owned scalar engine retains every completion. Compare
        // its last value/range, while NonBacktracking exposes one completion.
        var ordinary = new Utf8Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options | RegexOptions.NonBacktracking);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(ordinary.IsMatch(input), regex.IsMatch(input));
        Assert.Equal(ordinary.Count(input), regex.Count(input));
        var expected = ordinary.MatchDetailed(input);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected.Success, actual.Success);
        if (actual.Success)
        {
            Assert.Equal((expected.IndexInBytes, expected.LengthInBytes), (actual.IndexInBytes, actual.LengthInBytes));
            foreach (var number in ordinary.GetGroupNumbers())
            {
                var wanted = expected.GetGroup(number);
                var found = actual.GetGroup(number);
                Assert.Equal(wanted.Success, found.Success);
                Assert.Equal(wanted.Success ? 1 : 0, found.CaptureCount);
                if (found.Success)
                {
                    Assert.Equal((wanted.IndexInBytes, wanted.LengthInBytes), (found.IndexInBytes, found.LengthInBytes));
                    Assert.Equal(wanted.GetValueString(), found.GetValueString());
                }
            }
        }
        Assert.Equal(ordinary.ReplaceToString(input, "<$0:${x}>"), regex.ReplaceToString(input, "<$0:${x}>"));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ordinary.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NumericZeroWidthBoundsDoNotGrowThePreparedGraph(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var small = new Utf8Regex("(?<x>){3}", options);
        var modest = new Utf8Regex("(?<x>){512}", options);
        Assert.Equal(small.Inspection.AvailableOwnedSliceProgram?.Instructions.Length,
            modest.Inspection.AvailableOwnedSliceProgram?.Instructions.Length);
        // This modest structural assertion must pass before the huge witness;
        // restoring old expansion fails promptly without running a billion steps.
        var huge = new Utf8Regex("(?<x>){1000000000,2147483646}", options);
        Assert.Equal(small.Inspection.AvailableOwnedSliceProgram?.Instructions.Length,
            huge.Inspection.AvailableOwnedSliceProgram?.Instructions.Length);
        Assert.Equal(2, huge.Count("😀"u8));
        var group = huge.MatchDetailed("😀"u8).GetGroup("x");
        Assert.True(group.Success);
        Assert.Equal(1, group.CaptureCount);
        Assert.Equal((0, 0), (group.IndexInBytes, group.LengthInBytes));
        Assert.Equal("Q😀Q", huge.ReplaceToString("😀"u8, "Q"));
        Assert.True(huge.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NestedEmptyLoopsHaveLinearGraphSize(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0);
        var pattern = "(?<x>)";
        foreach (var depth in new[] { 8, 32, 128, 512 })
        {
            pattern = "(?<x>)";
            for (var index = 0; index < depth; index++) pattern = "(?:" + pattern + ")*";
            var regex = new Utf8Regex(pattern, options);
            var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Owned graph missing.");
            Assert.InRange(program.Instructions.Length, 1, depth + 4);
            Assert.Equal(2, regex.Count("😀"u8));
            Assert.True(regex.MatchDetailed("😀"u8).GetGroup("x").Success);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NestedNullableLoopsShareProgressInsteadOfCopyingWholeBodies(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0);
        foreach (var depth in new[] { 8, 16, 32, 64 })
        {
            var pattern = "(?<x>a*)";
            for (var index = 0; index < depth; index++) pattern = "(?:" + pattern + ")*";
            pattern += "b";
            var regex = new Utf8Regex(pattern, options);
            var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException("Owned graph missing.");
            Assert.InRange(program.Instructions.Length, 1, 8 * (depth + 1) * (depth + 1));
            // The smallest graph assertion guards deeper witnesses against the
            // former exponential construction when running baseline controls.
            Assert.True(regex.IsMatch("aaab"u8));
            Assert.False(regex.IsMatch("aaaa"u8));
            Assert.Equal(1, regex.Count("aaab"u8));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    public static IEnumerable<object[]> NullablePriorityCases()
    {
        (string Pattern, string Text)[] cases =
        [
            ("(?:.*)*?b", "aaabb"), ("(?:.*?)*b", "aaabb"), ("(?:.*)*b", "aaabb"),
            ("(?:(?<x>a?)*)*b", "aaab"), ("(?:(?<x>a*))*b", "aaab"),
            ("(?:(?<x>a*?))*b", "aaab"), ("(?:(?<x>a*))*?b", "aaab"),
            ("(?:(?<x>a|)*)*b", "aaab"), ("(?:(?<x>a*)){2,4}b", "aaab"),
            ("(?:(?<x>a*)){2,4}?b", "aaab"), ("(?:(?<x>😀?)*)*b", "😀😀b"),
            ("(?:(?<x>a*))*b|", "aaa"), ("(?:(?<x>a*?))*?b|", "aaa"),
        ];
        foreach (var row in cases)
        foreach (var compiled in new[] { false, true }) yield return [row.Pattern, row.Text, compiled];
    }

    [Theory]
    [MemberData(nameof(NullablePriorityCases))]
    public void ProgressStatesPreserveNullablePriorityAndLastCaptures(string pattern, string text, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var ordinary = new Utf8Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options | RegexOptions.NonBacktracking);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(ordinary.Count(input), regex.Count(input));
        var expected = ordinary.MatchDetailed(input);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal((expected.IndexInBytes, expected.LengthInBytes), (actual.IndexInBytes, actual.LengthInBytes));
        foreach (var number in ordinary.GetGroupNumbers())
        {
            var wanted = expected.GetGroup(number); var found = actual.GetGroup(number);
            Assert.Equal(wanted.Success, found.Success);
            Assert.Equal(wanted.Success ? 1 : 0, found.CaptureCount);
            if (found.Success) Assert.Equal(wanted.GetValueString(), found.GetValueString());
        }
        Assert.Equal(ordinary.ReplaceToString(input, "<$0:$1>"), regex.ReplaceToString(input, "<$0:$1>"));
        if (!text.Any(char.IsSurrogate))
        {
            // The interpreted comparator avoids the known compiled finite-lazy
            // capture defect. These subjects are ASCII, so its ranges overlap.
            var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            var wanted = oracle.Match(text);
            Assert.Equal((wanted.Index, wanted.Length), (actual.IndexInBytes, actual.LengthInBytes));
            foreach (var number in oracle.GetGroupNumbers())
                Assert.Equal(wanted.Groups[number].Value, actual.GetGroup(number).GetValueString());
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static IEnumerable<object[]> UnsupportedCases()
    {
        string[] patterns =
        [
            "(a)\\1", "(?<n>a)\\k<n>", "(?<=a)b", "(?<!a)b", "a(?=b)", "a(?!b)",
            "(?<o>a)(?<-o>b)", "(?>(a+))", "(a)?(?(1)b|c)", "(?(?=a)a|b)", "\\Ga",
        ];
        foreach (var pattern in patterns)
        foreach (var compiled in new[] { false, true }) yield return [pattern, compiled];
    }

    [Theory]
    [MemberData(nameof(UnsupportedCases))]
    public void GrammarRejectionOccursBeforePreparation(string pattern, bool compiled)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        Assert.NotNull(Utf8OwnedTreeParser.TryParse(pattern, options));
        Assert.Throws<NotSupportedException>(() => Utf8FrontEnd.Analyze(pattern, options));
        var error = Assert.Throws<NotSupportedException>(() => new Utf8Regex(pattern, options));
        Assert.StartsWith("NonBacktracking does not support ", error.Message);
    }
}
