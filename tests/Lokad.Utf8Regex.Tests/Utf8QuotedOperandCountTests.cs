using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8QuotedOperandCountTests
{
    private const string Template = "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
    private static readonly string Pattern = Template.Replace("ASIA|AKIA|AROA|AIDA", "AB|CD")
        .Replace("[A-Z0-7]{16}", "[0-9]{2}").Replace("[a-zA-Z0-9+/]{40}", "[a-z]{3}");

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void NecessaryOperandsRetainBothOrdersLazyRepeatsAndCaptures(RegexOptions extra)
    {
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Multiline | extra);
        var oracle = new Regex(Pattern, RegexOptions.Multiline | extra);
        foreach (var text in new[] { "", "EASTASIA AB12 CD34", "\"AB12\"", "\"ABxx\" \"abc\"",
            "\"AB12\"😀\"abc\"", "'abc' λ 'CD34'", "\"AB12\"\nctx\n\"abc\"",
            "\"AB12\"\"abc\"\"CD34\"\"abc\"", "é\"AB12\" \"abc\" 😀 \"abc\" \"CD34\"" })
        {
            var padded = Encoding.UTF8.GetBytes("pad" + text + "pad");
            var input = padded.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            var expected = oracle.Match(text);
            var whole = regex.Match(input);
            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(((global::System.ReadOnlySpan<byte>)input).ToArray()));
            Assert.Equal(expected.Success, whole.Success);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success)
            {
                Assert.Equal(expected.Value, Encoding.UTF8.GetString(input.Slice(whole.StartOffsetInBytes, (whole.EndOffsetInBytes - whole.StartOffsetInBytes))));
                for (var group = 0; group < expected.Groups.Count; group++)
                    Assert.Equal(expected.Groups[group].Value, System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(group).Span));
                Assert.Equal(oracle.Count(text), regex.Count(input));
                Assert.Equal(expected.Value, System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(0).Span));
            }
        }
    }

    [Theory]
    [InlineData("+?")]
    [InlineData("{2,}")]
    public void OuterRepeatsPreserveCounts(string quantifier)
    {
        var pattern = Pattern[..^1] + quantifier;
        var regex = new Utf8Regex(pattern, options: RegexOptions.Multiline);
        foreach (var text in new[] { "\"AB12\"\"abc\"", "\"AB12\"\"abc\"\"CD34\"\"abc\"" })
            Assert.Equal(new Regex(pattern, RegexOptions.Multiline).Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuotedRelationsRespectFiniteWorkingAdmissionOnHitsAndMisses(bool count)
    {
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Multiline, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 4096,
                MaxWorkingBytes = 1 << 20
            }
        });
        var denied = new Utf8Regex(Pattern, options: RegexOptions.Multiline, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        });
        var missingRequiredStart = "EASTASIA AB12 CD34 😀"u8;
        if (count) Assert.Equal(0, denied.Count(missingRequiredStart));
        else Assert.False(denied.IsMatch(missingRequiredStart));

        if (!count) Assert.False(denied.IsMatch("\"ABxx\" \"abc\""u8));
        // Give the miss enough remaining bytes to reach the verifier.
        foreach (var text in new[] { "\"ABxx\" \"abc\" .............", "\"AB12\" \"abc\"" })
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var expected = new Regex(Pattern, RegexOptions.Multiline).Count(text);
            if (count) Assert.Equal(expected, regex.Count(bytes));
            else Assert.Equal(expected != 0, regex.IsMatch(bytes));
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                if (count) denied.Count(bytes);
                else denied.IsMatch(bytes);
            });
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
        }
    }

    [Fact]
    public void UnicodeTailPredicatesRetainScalarSemantics()
    {
        var regex = new Utf8Regex(Pattern.Replace("[0-9]", "[^q]"), options: RegexOptions.Multiline);
        Assert.Equal(1, regex.Count("\"AB😀é\" \"abc\""u8));
        Assert.True(regex.IsMatch("\"abc\" \"CD😀é\""u8));
    }

    [Fact]
    public void ByteOffsetsKeepTheirExistingMeaning()
    {
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Multiline);
        const string text = "é\"AB12\" \"abc\" 😀 'abc' 'CD34'";
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(new Regex(Pattern, RegexOptions.Multiline).Matches(text, 14).Count,
            Utf8MatchSequence.Count(regex.EnumerateMatches(bytes, Encoding.UTF8.GetByteCount(text.AsSpan(0, 14)))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuotedRelationSearchPollsOriginalCancellationAndDeadline(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 8192));
        long ticks = 0; var reads = 0; var trigger = int.MaxValue;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == trigger) { if (expire) ticks = 100; else source.Cancel(); }
            return ticks;
        });
        var regex = new Utf8Regex(Pattern, options: RegexOptions.Multiline, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50),
            ResourcePolicy = new()
            {
                MaxDepth = 4096,
                MaxWorkingBytes = 4 << 20
            }
        });
        Assert.Equal(0, regex.Count(input, source.Token));
        Assert.True(reads > 1); trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout,
            Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, source.Token)).MatchTimeout);
        else Assert.Equal(source.Token,
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
    }
}
