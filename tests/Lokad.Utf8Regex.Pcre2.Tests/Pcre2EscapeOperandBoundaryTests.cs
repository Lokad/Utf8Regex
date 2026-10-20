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
            Assert.Equal(expected.Match(input).GetValueString(), regex.Match(input).GetValueString());
            Assert.Equal(expected.DebugCompiledProgram.GroupNames, regex.DebugCompiledProgram.GroupNames);
            Assert.False(regex.IsMatch("😀"u8));
        }
    }

    [Theory]
    [InlineData(@"\c\Q(?C)", Pcre2ErrorKind.CalloutUnsupported)]
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
        Assert.Equal(subject, new Utf8Pcre2Regex(pattern).Match(Encoding.UTF8.GetBytes(subject)).GetValueString());
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
