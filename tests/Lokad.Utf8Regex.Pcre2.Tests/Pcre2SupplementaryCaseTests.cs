using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SupplementaryCaseTests
{
    // Unicode 16.0 simple pairs, also checked against native PCRE2 10.47.
    [Theory]
    [InlineData(0x10D50, 0x10D70)]
    [InlineData(0x10D65, 0x10D85)]
    [InlineData(0x10400, 0x10428)]
    [InlineData(0x1E900, 0x1E922)]
    public void MatchingUsesPinnedPairsAcrossCharacterAndVmRoutes(int upper, int lower)
    {
        var capital = char.ConvertFromUtf32(upper);
        var small = char.ConvertFromUtf32(lower);
        using var cancellation = new CancellationTokenSource();
        foreach (var ucp in new[] { false, true })
        foreach (var finite in new[] { false, true })
        {
            var options = Pcre2CompileOptions.Caseless | (ucp ? Pcre2CompileOptions.Ucp : 0);
            foreach (var (pattern, text, success) in new[]
            {
                ("^" + capital + "$", small, true),
                ("^" + small + "$", capital, true),
                ("^[" + capital + "]$", small, true),
                ("^[" + capital + "-" + capital + "]$", small, true),
                ("^[^" + capital + "]$", small, false),
                ("^[^" + capital + "]$", "😀", true),
                (@"^(.)\1$", capital + small, true),
                ("(*MARK:x)^" + capital + "$", small, true),
                ("(*MARK:x)^[" + capital + "]$", small, true),
                (@"(*MARK:x)^(.)\1$", capital + small, true),
                ("(?ir)^" + capital + "$", small, true),
                ("(?-i)^" + capital + "$", small, false),
            })
            {
                var regex = new Utf8Pcre2Regex(pattern, options, default, Limits(finite), Timeout.InfiniteTimeSpan);
                var storage = Encoding.UTF8.GetBytes("padding!" + text + "!padding");
                var input = storage.AsSpan(8, Encoding.UTF8.GetByteCount(text));
                Assert.Equal(success, regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
                Assert.Equal(success ? 1 : 0, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
                var match = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token);
                Assert.Equal(success, match.Success);
                if (success)
                {
                    Assert.Equal((0, input.Length), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
                    Assert.Equal(text, match.GetValueString());
                    if (pattern.Contains(@"\1"))
                    {
                        Assert.Equal(capital, match.GetGroup(1).GetValueString());
                        Assert.Equal((0, 4), (match.GetGroup(1).StartOffsetInBytes, match.GetGroup(1).EndOffsetInBytes));
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(0x10D6F, false)]
    [InlineData(0x10D70, true)]
    [InlineData(0x10D85, true)]
    [InlineData(0x10D86, false)]
    [InlineData(0x1F600, false)]
    [InlineData(0x10FFFF, false)]
    public void FoldedRangePreservesEndpointsAndUnmappedScalars(int scalar, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(char.ConvertFromUtf32(scalar));
        foreach (var prefix in new[] { "", "(*MARK:x)" })
        {
            var regex = new Utf8Pcre2Regex(prefix + @"(?i)^[\x{10D50}-\x{10D65}]$");
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.Equal(expected ? 1 : 0, regex.Count(input));
        }
    }

    [Theory]
    [InlineData(0x10D50, 0x10D70)]
    [InlineData(0x10D65, 0x10D85)]
    [InlineData(0x10400, 0x10428)]
    [InlineData(0x1E900, 0x1E922)]
    public void ExtendedReplacementTransformsCompleteScalars(int upper, int lower)
    {
        var capital = char.ConvertFromUtf32(upper);
        var small = char.ConvertFromUtf32(lower);
        using var cancellation = new CancellationTokenSource();
        foreach (var finite in new[] { false, true })
        foreach (var (template, text, pattern, expected) in new[]
        {
            (@"\U$1", small + capital, "^(.+)$", capital + capital),
            (@"\L$1", capital + small, "^(.+)$", small + small),
            (@"\u$1", small + small, "^(.+)$", capital + small),
            (@"\l$1", capital + capital, "^(.+)$", small + capital),
            (@"\U" + small, "x", "x", capital),
            (@"\L" + capital, "x", "x", small),
            (@"\u" + small + small, "x", "x", capital + small),
            (@"\l" + capital + capital, "x", "x", small + capital),
            (@"\u😀" + small, "x", "x", "😀" + small),
        })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Ucp, default, Limits(finite), Timeout.InfiniteTimeSpan);
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(expected, regex.ReplaceToString(input, template, 0,
                Pcre2SubstitutionOptions.Extended, Pcre2MatchOptions.None, cancellation.Token));
            Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(input, template, 0,
                Pcre2SubstitutionOptions.Extended, Pcre2MatchOptions.None, cancellation.Token));
        }
    }

    private static Utf8Pcre2ExecutionLimits Limits(bool finite) => finite ? new()
    {
        MatchLimit = 1_000_000, DepthLimit = 256,
        HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20,
    } : default;
}
