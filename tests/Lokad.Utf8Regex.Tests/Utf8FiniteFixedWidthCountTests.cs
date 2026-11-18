using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8FiniteFixedWidthCountTests
{
    private static readonly byte[] Dense = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(" xx ab12 ab34 ", 1000)));
    private static Utf8Regex Create(string pattern, ulong work = 1000000, ulong working = 1 << 20,
        RegexOptions options = RegexOptions.None) => new(pattern, options: options | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = work,
                MaxDepth = 1024,
                MaxWorkingBytes = working,
                MaxResultBytes = 1
            }
        });

    [Theory]
    [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void DenseOverlapPreservesBorrowedOffsetsAndEveryVectorTail(int width)
    {
        var pattern = "aa[A-Za-z]{" + (width - 2) + "}";
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(pattern, options: options);
            foreach (var length in Enumerable.Range(0, 100).Concat(new[] { 127, 128, 129, 255, 256, 257, 511 }))
            {
                var storage = Encoding.UTF8.GetBytes("pad" + new string('a', length) + "pad");
                for (var offset = 0; offset < 4; offset++)
                {
                    var bytes = new byte[length + offset];
                    storage.AsSpan(3, length).CopyTo(bytes.AsSpan(offset));
                    Assert.Equal(length / width, regex.Count(bytes.AsSpan(offset)));
                }
            }
        }
    }

    [Fact]
    public void EligibleCountNeedsNoVmDepthOrWorkingStorage()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 };
        foreach (var option in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("ab[0-9]{2}", options: option | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = policy
            });
            // The vector kernel stays preferred; its checked branch fallback
            // also needs no VM storage on hardware without AVX2.
            Assert.Equal(2000, regex.Count(Dense));
        }
    }

    [Theory]
    [InlineData("ab[0-9]{2}$", "ab12 xx ab34")]
    [InlineData("^ab[0-9]{2}", "xx ab12 ab34")]
    [InlineData(@"ab[0-9]{2}\b", "ab12ab34")]
    [InlineData("(?i)ab[0-9]{2}", "AB12 ab34")]
    [InlineData("(?<=x)ab[0-9]{2}", "xab12 ab34")]
    [InlineData("ab[0-9]{2}", "😀ab12éab34")]
    public void IneligibleSemanticsKeepTheirExistingResults(string pattern, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(new Regex(pattern, RegexOptions.CultureInvariant).Count(text), Create(pattern).Count(bytes));
    }
}
