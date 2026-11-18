using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ComplementaryClassTests
{
    [Theory]
    [InlineData(@"[^\s\S]")]
    [InlineData(@"[^\u0000-\uDBFF\uDFFF]")]
    [InlineData(@"[^\s\S\u0000-\uDBFF\uDFFF]")]
    public void ConstantEmptyClassesFitFiniteConstructionWorkBeforeCaseExpansion(string emptyClass)
    {
        var construction = new Utf8ConstructionPolicy
        {
            MaxWorkSteps = 20000,
            MaxDepth = 128,
            MaxInstructionCapacity = 1024,
            MaxBytes = 1 << 20
        };
        var invocation = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 10000,
            MaxDepth = 128,
            MaxWorkingBytes = 1 << 20,
            MaxResultBytes = 1 << 20
        };
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.NonBacktracking | RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
            var regex = Utf8Regex.Compile("(?<g>" + emptyClass + "){1000000000}", options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = invocation,
                ConstructionPolicy = construction,
                ConstructionTimeout = Timeout.InfiniteTimeSpan
            }, cancellationToken: default);
            Assert.Equal(0, regex.Count("a😀b"u8));
            Assert.False(Utf8MatchSequence.First(regex.CreateMatchCursor("a😀b"u8.ToArray())).Success);
        }
    }

    [Theory]
    [InlineData(@"[\u0000-\uDBFF\uDFFF-[a]]")]
    [InlineData(@"[\s\S\u0000-\uDBFF\uDFFF-[a]]")]
    public void UniversalRootsKeepTheirCaseFoldedSubtraction(string pattern)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
                RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0);
            var regex = new Utf8Regex(pattern, options: options);
            var ranges = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches("aAé😀\n"u8))
                ranges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
            Assert.Equal(new[] { (2, 2), (4, 4), (8, 1) }, ranges);
        }
    }

    [Theory]
    [InlineData(@"[^\s\S]")]
    [InlineData(@"[^\d\D]")]
    [InlineData(@"[^\p{Lu}\P{Lu}]")]
    [InlineData(@"[^\u0000-\uDBFF\uDFFF]")]
    [InlineData(@"[^\s\S-[a]]")]
    [InlineData(@"[^\s\Sx]")]
    public void ImpossibleClassesDoNotExpandLargeNonBacktrackingRepeats(string emptyClass)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0);
            var required = new Utf8Regex("(?<g>" + emptyClass + "){1000000000,2147483646}", options: options);
            Assert.False(required.IsMatch("a😀b"u8));
            Assert.Equal(0, required.Count("a😀b"u8));
            Assert.InRange(required.Inspection.AvailableOwnedSliceProgram!.Instructions.Length, 1, 32);

            var optional = new Utf8Regex("(?<g>" + emptyClass + "){0,2147483646}", options: options);
            Assert.Equal(4, optional.Count("a😀b"u8));
            Assert.True(Utf8MatchSequence.First(optional.CreateMatchCursor("a😀b"u8.ToArray())).TryGetGroup(1, out var range1));
            Assert.False(range1.Success);

            var alternative = new Utf8Regex("(?<g>" + emptyClass + "){1000000000}|(?<g>😀)", options: options);
            Assert.True(Utf8MatchSequence.First(alternative.CreateMatchCursor("a😀b"u8.ToArray())).TryGetGroup(1, out var group));
            Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(alternative.CreateMatchCursor("a😀b"u8.ToArray())).GetGroupMemory(alternative.GroupNumberFromUtf8Name("g"u8)).Span));
            Assert.Equal((1, 4, 1), (group.StartOffsetInBytes, (group.EndOffsetInBytes - group.StartOffsetInBytes), Utf8MatchSequence.First(alternative.CreateMatchCursor("a😀b"u8.ToArray())).GetCaptures(alternative.GroupNumberFromUtf8Name("g"u8)).Length));
        }
    }

    [Theory]
    [InlineData(@"[\s\S]")]
    [InlineData(@"[\d\D]")]
    [InlineData(@"[\p{Lu}\P{Lu}]")]
    [InlineData(@"[\S\s]")]
    public void ComplementaryPredicatesCoverCompleteScalars(string allScalars)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.IgnoreCase,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexOptions.ECMAScript,
            RegexOptions.NonBacktracking, RegexOptions.NonBacktracking | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(allScalars, options: options);
            Assert.Equal(4, regex.Count("é😀b\n"u8));
            var ranges = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches("é😀b\n"u8))
                ranges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
            Assert.Equal(new[] { (0, 2), (2, 4), (6, 1), (7, 1) }, ranges);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ConsumingRepeatsKeepTheirConstructionBackstop(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0);
        Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex("a{1000000000}", options: options));
    }

    [Fact]
    public void ImpossibleOuterClassesStillValidateSubtractionSyntax()
    {
        Assert.Throws<ArgumentException>(() => new Utf8Regex(@"[^\s\S-[\p{UnknownProperty}]]"));
        Assert.Throws<ArgumentException>(() => new Utf8Regex(@"[^\s\S-[z-a]]"));
    }
}
