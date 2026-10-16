using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R03 branch-to-group capture maps: pins which literal-alternation shapes map to owned
/// per-branch group spans and which retain scalar capture execution.
/// </summary>
public sealed class LiteralFamilyCaptureMapTests
{
    [Theory]
    [InlineData("(cat)|(horse)", RegexOptions.CultureInvariant, "whole=[];branches=[1;2]")]
    [InlineData("(?<w>cat)|(?<v>horse)", RegexOptions.CultureInvariant, "whole=[];branches=[1;2]")]
    [InlineData("(cat)|horse", RegexOptions.CultureInvariant, "whole=[];branches=[1;]")]
    [InlineData("horse|(cat)", RegexOptions.CultureInvariant, "whole=[];branches=[;1]")]
    [InlineData("(?:(a)|(b))", RegexOptions.CultureInvariant, "whole=[];branches=[1;2]")]
    [InlineData("((cat)|(horse))", RegexOptions.CultureInvariant, "whole=[1];branches=[2;3]")]
    [InlineData("(cat|horse)", RegexOptions.CultureInvariant, "whole=[1];branches=[;]")]
    [InlineData("cat|horse", RegexOptions.CultureInvariant, "whole=[];branches=[;]")]
    [InlineData("(a)|(ab)", RegexOptions.CultureInvariant, "whole=[];branches=[1;2]")]
    [InlineData("(a)|(b)|(c)", RegexOptions.CultureInvariant, "whole=[];branches=[1;2;3]")]
    [InlineData("(CAT)|(DOG)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "whole=[];branches=[1;2]")]
    [InlineData("abc", RegexOptions.CultureInvariant, "whole=[];branches=[]")]
    [InlineData("(abc)", RegexOptions.CultureInvariant, "whole=[1];branches=[]")]
    public void LiteralFamilyCaptureMapMatchesExpectedShape(string pattern, RegexOptions options, string expected)
    {
        var regex = new Utf8Regex(pattern, options);
        Assert.Equal(expected, regex.Inspection.DebugLiteralFamilyCaptureMapSummary());
    }

    [Theory]
    [InlineData("(a)(b)|C")]
    [InlineData("\\b(cat)|(horse)\\b")]
    [InlineData("[a-z]+")]
    [InlineData("(a)+")]
    [InlineData("(?<=a)(b)|(c)")]
    public void UnsupportedShapesDeclineCaptureMapping(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal("declined", regex.Inspection.DebugLiteralFamilyCaptureMapSummary());
    }
}
