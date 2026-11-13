using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAsciiCaptureStringTests
{
    private static Utf8ResourcePolicy Funded => new()
    {
        MaxWorkSteps = 10_000_000, MaxDepth = 100_000,
        MaxWorkingBytes = 16 * 1024 * 1024, MaxResultBytes = 16 * 1024 * 1024,
    };

    private static Utf8PythonRegex Create(string pattern, Utf8ResourcePolicy policy)
        => new(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void AsciiCaptureAcrossDecodeBlocksRetainsExactOwnedText(int length)
    {
        var first = new string('a', length);
        {
            using var regex = Create("(a+)(b)", Funded);
            var result = regex.FindAllToStrings(Encoding.UTF8.GetBytes(first + "b"));
            var tuple = Assert.Single(result.TupleValues);
            Assert.Equal(new[] { first, "b" }, tuple);
            Assert.Equal(new[] { "aa", "b" }, Assert.Single(regex.FindAllToStrings("aab"u8).TupleValues));
            Assert.Equal(first, tuple[0]);
        }
    }
    [Theory]
    [InlineData("(a+)(b)", "😀aaabé", "aaa", "b")]
    [InlineData("(😀+)(é+)", "x😀😀ééx", "😀😀", "éé")]
    [InlineData("(a+)(b)", "aaab\0", "aaa", "b")]
    public void WholeSubjectValidationKeepsAsciiAndUnicodeCaptureLengths(
        string pattern, string input, string first, string second)
    {
        {
            using var regex = Create(pattern, Funded);
            var result = regex.FindAllToStrings(Encoding.UTF8.GetBytes(input));
            Assert.Equal(new[] { first, second }, Assert.Single(result.TupleValues));
        }
    }
    [Fact]
    public void EmptyCapturesStillProduceOneOwnedTuple()
    {
        var result = Create("(a*)(b?)", Funded).FindAllToStrings([]);
        Assert.Equal(new[] { string.Empty, string.Empty }, Assert.Single(result.TupleValues));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResultAdmissionUsesCharacterLengthBeforeTheFirstCaptureString(bool unicode)
    {
        var first = unicode ? string.Concat(Enumerable.Repeat("😀", 3000)) : new string('a', 4097);
        var second = unicode ? "é" : "b";
        var pattern = unicode ? "(😀+)(é)" : "(a+)(b)";
        var input = Encoding.UTF8.GetBytes(first + second);
        var required = Utf8ConstructionBudget.ArrayBytes<string>(2) + Utf8ConstructionBudget.StringBytes(first.Length);
        {
            using var limited = Create(pattern, Funded with { MaxResultBytes = required - 1 });
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.FindAllToStrings(input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, failure.ResourceKind);
            Assert.Equal(required - 1, failure.Limit);
            Assert.Equal(required, failure.Requested);
            using var sufficient = Create(pattern, Funded);
            Assert.Equal(new[] { first, second }, Assert.Single(sufficient.FindAllToStrings(input).TupleValues));
        }
    }
    [Fact]
    public void PreCancellationRetainsItsOriginalToken()
    {
        var regex = Create("(a)(b)", Funded);

        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.FindAllToStrings("ab"u8, 0, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }
}
