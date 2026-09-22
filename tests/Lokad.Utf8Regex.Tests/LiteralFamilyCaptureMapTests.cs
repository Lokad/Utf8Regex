using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R03 branch-to-group capture maps: pins which literal-alternation shapes map to owned
/// per-branch group spans and which keep declining to the BCL routes.
/// </summary>
public sealed class LiteralFamilyCaptureMapTests
{
    [Theory]
    [InlineData("(cat)|(horse)", "whole=[];branches=[1;2]")]
    [InlineData("(?<w>cat)|(?<v>horse)", "whole=[];branches=[1;2]")]
    [InlineData("(cat)|horse", "whole=[];branches=[1;]")]
    [InlineData("horse|(cat)", "whole=[];branches=[;1]")]
    [InlineData("(?:(a)|(b))", "whole=[];branches=[1;2]")]
    [InlineData("((cat)|(horse))", "whole=[1];branches=[2;3]")]
    [InlineData("(cat|horse)", "whole=[1];branches=[;]")]
    [InlineData("cat|horse", "whole=[];branches=[;]")]
    [InlineData("(a)|(ab)", "whole=[];branches=[1;2]")]
    [InlineData("(cat)|(horse)", "whole=[];branches=[1;2]")]
    [InlineData("(a)|(b)|(c)", "whole=[];branches=[1;2;3]")]
    public void LiteralFamilyCaptureMapMatchesExpectedShape(string pattern, string expected)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.Inspection.DebugLiteralFamilyCaptureMapSummary());
    }

    [Theory]
    [InlineData("(a)(b)|C")]
    [InlineData("\\b(cat)|(horse)\\b")]
    [InlineData("[a-z]+")]
    [InlineData("(a)+")]
    [InlineData("abc")]
    [InlineData("(?<=a)(b)|(c)")]
    public void UnsupportedShapesDeclineCaptureMapping(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal("declined", regex.Inspection.DebugLiteralFamilyCaptureMapSummary());
    }
}
