using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNoMatchOutputTests
{
    private static Utf8PythonRegex Create(ulong resultBytes = 128) =>
        new("(cat)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxDepth = 128,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = resultBytes
            }
        });

    [Theory]
    [InlineData("")]
    [InlineData("dog fox")]
    [InlineData("😀é dog")]
    public void NoMatchesNeedOnlyTheReturnedPayloadAndKeepOwnedByteResults(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = Create();
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
    [InlineData(false)]
    [InlineData(true)]
    public void NoMatchOutputStillAdmitsTheExactFinalPayload(bool allow)
    {
        var input = "😀é"u8.ToArray();
        var extent = Utf8ConstructionBudget.ArrayBytes<byte>(input.Length);
        var regex = Create(extent - (allow ? 0UL : 1UL));
        if (allow)
        {
            Assert.Equal(input, regex.Subn(input, "x", 0, 0).ResultBytes);
        }
        else
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                _ = regex.Subn(input, "x", 0, 0);
            });
            Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, failure.ResourceKind);
        }
    }

    [Fact]
    public void NoMatchReturnObservesTheOriginalToken()
    {
        var regex = Create();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() =>
            PythonReSubstitutionAssert.Decode(regex.Subn("dog"u8, "x", 0, 0, cancellation.Token)));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }
}
