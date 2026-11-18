using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarOnlyPreparationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CapturedFallbackUsesScalarPreparationAcrossPublicOperations(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        const string pattern = @"(?<x>(?:a|ab){2,3})\k<x>";
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 512,
                MaxWorkingBytes = 2 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        AssertScalarPreparation(regex);
        const string text = "😀abababab中abababab";
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal((4, 8), (match.Value.StartOffsetInBytes, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes)));
        Assert.Equal("abab", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
        var count = 0;
        foreach (var value in regex.EnumerateMatches(input))
        { Assert.Equal(8, (value.EndOffsetInBytes - value.StartOffsetInBytes)); count++; }
        Assert.Equal(2, count);
        Assert.Equal("😀<abab>中<abab>", Encoding.UTF8.GetString(regex.Replace(input, "<${x}>"u8)));
        var parts = new List<string>();
        foreach (var part in regex.EnumerateSplits(input)) parts.Add(Encoding.UTF8.GetString(part.GetValueBytes()));
        Assert.Equal(new[] { "😀", "中", "" }, parts);
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("abab", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RightToLeftFallbackKeepsCaptureAndReferenceOrder(bool compiled)
    {
        var regex = new Utf8Regex(@"\k<x>(?<x>(?:ab|ba){2})", options:             RegexOptions.CultureInvariant | RegexOptions.RightToLeft | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        AssertScalarPreparation(regex);
        var input = "😀abababab中"u8;
        Assert.Equal(1, regex.Count(input));
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(((global::System.ReadOnlySpan<byte>)input).ToArray()));
        Assert.Equal((4, 8), (match.Value.StartOffsetInBytes, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes)));
        Assert.Equal("abab", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NonBacktrackingFallbackKeepsWholeScalarsAndLastCapture(bool compiled)
    {
        var regex = new Utf8Regex(@"(?<x>\p{Lu}){2}", options:             RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        AssertScalarPreparation(regex);
        var input = "😀A𐐀中"u8;
        Assert.Equal(1, regex.Count(input));
        var match = Utf8MatchSequence.First(regex.CreateMatchCursor(((global::System.ReadOnlySpan<byte>)input).ToArray()));
        Assert.Equal((4, 5), (match.Value.StartOffsetInBytes, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes)));
        Assert.Equal("𐐀", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(regex.GroupNumberFromUtf8Name("x"u8)).Span));
        Assert.Equal(1, match.GetCaptures(regex.GroupNumberFromUtf8Name("x"u8)).Length);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeFamilyRetainsItsRealPreparation(bool compiled)
    {
        var regex = new Utf8Regex("^cat", options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var prepared = regex.Inspection.PreparedRegex;
        Assert.True(prepared.AnchoredLiteral.HasValue);
        Assert.Equal(1, regex.Count("cat cat"u8));
        Assert.Equal(3, (regex.Match("cat cat"u8).EndOffsetInBytes - regex.Match("cat cat"u8).StartOffsetInBytes));
    }

    private static void AssertScalarPreparation(Utf8Regex regex)
    {
        var prepared = regex.Inspection.PreparedRegex;
        Assert.Equal(NativeExecutionKind.FallbackRegex, prepared.ExecutionKind);
        Assert.False(prepared.AnchoredLiteral.HasValue);
        Assert.NotNull(prepared.ScalarProgram);
        Assert.Same(prepared.SemanticRegex.CaptureCatalog, prepared.ScalarProgram.CaptureCatalog);
    }
}
