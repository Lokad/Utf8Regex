using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureByteOutputTests
{
    private static Utf8ResourcePolicy Funded => new()
    {
        MaxWorkSteps = 10_000_000,
        MaxDepth = 100_000,
        MaxWorkingBytes = 16 * 1024 * 1024,
        MaxResultBytes = 16 * 1024 * 1024
    };

    private static Utf8PythonRegex Create(string pattern, Utf8ResourcePolicy policy)
        => new(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void AsciiCapturesAcrossBlockEdgesRetainOwnedValues(int length)
    {
        var first = new string('a', length);
        {
            using var regex = Create("(a+)(b)", Funded);
            var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(Encoding.UTF8.GetBytes(first + "b")));
            var tuple = Assert.Single(result.TupleValues);
            Assert.Equal(new[] { first, "b" }, tuple);
            Assert.Equal(new[] { "aa", "b" }, Assert.Single(PythonReFindAllAssert.Decode(regex.FindAllToUtf8("aab"u8)).TupleValues));
            Assert.Equal(first, tuple[0]);
        }
    }
    [Theory]
    [InlineData("(a+)(b)", "😀aaabé", "aaa", "b")]
    [InlineData("(😀+)(é+)", "x😀😀ééx", "😀😀", "éé")]
    [InlineData("(a+)(b)", "aaab\0", "aaa", "b")]
    public void ValidTextPreservesAsciiAndUnicodeCaptureLengths(
        string pattern, string input, string first, string second)
    {
        {
            using var regex = Create(pattern, Funded);
            var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(Encoding.UTF8.GetBytes(input)));
            Assert.Equal(new[] { first, second }, Assert.Single(result.TupleValues));
        }
    }
    [Fact]
    public void EmptyCapturesStillProduceOneOwnedTuple()
    {
        var result = PythonReFindAllAssert.Decode(Create("(a*)(b?)", Funded).FindAllToUtf8([]));
        Assert.Equal(new[] { string.Empty, string.Empty }, Assert.Single(result.TupleValues));
    }
    [Fact]
    public void PreCancellationRetainsItsOriginalToken()
    {
        var regex = Create("(a)(b)", Funded);

        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => PythonReFindAllAssert.Decode(regex.FindAllToUtf8("ab"u8, 0, cancellation.Token)));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }
}
