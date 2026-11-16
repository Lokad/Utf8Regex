using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexInvalidInputApiTests
{
    private static readonly byte[] s_invalidUtf8 = new byte[] { 0x61, 0xE2, 0x28, 0xA1 };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstanceMatchApisRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);

        Assert.Throws<ArgumentException>(() => regex.IsMatch(s_invalidUtf8));
        Assert.Throws<ArgumentException>(() => regex.Match(s_invalidUtf8));
        Assert.Throws<ArgumentException>(() => regex.MatchDetailed(s_invalidUtf8));
        Assert.Throws<ArgumentException>(() => regex.Count(s_invalidUtf8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstanceEnumerationApisRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);

        Assert.Throws<ArgumentException>(() =>
        {
            var enumerator = regex.EnumerateMatches(s_invalidUtf8);
            _ = enumerator.MoveNext();
        });

        Assert.Throws<ArgumentException>(() =>
        {
            var enumerator = regex.EnumerateSplits(s_invalidUtf8);
            _ = enumerator.MoveNext();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstanceReplaceApisRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);

        Assert.Throws<ArgumentException>(() => regex.Replace(s_invalidUtf8, "z"));
        Assert.Throws<ArgumentException>(() => regex.ReplaceToString(s_invalidUtf8, "z"));
        Assert.Throws<ArgumentException>(() => regex.Replace(s_invalidUtf8, "z"u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AsciiIntersectionSimplePatternRejectsInvalidUtf8(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(@"^\d{1,3}$", options);

        Assert.Throws<ArgumentException>(() => regex.IsMatch(s_invalidUtf8));
        Assert.Throws<ArgumentException>(() => regex.Count(s_invalidUtf8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartAtApisRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);

        Assert.Throws<ArgumentException>(() => regex.Match(s_invalidUtf8, 0, []).Success);
        Assert.Throws<ArgumentException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(s_invalidUtf8, 0)));
        Assert.Throws<ArgumentException>(() =>
        {
            var enumerator = regex.EnumerateMatches(s_invalidUtf8, 0);
            _ = enumerator.MoveNext();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountedSplitsRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);
        Assert.Throws<ArgumentException>(() =>
        {
            var enumerator = regex.EnumerateSplits(s_invalidUtf8, 10);
            _ = enumerator.MoveNext();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryReplaceApisRejectInvalidUtf8(bool compiled)
    {
        var regex = CreateRegex(compiled);

        Assert.Throws<ArgumentException>(() =>
        {
            Span<byte> destination = new byte[64];
            _ = regex.TryReplace(s_invalidUtf8, "z", destination, out _);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            Span<byte> destination = new byte[64];
            _ = regex.TryReplace(s_invalidUtf8, "z"u8.ToArray(), destination, out _);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StartAtApisRejectInvalidUtf8BeforeTheStart(bool compiled)
    {
        // The malformed byte precedes the requested start: the strict input
        // contract still rejects the whole input.
        var regex = CreateRegex(compiled);
        byte[] input = [0xFF, .. "abc"u8];

        Assert.Throws<ArgumentException>(() => regex.Match(input, 1, []).Success);
        Assert.Throws<ArgumentException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(input, 1)));
        Assert.Throws<ArgumentException>(() =>
        {
            var enumerator = regex.EnumerateMatches(input, 1);
            _ = enumerator.MoveNext();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstanceApisRejectInvalidUtf8AfterAHit(bool compiled)
    {
        // The malformed byte trails a valid hit: routes must validate the whole
        // input rather than stopping at the first hit.
        var regex = CreateRegex(compiled);
        byte[] input = [.. "abc"u8, 0xFF];

        Assert.Throws<ArgumentException>(() => regex.IsMatch(input));
        Assert.Throws<ArgumentException>(() => regex.Count(input));
        Assert.Throws<ArgumentException>(() => regex.Match(input));
        Assert.Throws<ArgumentException>(() => regex.MatchDetailed(input));
        Assert.Throws<ArgumentException>(() =>
        {
            var enumerator = regex.EnumerateMatches(input);
            _ = enumerator.MoveNext();
        });
        Assert.Throws<ArgumentException>(() => regex.Replace(input, "z"));
        Assert.Throws<ArgumentException>(() =>
        {
            Span<byte> destination = new byte[64];
            _ = regex.TryReplace(input, "z", destination, out _);
        });
    }

    private static Utf8Regex CreateRegex(bool compiled)
        => new("abc", compiled ? RegexOptions.Compiled | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant);
}
