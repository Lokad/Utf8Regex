using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralCountTests
{
    [Theory]
    [InlineData("夏洛克·福尔摩斯")]
    [InlineData("夏")]
    [InlineData("夏·é")]
    [InlineData("夏洛😀")]
    [InlineData("夏😀")]
    [InlineData("夏A")]
    public void PublicCountPreservesDensePrefixTailsNonoverlapAndBorrowedBounds(string literal)
    {
        var scalars = literal.EnumerateRunes().ToArray();
        var prefix = string.Concat(scalars.Take(Math.Min(2, scalars.Length - 1)).Select(scalar => scalar.ToString()));
        var text = string.Concat(Enumerable.Repeat(prefix + "?", 256)) + literal + literal + prefix;
        var bytes = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        foreach (var controlled in new[] { false, true })
        {
            using var token = new CancellationTokenSource();
            var regex = new Utf8Regex(literal, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = controlled ? new()
                {
                    MaxWorkSteps = 1_000_000,
                    MaxDepth = 1024,
                    MaxWorkingBytes = 1 << 20,
                    MaxResultBytes = 1 << 20
                }

                : default
            });
                foreach (var offset in new[] { 0, 1, 31, 32 })
            {
                byte[] storage = [.. Enumerable.Repeat((byte)0xff, offset), .. bytes, 0xff];
                Assert.Equal(2, regex.Count(storage.AsSpan(offset, bytes.Length), controlled ? token.Token : default));
            }
        }
        var repeated = new Utf8Regex("夏夏A");
        Assert.Equal(1, repeated.Count("夏夏夏A"u8));
        Assert.Equal(2, repeated.Count("夏夏A夏夏A夏夏"u8));
    }

    [Theory]
    [InlineData("漢|漢字😀|字", 4)]
    [InlineData("漢字😀|漢|字", 3)]
    [InlineData("漢|漢字|字", 4)]
    [InlineData("漢字|漢|字", 3)]
    public void PublicCountKeepsThreeByteLeadingAlternativesInOrder(string pattern, int expected)
    {
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
        var storage = "🙂é漢字😀漢😀字!"u8;
        Assert.Equal(expected, regex.Count(storage[4..]));
        using var source = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(storage[4..], source.Token));
    }

    [Theory]
    [InlineData("夏A|夏A字F", 2046)]
    [InlineData("夏A字F|夏A", 2046)]
    [InlineData("夏A字F|夏A", 2048)]
    public void WideThreeByteLeadingFamilyKeepsOrderAcrossCountWindows(string prefixAlternatives, int firstMatchByte)
    {
        var pattern = prefixAlternatives + "|约A|阿B|雷C|莫D|文E|字F|语G";
        var spacing = new string('x', firstMatchByte - Encoding.UTF8.GetByteCount("夏Z"));
        var text = spacing + "夏Z夏A字F约A" + new string('x', 2052) + "字F莫D夏A夏";
        var bytes = Encoding.UTF8.GetBytes("🙂" + text);
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
        using var source = new CancellationTokenSource();
        var expected = Regex.Count(text, pattern);

        Assert.Equal(PreparedMultiLiteralKind.ExactAutomaton, regex.Inspection.SearchPlan.MultiLiteralSearch.Kind);
        Assert.Equal(expected, regex.Count(bytes.AsSpan(4)));
        Assert.Equal(expected, regex.Count(bytes.AsSpan(4), source.Token));
    }

    [Theory]
    [InlineData("夏洛克·福尔摩斯")]
    [InlineData("夏·é")]
    [InlineData("夏洛😀")]
    [InlineData("夏😀")]
    public void PublicCountIgnoresPartialLeadingScalarAnchorsAtTheSubjectTail(string literal)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        foreach (var controlled in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
                var policy = controlled ? new Utf8ResourcePolicy { MaxWorkSteps = 100_000, MaxDepth = 256, MaxWorkingBytes = 1UL << 20, MaxResultBytes = 1UL << 20 } : default;
                var regex = new Utf8Regex(literal, options: options, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = policy
                });
            var prefix = "";
            foreach (var scalar in literal.EnumerateRunes().SkipLast(1))
            {
                prefix += scalar.ToString();
                foreach (var offset in new[] { 0, 1, 31, 32 })
                {
                    var bytes = Encoding.UTF8.GetBytes(prefix + new string('x', 32) + literal + new string('x', 32) + prefix);
                    var storage = new byte[offset + bytes.Length + 16];
                    Array.Fill(storage, (byte)0xff);
                    bytes.CopyTo(storage.AsSpan(offset));
                    var subject = storage.AsSpan(offset, bytes.Length);
                    Assert.Equal(1, regex.Count(subject, controlled ? cancellation.Token : default));
                    Assert.Equal(0, regex.Count(Encoding.UTF8.GetBytes(new string('x', 32) + prefix),
                        controlled ? cancellation.Token : default));
                }
            }
        }
    }

    [Theory]
    [InlineData("汉字")]
    [InlineData("夏洛克·福尔摩斯")]
    [InlineData("é")]
    [InlineData("éa")]
    [InlineData("😀")]
    [InlineData("ab")]
    [InlineData("abé")]
    public void LiteralKernelsCountWithinBorrowedViews(string literal)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft })
        {
            using var regex = new Utf8Regex(Regex.Escape(literal), options: options);
            var search = Assert.IsType<PreparedSubstringSearch>(regex.Inspection.SearchPlan.LiteralSearch);
            foreach (var tail in new[] { "", literal[..^1] })
            {
                // Keep every input a complete scalar, including a supplementary tail.
                var suffix = tail.Length > 0 && char.IsHighSurrogate(tail[^1]) ? "" : tail;
                var bytes = Encoding.UTF8.GetBytes("x" + literal + "!" + literal + "!" + suffix);
                foreach (var offset in new[] { 0, 1, 31 })
                {
                    var storage = new byte[offset + bytes.Length + 32];
                    bytes.CopyTo(storage.AsSpan(offset));
                    var input = storage.AsSpan(offset, bytes.Length);
                    Assert.Equal(2, regex.Count(input));
                    Assert.Equal(2, search.Count(input));
                }
            }
        }
    }

    [Fact]
    public void MixedWidthCountResumesPastDenseFalseAnchorsWithinBorrowedView()
    {
        const string pattern = "夏洛克·福尔摩斯";
        using var regex = new Utf8Regex(pattern);
        using var source = new CancellationTokenSource();
        var text = pattern + new string('¡', 256) + pattern + pattern[..^1];
        var storage = Encoding.UTF8.GetBytes(pattern + text + "斯" + pattern);
        var prefix = Encoding.UTF8.GetByteCount(pattern);
        var input = storage.AsSpan(prefix, Encoding.UTF8.GetByteCount(text));

        Assert.Equal(2, regex.Count(input));
        Assert.Equal(2, regex.Count(input, source.Token));
        Assert.Equal(1, regex.Count(input[..prefix]));
    }
}
