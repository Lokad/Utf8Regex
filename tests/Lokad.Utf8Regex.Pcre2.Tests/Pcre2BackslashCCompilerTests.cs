using System.Text;

using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BackslashCCompilerTests
{
    private static readonly Utf8Pcre2CompileSettings s_allowBackslashC = new()
    {
        BackslashC = Pcre2BackslashCPolicy.Allow
    };

    [Fact]
    public void BackslashCRemainsDisabledByDefaultButQuotedTextIsLiteral()
    {
        var exception = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(@"\C"));
        Assert.Equal(Pcre2ErrorKind.BackslashCDisabled, exception.ErrorKind);

        var quoted = new Utf8Pcre2Regex(@"^\Q\C\E$");
        Assert.True(quoted.IsMatch("\\C"u8));
    }

    [Fact]
    public void OptedInBackslashCConsumesOneByteAndReportsHonestCoordinates()
    {
        var regex = Create(@"\C");
        var match = regex.Match("é"u8);

        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(1, match.EndOffsetInBytes);
        Assert.True(match.HasContiguousByteRange);
        Assert.False(match.IsUtf8SliceWellFormed);
        Assert.Equal(new byte[] { 0xc3 }, match.GetValueBytes().ToArray());
    }

    [Fact]
    public void CapturedBackslashCReportsHonestCoordinates()
    {
        var regex = Create(@"(\C)");
        ReadOnlySpan<byte> match1Input = "😀"u8;
        var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match1Input, captures: match1Captures);

        Assert.True(match.Success);
        Assert.False(match.IsUtf8SliceWellFormed);
        var group = match1Captures[1];
        Assert.True(group.Success);
        Assert.Equal(0, group.StartOffsetInBytes);
        Assert.Equal(1, group.EndOffsetInBytes);
        Assert.False(group.IsUtf8SliceWellFormed);
        Assert.True((group.Success && group.StartOffsetInBytes <= group.EndOffsetInBytes));
        Assert.Equal(new byte[] { 0xf0 }, (group.Success ? match1Input.Slice(group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty).ToArray());
    }

    [Fact]
    public void CompleteScalarMadeOfCodeUnitsHasAValidByteValue()
    {
        var regex = Create(@"^\C\C$");
        var match = regex.Match("é"u8);

        Assert.True(match.Success);
        Assert.True(match.IsUtf8SliceWellFormed);
        Assert.Equal((0, 2), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.True(match.GetValueBytes().SequenceEqual("é"u8));
    }

    [Fact]
    public void ScalarAtomCannotDecodeFromInsideScalar()
    {
        var regex = Create(@"^\C.$");
        Assert.False(regex.IsMatch("é"u8));
    }

    [Fact]
    public void GlobalOperationsAdvanceByCodeUnitAfterSplitMatch()
    {
        var regex = Create(@"\C");
        var input = "é"u8;

        Assert.Equal(2, regex.Count(input));

        var enumerator = regex.EnumerateMatches(input);
        Assert.True(enumerator.MoveNext());
        Assert.Equal((0, 1), (enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        Assert.False(enumerator.Current.IsUtf8SliceWellFormed);
        Assert.True(enumerator.MoveNext());
        Assert.Equal((1, 2), (enumerator.Current.StartOffsetInBytes, enumerator.Current.EndOffsetInBytes));
        Assert.False(enumerator.Current.IsUtf8SliceWellFormed);
        Assert.False(enumerator.MoveNext());


    }

    [Fact]
    public void LiteralPrefixesKeepSplitByteRangesAndFindTheNextBranch()
    {
        using var regex = Create(@"ab(\C)|cd(\C)");
        ReadOnlySpan<byte> input = "axabcab😀cdéab"u8;
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[3];

        Assert.True(regex.IsMatch(input, startOffsetInBytes: 5));
        var match = regex.Match(input, startOffsetInBytes: 5, captures: captures);
        Assert.Equal((5, 8), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.False(match.IsUtf8SliceWellFormed);
        Assert.True(captures[1].Success);
        Assert.Equal((7, 8), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.False(captures[2].Success);
        Assert.Equal(3, regex.Count(input));

        var cursor = regex.EnumerateMatches(input);
        Assert.True(cursor.MoveNext());
        Assert.Equal((2, 5), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.True(cursor.MoveNext());
        Assert.Equal((5, 8), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.True(cursor.MoveNext());
        Assert.Equal((11, 14), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.False(cursor.MoveNext());
    }

    [Fact]
    public void FundedLiteralPrefixResumesAtOverlappingStartsAcrossSearchWindows()
    {
        var input = Encoding.UTF8.GetBytes("😀" + new string('x', 2046) + "aaabz");
        using var regex = new Utf8Pcre2Regex(@"aa\Cz", compileSettings: s_allowBackslashC, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 32 },
        });

        var match = regex.Match(input, startOffsetInBytes: 4);
        Assert.True(match.Success);
        Assert.Equal((2051, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
    }

    [Fact]
    public void FundedBranchPrefixesResumeAtOverlappingStartsAcrossSearchWindows()
    {
        var input = Encoding.UTF8.GetBytes("😀" + new string('x', 2044) + "aaaabxz");
        using var regex = new Utf8Pcre2Regex(@"aa\Cq|ab\Cz", compileSettings: s_allowBackslashC, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 96 },
        });

        var match = regex.Match(input, startOffsetInBytes: 4);
        Assert.True(match.Success);
        Assert.Equal((2051, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
    }

    [Fact]
    public void FundedBranchPrefixesKeepPatternOrderAtTheSameStart()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 4096) + "abczx");
        using var regex = new Utf8Pcre2Regex(@"ab(\C)z|abcz(\C)", compileSettings: s_allowBackslashC, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 64 },
        });
        Span<Utf8Pcre2CaptureRange> captures = stackalloc Utf8Pcre2CaptureRange[3];

        var match = regex.Match(input, captures: captures);
        Assert.True(match.Success);
        Assert.Equal((4096, 4100), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.True(captures[1].Success);
        Assert.Equal((4098, 4099), (captures[1].StartOffsetInBytes, captures[1].EndOffsetInBytes));
        Assert.False(captures[2].Success);
    }

    [Theory]
    [InlineData(@"ab\Cde", false)]
    [InlineData(@"ab\Cde", true)]
    [InlineData(@"ab\Cde|cd\Cef", false)]
    [InlineData(@"ab\Cde|cd\Cef", true)]
    public void LiteralPrefixWindowsObserveWorkLimitsAndOriginalInterruption(string pattern, bool cancel)
    {
        var input = Encoding.ASCII.GetBytes(new string('a', 256 * 1024));
        using var limited = new Utf8Pcre2Regex(pattern, compileSettings: s_allowBackslashC, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 16 },
        });
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => limited.IsMatch(input)).ErrorKind);

        using var source = new CancellationTokenSource();
        using var regex = new Utf8Pcre2Regex(pattern, compileSettings: s_allowBackslashC, policy: new()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
        });
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads < 10) return 0;
            if (!cancel) return 25;
            source.Cancel();
            return 0;
        });
        if (cancel)
        {
            var failure = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, cancellationToken: source.Token));
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        else
        {
            var failure = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input));
            Assert.Equal(Pcre2ErrorKind.Timeout, failure.ErrorKind);
        }
    }

    [Fact]
    public void ReplacementCanRemoveEveryCodeUnitWithoutAcceptingMalformedInput()
    {
        var regex = Create(@"\C");

        Assert.Equal("xx", Encoding.UTF8.GetString(regex.Replace("é"u8, "x"u8)));
    }

    [Fact]
    public void ByteReplacementPreservesAnExplicitSplitScalar()
    {
        var regex = Create(@"^\C");

        Assert.Equal(new byte[] { (byte)'x', 0xA9 }, regex.Replace("é"u8, "x"u8));
    }

    [Fact]
    public void OptionalCodeUnitUsesTheStandardEmptyMatchProgressRule()
    {
        var regex = Create(@"\C?");
        Assert.Equal(3, regex.Count("é"u8));
    }

    [Theory]
    [InlineData(@"(?<=\C)a")]
    [InlineData(@"(?<!\C)a")]
    public void BackslashCInLookbehindRemainsRejected(string pattern)
    {
        var exception = Assert.Throws<Pcre2CompileException>(() => Create(pattern));
        Assert.Equal(Pcre2ErrorKind.BackslashCInUtfLookbehind, exception.ErrorKind);
    }

    [Fact]
    public void BackslashCOutsideLookbehindIsNotRejectedBecausePatternAlsoHasLookbehind()
    {
        var regex = Create(@"(?<=a)\C");
        Assert.True(regex.IsMatch("ab"u8));
    }

    [Fact]
    public void BackslashCIsInvalidInsideACharacterClassEvenWhenOptedIn()
    {
        var exception = Assert.Throws<Pcre2CompileException>(() => Create(@"[\C]"));
        Assert.Equal(Pcre2ErrorKind.EscapeInvalidInClass, exception.ErrorKind);
    }

    [Fact]
    public void OptedInBackslashCIsSafeForConcurrentReuse()
    {
        var regex = Create(@"^(\C){4}$");
        Parallel.For(0, 1_000, _ => Assert.True(regex.IsMatch("😀"u8)));
    }

    private static Utf8Pcre2Regex Create(string pattern) =>
        new(pattern, options: Pcre2CompileOptions.None, compileSettings: s_allowBackslashC, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
}
