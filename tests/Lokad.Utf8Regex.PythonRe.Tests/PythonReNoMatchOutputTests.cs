using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNoMatchOutputTests
{
    private static Utf8PythonRegex Create(ulong resultBytes = 128) =>
        new("(cat)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000, MaxDepth = 128,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = resultBytes,
            });

    [Theory]
    [InlineData("")]
    [InlineData("dog fox")]
    [InlineData("😀é dog")]
    public void NoMatchesNeedOnlyTheReturnedPayloadAndKeepOwnedByteResults(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = Create();
        var output = regex.SubnToString(input, @"[\1]", 0, 0);
        Assert.Equal(0, output.ReplacementCount);
        Assert.Equal(text, output.ResultText);
        var first = regex.Subn(input, @"[\1]", 0, 0);
        var retained = regex.Subn(input, @"[\1]", 0, 0);
        Assert.Equal(0, first.ReplacementCount);
        Assert.Equal(input, first.ResultBytes);
        Assert.Equal(input, retained.ResultBytes);
        if (input.Length != 0)
        {
            Assert.NotSame(input, first.ResultBytes);
            Assert.NotSame(first.ResultBytes, retained.ResultBytes);
            first.ResultBytes[0] ^= 1;
            Assert.Equal(Encoding.UTF8.GetBytes(text), input);
            Assert.Equal(input, retained.ResultBytes);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-7)]
    public void NegativeCountReturnsTheWholeInputWithoutCallingTheEvaluator(int count)
    {
        var input = Encoding.UTF8.GetBytes("cat😀cat");
        var regex = Create();
        Assert.Equal("cat😀cat", regex.SubnToString(input, @"[\1]", count, 3).ResultText);
        var output = regex.SubnToString(input, 0,
            static (int _, Utf8PythonDetailedMatchData _) => throw new InvalidOperationException("Evaluator must not run."),
            count, 3);
        Assert.Equal(0, output.ReplacementCount);
        Assert.Equal("cat😀cat", output.ResultText);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NoMatchOutputStillAdmitsTheExactFinalPayload(bool bytes, bool allow)
    {
        var input = "😀é"u8.ToArray();
        var extent = bytes ? Utf8ConstructionBudget.ArrayBytes<byte>(input.Length)
            : Utf8ConstructionBudget.StringBytes("😀é".Length);
        var regex = Create(extent - (allow ? 0UL : 1UL));
        if (allow)
        {
            if (bytes) Assert.Equal(input, regex.Subn(input, "x", 0, 0).ResultBytes);
            else Assert.Equal("😀é", regex.SubnToString(input, "x", 0, 0).ResultText);
        }
        else
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                if (bytes) _ = regex.Subn(input, "x", 0, 0);
                else _ = regex.SubnToString(input, "x", 0, 0);
            });
            Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, failure.ResourceKind);
        }
    }

    [Fact]
    public void NoMatchReturnStillValidatesTheWholeSubjectAndObservesTheOriginalToken()
    {
        var regex = Create();
        Assert.Throws<ArgumentException>(() => regex.SubnToString([0xFF], "x", 0, 0));
        Assert.Throws<ArgumentException>(() => regex.Subn([0xFF], "x", -1, 0));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() =>
            regex.SubnToString("dog"u8, "x", 0, 0, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }
}
