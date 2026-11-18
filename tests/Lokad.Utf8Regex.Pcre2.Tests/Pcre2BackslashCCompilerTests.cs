using System.Text;

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
        Assert.True(regex.Analyze().MayProduceNonUtf8Slices);
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

        Span<Utf8Pcre2MatchData> matches = stackalloc Utf8Pcre2MatchData[2];
        Assert.Equal(2, regex.MatchMany(input, matches, out var isMore));
        Assert.False(isMore);
    }

    [Fact]
    public void ReplacementCanRemoveEveryCodeUnitWithoutAcceptingMalformedInput()
    {
        var regex = Create(@"\C");

        Assert.Equal("xx", Encoding.UTF8.GetString(regex.Replace("é"u8, "x")));
    }

    [Fact]
    public void ByteReplacementPreservesAnExplicitSplitScalar()
    {
        var regex = Create(@"^\C");

        Assert.Equal(new byte[] { (byte)'x', 0xA9 }, regex.Replace("é"u8, "x"));
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
