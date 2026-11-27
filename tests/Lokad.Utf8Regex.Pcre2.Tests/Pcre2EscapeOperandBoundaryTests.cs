using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2EscapeOperandBoundaryTests
{
    [Theory]
    [InlineData(@"[\c](?C)]", @"[\x1D(?C)]", "(?C)\u001D")]
    [InlineData(@"[\c](?<1>)]", @"[\x1D(?<1>)]", "(?<1>)\u001D")]
    [InlineData(@"[\c](?y)]", @"[\x1D(?y)]", "(?y)\u001D")]
    [InlineData(@"[\c\Q(?C)]", @"[\x1CQ(?C)]", "Q(?C)\u001C")]
    [InlineData(@"\c\Q", @"\x1CQ", "\u001CQ")]
    [InlineData(@"\c((?<n>a)", @"\x68(?<n>a)", "ha")]
    public void CompleteControlOperandsCannotBecomeRegexDelimiters(string pattern, string equivalent, string subject)
    {
        var input = Encoding.UTF8.GetBytes(subject);
        var expected = new Utf8Pcre2Regex(equivalent);
        foreach (var regex in new[] { new Utf8Pcre2Regex(pattern), new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern)) })
        {
            Assert.Equal(expected.Count(input), regex.Count(input));
            Assert.True(regex.IsMatch(input));
            Assert.Equal(System.Text.Encoding.UTF8.GetString(expected.Match(input).GetValueBytes()), System.Text.Encoding.UTF8.GetString(regex.Match(input).GetValueBytes()));
            ReadOnlySpan<byte> expectedMatch1Input = input;
            var expectedMatch1Captures = new Utf8Pcre2CaptureRange[expected.CaptureGroupCount + 1];
            var expectedMatch = expected.Match(expectedMatch1Input, captures: expectedMatch1Captures);
            ReadOnlySpan<byte> actualMatch2Input = input;
            var actualMatch2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var actualMatch = regex.Match(actualMatch2Input, captures: actualMatch2Captures);
            Assert.Equal((expectedMatch.Success ? expectedMatch1Captures.Length : 0), (actualMatch.Success ? actualMatch2Captures.Length : 0));
            for (var slot = 0; slot < (expectedMatch.Success ? expectedMatch1Captures.Length : 0); slot++)
            {
                var expectedGroup = expectedMatch1Captures[slot];
                var actualGroup = actualMatch2Captures[slot];
                Assert.Equal(expectedGroup.Success, actualGroup.Success);
                if (expectedGroup.Success)
                    Assert.Equal(System.Text.Encoding.UTF8.GetString((expectedGroup.Success ? expectedMatch1Input.Slice(expectedGroup.StartOffsetInBytes, expectedGroup.EndOffsetInBytes - expectedGroup.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)), System.Text.Encoding.UTF8.GetString((actualGroup.Success ? actualMatch2Input.Slice(actualGroup.StartOffsetInBytes, actualGroup.EndOffsetInBytes - actualGroup.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
            }
            Assert.Equal(expected.NameEntryCount, regex.NameEntryCount);
            var expectedName = new byte[128];
            var actualName = new byte[128];
            for (var index = 0; index < regex.NameEntryCount; index++)
            {
                Assert.Equal(System.Buffers.OperationStatus.Done,
                    expected.TryCopyNameEntryUtf8(index, expectedName, out var expectedLength, out var expectedNumber));
                Assert.Equal(System.Buffers.OperationStatus.Done,
                    regex.TryCopyNameEntryUtf8(index, actualName, out var actualLength, out var actualNumber));
                Assert.Equal(expectedNumber, actualNumber);
                Assert.Equal(expectedLength, actualLength);
                Assert.Equal(expectedName.AsSpan(0, expectedLength).ToArray(), actualName.AsSpan(0, actualLength).ToArray());
            }
            Assert.False(regex.IsMatch("😀"u8));
        }
    }

    [Theory]
    [InlineData("\\c\\Q(?C\"label\")", Pcre2ErrorKind.CalloutUnsupported)]
    [InlineData(@"\c((?<1>a)", Pcre2ErrorKind.InvalidAfterParensQuery)]
    [InlineData(@"(?=\c)\K)i", Pcre2ErrorKind.LookaroundBackslashKDisabled)]
    [InlineData(@"[\c]\C]", Pcre2ErrorKind.BackslashCDisabled)]
    public void RealPolicyAndNameFailuresRemainVisibleAfterOperands(string pattern, Pcre2ErrorKind kind)
    {
        Assert.Equal(kind, Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern)).ErrorKind);
        Assert.Equal(kind, Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern))).ErrorKind);
    }

    [Theory]
    [InlineData(@"\Q[\c](?C)]\E", @"[\c](?C)]")]
    [InlineData("(?x)[\\c](?C)] # (?C)\n", "C")]
    [InlineData(@"\x{10000}", "𐀀")]
    [InlineData(@"\o{200000}", "𐀀")]
    public void QuotesCommentsAndBracedOperandsKeepTheirMeaning(string pattern, string subject)
    {
        Assert.Equal(subject, System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex(pattern).Match(Encoding.UTF8.GetBytes(subject)).GetValueBytes()));
    }

    [Fact]
    public void ControlOperandCannotServeAsTheMissingClassTerminator()
    {
        Assert.Equal(Pcre2ErrorKind.MissingCharacterClassTerminator,
            Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(@"[\c]")).ErrorKind);
    }

    [Fact]
    public void IncompleteBracedOperandCannotPublishAnExpression()
    {
        Assert.Throws<NotSupportedException>(() => new Utf8Pcre2Regex(@"\x{10000"));
    }
}
