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
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 512, MaxWorkingBytes = 2 << 20, MaxResultBytes = 1 << 20 });
        AssertScalarPreparation(regex);
        const string text = "😀abababab中abababab";
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
        var match = regex.MatchDetailed(input);
        Assert.Equal((4, 8), (match.IndexInBytes, match.LengthInBytes));
        Assert.Equal("abab", match.GetGroup("x").GetValueString());
        var count = 0;
        foreach (var value in regex.EnumerateMatches(input))
        { Assert.Equal(8, value.LengthInBytes); count++; }
        Assert.Equal(2, count);
        Assert.Equal("😀<abab>中<abab>", regex.ReplaceToString(input, "<${x}>"));
        var parts = new List<string>();
        foreach (var part in regex.EnumerateSplits(input)) parts.Add(part.GetValueString());
        Assert.Equal(new[] { "😀", "中", "" }, parts);
        Assert.Throws<ArgumentException>(() => regex.IsMatch([.. input, 0xff]));
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("abab", match.GetGroup("x").GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RightToLeftFallbackKeepsCaptureAndReferenceOrder(bool compiled)
    {
        var regex = new Utf8Regex(@"\k<x>(?<x>(?:ab|ba){2})",
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        AssertScalarPreparation(regex);
        var input = "😀abababab中"u8;
        Assert.Equal(1, regex.Count(input));
        var match = regex.MatchDetailed(input);
        Assert.Equal((4, 8), (match.IndexInBytes, match.LengthInBytes));
        Assert.Equal("abab", match.GetGroup("x").GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NonBacktrackingFallbackKeepsWholeScalarsAndLastCapture(bool compiled)
    {
        var regex = new Utf8Regex(@"(?<x>\p{Lu}){2}",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        AssertScalarPreparation(regex);
        var input = "😀A𐐀中"u8;
        Assert.Equal(1, regex.Count(input));
        var match = regex.MatchDetailed(input);
        Assert.Equal((4, 5), (match.IndexInBytes, match.LengthInBytes));
        Assert.Equal("𐐀", match.GetGroup("x").GetValueString());
        Assert.Equal(1, match.GetGroup("x").CaptureCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeFamilyRetainsItsRealPreparation(bool compiled)
    {
        var regex = new Utf8Regex("^cat", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var prepared = regex.Inspection.PreparedRegex;
        Assert.True(prepared.FallbackDirectFamily.HasValue);
        Assert.NotNull(prepared.ExecutionTree);
        Assert.Equal(1, regex.Count("cat cat"u8));
        Assert.Equal(3, regex.Match("cat cat"u8).LengthInBytes);
    }

    private static void AssertScalarPreparation(Utf8Regex regex)
    {
        var prepared = regex.Inspection.PreparedRegex;
        Assert.Equal(NativeExecutionKind.FallbackRegex, prepared.ExecutionKind);
        Assert.False(prepared.FallbackDirectFamily.HasValue);
        Assert.NotNull(prepared.ScalarProgram);
        Assert.Null(prepared.ExecutionTree);
        Assert.Same(prepared.SemanticRegex.CaptureCatalog, prepared.ScalarProgram.CaptureCatalog);
    }
}
