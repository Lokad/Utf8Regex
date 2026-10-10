using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ProjectedClassScalarLoopTests
{
    public static IEnumerable<object[]> ProjectionCases()
    {
        foreach (var category in new[] { @"\W", @"\D" })
        foreach (var shape in new[] { 0, 1, 2 })
        foreach (var compiled in new[] { false, true }) yield return [category, shape, compiled];
    }

    private static string Pattern(string category, int shape, bool capture)
    {
        var body = category + (shape == 0 ? "{0,}" : "{2,}");
        if (capture) body = "(?<x>" + body + ")";
        return shape == 2 ? "^a" + body + "7$" : "^" + body + "$";
    }

    [Theory]
    [MemberData(nameof(ProjectionCases))]
    public void AsciiProjectionAdmitsUnboundedLoopsAndDefersNonAscii(string category, int shape, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var analysis = Utf8FrontEnd.Compile(Pattern(category, shape, false), options);
        Assert.NotNull(analysis.ExecutionTree);
        var outcome = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree);
        Assert.True(outcome.Succeeded);
        Assert.Contains(outcome.Program.Steps, static step => step.Kind == Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet);
        foreach (var body in new[] { "", "?", "??", "???" })
        {
            var input = Encoding.UTF8.GetBytes(shape == 2 ? "a" + body + "7" : body);
            var hit = outcome.Program.TryMatch(input, 0, out var length, out var defer);
            Assert.False(defer);
            Assert.Equal(shape == 0 || body.Length >= 2, hit);
            if (hit) Assert.Equal(input.Length, length);
        }
        foreach (var body in new[] { "😀", "😀😀", "?😀", "😀?" })
        {
            var input = Encoding.UTF8.GetBytes(shape == 2 ? "a" + body + "7" : body);
            Assert.False(outcome.Program.TryMatch(input, 0, out _, out var defer));
            Assert.True(defer); // The dispatcher must run the owned scalar engine.
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OverlappingSuffixStillDeclinesTheDeterministicProjection(bool compiled)
    {
        const string pattern = @"^a\D{2,}b$";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var analysis = Utf8FrontEnd.Compile(pattern, options);
        Assert.NotNull(analysis.ExecutionTree);
        var outcome = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree);
        Assert.False(outcome.Succeeded);
        Assert.Equal(Utf8ByteSafeLinearCompileFailureKind.NonDisjointVariableLoop, outcome.FailureKind);
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.IsMatch("a??b"u8));
        Assert.False(regex.IsMatch("a😀b"u8));
        Assert.Equal(10, regex.Match("a😀😀b"u8).LengthInBytes);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static IEnumerable<object[]> PublicCases()
    {
        foreach (var row in ProjectionCases())
        foreach (var capture in new[] { false, true })
        foreach (var rtl in new[] { false, true }) yield return [.. row, capture, rtl];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicLoopMinimumCountsCompleteScalarsAcrossOperations(string category, int shape, bool compiled, bool capture, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex(Pattern(category, shape, capture), options);
        foreach (var body in new[] { "", "?", "??", "😀", "😀😀", "?😀", "😀?" })
        {
            var text = shape == 2 ? "a" + body + "7" : body;
            var input = Encoding.UTF8.GetBytes(text);
            var expected = shape == 0 || body.EnumerateRunes().Count() >= 2;
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.Equal(expected ? 1 : 0, regex.Count(input));
            var match = regex.Match(input);
            Assert.Equal(expected, match.Success);
            var detailed = regex.MatchDetailed(input);
            Assert.Equal(expected, detailed.Success);
            if (expected)
            {
                Assert.Equal((0, input.Length), (match.IndexInBytes, match.LengthInBytes));
                Assert.Equal((0, input.Length), (detailed.IndexInBytes, detailed.LengthInBytes));
                if (capture)
                {
                    var group = detailed.GetGroup("x");
                    Assert.True(group.Success);
                    Assert.Equal((shape == 2 ? 1 : 0, Encoding.UTF8.GetByteCount(body)), (group.IndexInBytes, group.LengthInBytes));
                    Assert.Equal(body, group.GetValueString());
                }
            }
            var cursor = regex.EnumerateMatches(input);
            Assert.Equal(expected, cursor.MoveNext());
            if (expected)
            {
                Assert.Equal((0, input.Length), (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
                Assert.False(cursor.MoveNext());
            }
            Assert.Equal(expected ? "<" + (capture ? body : text) + ">" : text,
                regex.ReplaceToString(input, capture ? "<${x}>" : "<$&>"));
            var splits = new List<string>();
            foreach (var split in regex.EnumerateSplits(input)) splits.Add(split.GetValueString());
            Assert.Equal(expected ? new[] { "", "" } : new[] { text }, splits);
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
