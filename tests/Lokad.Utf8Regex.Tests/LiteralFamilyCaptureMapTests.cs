using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

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
        var tree = Utf8ExecutionTreeLowerer.Lower(Utf8FrontEnd.Analyze(pattern, options).SemanticRegex);
        var map = Utf8LiteralFamilyCaptureMap.TryCreate(tree);
        Assert.NotNull(map);
        var summary = "whole=[" + string.Join(",", map.WholeMatchGroups) + "];branches=[" +
            string.Join(";", map.BranchGroups.Select(static groups => string.Join(",", groups))) + "]";
        Assert.Equal(expected, summary);
    }

    [Theory]
    [InlineData("(a)(b)|C")]
    [InlineData("\\b(cat)|(horse)\\b")]
    [InlineData("[a-z]+")]
    [InlineData("(a)+")]
    [InlineData("(?<=a)(b)|(c)")]
    public void UnsupportedShapesDeclineCaptureMapping(string pattern)
    {
        var tree = Utf8ExecutionTreeLowerer.Lower(Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant).SemanticRegex);
        Assert.Null(Utf8LiteralFamilyCaptureMap.TryCreate(tree));
    }
}
