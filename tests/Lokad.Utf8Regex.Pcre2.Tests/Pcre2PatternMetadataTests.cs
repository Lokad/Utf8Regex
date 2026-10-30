using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PatternMetadataTests
{
    [Theory]
    [InlineData("literal", 0)]
    [InlineData("[abc]+", 0)]
    [InlineData("(a)(b)", 2)]
    [InlineData("(?J)(?<same>a)(?<same>b)", 2)]
    [InlineData("(?|(a)|(b))(c)", 2)]
    [InlineData("(?=(?<x>a))a", 1)]
    [InlineData("(?<x>a)(?&x)", 1)]
    [InlineData("(?n)(a)(?<x>b)", 1)]
    [InlineData("(?(DEFINE)(?<d>a))(?&d)", 1)]
    public void CaptureCountSurvivesExecutionSelectionAndBranchReset(string source, int count)
    {
        Assert.Equal(count, new Utf8Pcre2Regex(source).CaptureGroupCount);
        Assert.Equal(count, new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(source)).CaptureGroupCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CopiesOriginalSourceIntoCallerStorageAndPreservesCancellation(bool native)
    {
        var source = "(?#" + new string('x', 2043) + ")é😀\0";
        var encoded = Encoding.UTF8.GetBytes(source);
        var regex = native ? new Utf8Pcre2Regex(encoded) : new Utf8Pcre2Regex(source);
        var destination = Enumerable.Repeat((byte)0xa5, encoded.Length + 2).ToArray();
        Assert.Equal(OperationStatus.DestinationTooSmall,
            regex.TryCopyPatternUtf8(destination.AsSpan(1, encoded.Length - 1), out var required));
        Assert.Equal(encoded.Length, required); Assert.All(destination, value => Assert.Equal(0xa5, value));
        Assert.Equal(OperationStatus.Done, regex.TryCopyPatternUtf8(destination.AsSpan(1, encoded.Length), out required));
        Assert.Equal(encoded.Length, required); Assert.Equal(encoded, destination.AsSpan(1, required).ToArray());
        Assert.Equal(0xa5, destination[0]); Assert.Equal(0xa5, destination[^1]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.TryCopyPatternUtf8(destination, out _, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        destination.AsSpan().Clear(); Assert.Equal(source, regex.Pattern);
    }
}
