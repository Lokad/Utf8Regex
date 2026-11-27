using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuralBoundaryOverlapTests
{
    private const string Declaration = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";

    [Theory]
    [InlineData(Declaration, "class Alpha struct Beta record Gamma")]
    [InlineData(Declaration, "éclass Wrong 😀class\u2003Right _struct Wrong record Good")]
    [InlineData(@"\b(?:ab|ba)\s+[A-Z][A-Za-z0-9_]+", "aba Wrong ab Good ba Right")]
    [InlineData(@"\b(?:foobar|bar)\s+[A-Z][A-Za-z0-9_]+", "foobar Good bar Right")]
    [InlineData(@"\b(?:foo|foobar)[A-Z][A-Za-z0-9_]*", "foobarAlpha fooBeta")]
    [InlineData(@"\b(?:foo:|:bar)[A-Z][A-Za-z0-9_]*", "foo:barAlpha foo:Beta")]
    [InlineData(@"\b(?:foo|foobar|qux)[A-Z][A-Za-z0-9_]*", "foobarAlpha fooBeta quxGamma")]
    [InlineData(@"\b(?:record|struct|class)(\s+)([A-Z][A-Za-z0-9_]+)", "record Alpha; class Beta")]
    [InlineData(@"(?:class|struct)\s+[A-Z][A-Za-z0-9_]+", "classstruct Good class Right")]
    public void BoundariesAndOverlappingPrefixesPreservePublicCount(string pattern, string text)
    {
        using var token = new CancellationTokenSource();
        var longText = new string(' ', 260) + text;
        var longInput = Encoding.UTF8.GetBytes(longText);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            using var regex = new Utf8Regex(pattern, options: options);
            var expected = new Regex(pattern).Count(text);
            Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text)));
            Assert.Equal(expected, regex.Count(longInput, token.Token));
        }
    }

    [Theory]
    [InlineData("class Alpha struct Beta record Gamma")]
    [InlineData("classstruct Wrong class lower struct Z record A_ class A1")]
    [InlineData("class\t\r\nAlpha struct Beta record Gamma")]
    [InlineData("class" + "                                                                " + "Alpha")]
    [InlineData("éclass Wrong 😀class\u2003Right struct lower record Good")]
    [InlineData("class Alphaé struct Beta中 record Gamma😀")]
    [InlineData("")]
    [InlineData("class A struct Z record a")]
    public void PublicOperationsPreserveRangesAndControlledRoutes(string text)
    {
        var expected = new Regex(Declaration).Matches(text);
        var buffer = Encoding.UTF8.GetBytes("pad" + text + "tail");
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        foreach (var controlled in new[] { false, true })
        {
            var regex = new Utf8Regex(Declaration, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = controlled ? new()
                {
                    MaxDepth = 128,
                    MaxWorkingBytes = 1000000,
                    MaxResultBytes = 1000000
                }

                : default
            });
            var input = buffer.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            Assert.Equal(expected.Count, regex.Count(input));
            Assert.Equal(expected.Count, regex.Count(input, cancellation.Token));
            Assert.Equal(expected.Count != 0, regex.IsMatch(input));
            Assert.Equal(expected.Count != 0, regex.IsMatch(input, cancellation.Token));
            var matches = new Regex(Declaration, options).Matches(text);
            var first = regex.Match(input);
            Assert.Equal(matches.Count != 0, first.Success);
            var cursor = regex.EnumerateMatches(input);
            var index = 0;
            while (cursor.MoveNext())
            {
                var match = matches[index++];
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)), cursor.Current.StartOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(match.Value), (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes));
                }
            Assert.Equal(matches.Count, index);
            if (matches.Count != 0)
            {
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, matches[0].Index)), first.StartOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(matches[0].Value), (first.EndOffsetInBytes - first.StartOffsetInBytes));
                }
        }
    }



    [Theory]
    [InlineData(@"\b(?:call|tell)\s+[A-Z][A-Za-z0-9_]+", "call A tell Bee call Z")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]{0,2}", "class ABCDE struct Z record A1")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]{2,3}", "class A struct B2 record AbcdE")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]{0,2}\b", "class ABCDE struct Z record A1")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]*", "class Alpha struct B record C")]
    [InlineData(@"\b(?:record|struct|class)[ \t]+[A-Z][A-Za-z0-9_]*", "class\nWrong struct\tGood record Right")]
    public void NativeIdentifierVerificationPreservesTailBounds(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern).Matches(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(expected.Count, regex.Count(input));
            Assert.Equal(expected.Count != 0, regex.IsMatch(input));
            var first = regex.Match(input);
            if (expected.Count != 0)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[0].Index)), first.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected[0].Index, expected[0].Length)), (first.EndOffsetInBytes - first.StartOffsetInBytes));
            }
            var cursor = regex.EnumerateMatches(input);
            var index = 0;
            while (cursor.MoveNext())
            {
                var match = expected[index++];
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)), cursor.Current.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(match.Index, match.Length)), (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes));
            }
            Assert.Equal(expected.Count, index);
        }
    }
}
