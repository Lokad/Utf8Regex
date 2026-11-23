using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarAnchoredRepeatRegressionTests
{
    public static IEnumerable<object[]> AnchoredCases()
    {
        foreach (var pattern in new[] { @"^abx?xy$", @"\Aabx{0,2}xy\z", @"^(?<head>ab)x?xy$", @"\A(?<head>ab)x{0,2}xy\z" })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [pattern, compiled, rtl];
    }

    [Theory]
    [MemberData(nameof(AnchoredCases))]
    public void AnchoredLoopOverlapRetainsOwnedCompletePublicOperations(string pattern, bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "abxy", "abxxy", "abxxxy", "abxy\n", "abxxy\n", "abxy\r\n", "abxy\nabxxy", "\U0001f600abxy", "abxy\U0001f600"], options);
    }

    [Theory]
    [InlineData("$", "100", true)]
    [InlineData("$", "100-😀\n", true)]
    [InlineData("$", "100-\n\n", false)]
    [InlineData("$", "100:\n", false)]
    [InlineData(@"\z", "100-😀", true)]
    [InlineData(@"\z", "100-😀\n", false)]
    public void ShortAnchoredRepeatProvesRangeWithUnobservedCaptures(string end, string text, bool expected)
    {
        var pattern = $@"^([0-9]+)(\-| |{end})(.*){end}";
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = 3, MaxWorkingBytes = 1, MaxDepth = 1 } });
        using var token = new CancellationTokenSource();
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(bytes, token.Token));

        using var ranges = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = 4, MaxWorkingBytes = 1, MaxDepth = 1 } });
        var actual = ranges.Match(bytes, cancellationToken: token.Token);
        Assert.Equal(expected, actual.Success);
        if (expected)
        {
            var oracle = Regex.Match(text, pattern);
            Utf8MatchSequence.AssertRange(actual, text, oracle.Index, oracle.Length);
        }
    }

    [Theory]
    [InlineData(" [|  /// title", 4, true)]
    [InlineData(" [|  ///\n😀", 4, true)]
    [InlineData(" [|  ///\n\n😀", 4, false)]
    [InlineData(" [|\u2003///\u2003doc😀", 128, true)]
    public void AnchoredWhitespaceProofPreservesGreedyScalarRanges(string text, int workLimit, bool expected)
    {
        const string pattern = @"^\s*(\[\|\s*)?///\s?(?<doc>.*)$";
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = (ulong)workLimit, MaxWorkingBytes = 4096 } });
        using var source = new CancellationTokenSource();

        var actual = regex.Match(Encoding.UTF8.GetBytes(text), cancellationToken: source.Token);

        Assert.Equal(expected, actual.Success);
        if (expected)
        {
            var oracle = Regex.Match(text, pattern);
            Utf8MatchSequence.AssertRange(actual, text, oracle.Index, oracle.Length);
        }
    }

    [Fact]
    public void AnchoredFoldedSetProofChecksAsciiAndUnicodeUnderTheSameLimit()
    {
        const string pattern = "^[a-z][a-z0-9_]*$";
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        using var source = new CancellationTokenSource();
        using var limited = new Utf8Regex(pattern, options,
            new() { ResourcePolicy = new() { MaxWorkSteps = 4, MaxWorkingBytes = 1, MaxDepth = 1 } });
        using var ordinary = new Utf8Regex(pattern, options);

        Assert.Equal(new Utf8CaptureRange(0, 5), limited.Match("Name9"u8, cancellationToken: source.Token));
        Assert.Equal(new Utf8CaptureRange(0, 5), limited.Match("Name9\n"u8, cancellationToken: source.Token));
        Assert.False(limited.Match("Name9-"u8, cancellationToken: source.Token).Success);
        Assert.False(limited.Match("-Name9"u8, cancellationToken: source.Token).Success);
        Assert.Equal(new Utf8CaptureRange(0, 5), limited.Match("Key\n"u8, cancellationToken: source.Token));
        Assert.Equal(new Utf8CaptureRange(0, 10), limited.Match("AKBCK9"u8, cancellationToken: source.Token));
        Assert.False(limited.Match("AK😀9"u8, cancellationToken: source.Token).Success);
        Assert.False(limited.Match("😀Name9"u8, cancellationToken: source.Token).Success);
        Assert.False(ordinary.Match("Name9\nx"u8).Success);
    }

    [Fact]
    public void SingleOriginProofChecksALongerLineWithinTheSamePublicWorkLimit()
    {
        const string pattern = @"^\s*(\[\|\s*)?///\s?(?<doc>.*)$";
        using var regex = new Utf8Regex(pattern, RegexOptions.Compiled,
            new() { ResourcePolicy = new() { MaxWorkSteps = 4, MaxWorkingBytes = 4096 } });
        using var source = new CancellationTokenSource();
        var text = " [|  ///" + new string('x', 128) + "😀";

        Assert.Equal(new Utf8CaptureRange(0, Encoding.UTF8.GetByteCount(text)),
            regex.Match(Encoding.UTF8.GetBytes(text + "\n"), cancellationToken: source.Token));
        Assert.False(regex.Match(Encoding.UTF8.GetBytes(text + "\nx"), cancellationToken: source.Token).Success);
    }
}
