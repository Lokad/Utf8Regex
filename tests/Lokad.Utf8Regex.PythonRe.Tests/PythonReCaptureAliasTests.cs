using System.Reflection;
using System.Text;
using Lokad.Utf8Regex.Benchmarks;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureAliasTests
{
    // CPython 3.13.2 facts. Subjects are ASCII here, so the reference renderer
    // also tests the same language without a UTF-16/scalar semantic difference.
    [Theory]
    [InlineData("(?P<α>a)(?P<g1>b)(?P=α)", "aba", "abb", "α", "g1")]
    [InlineData("(?P<g2>a)(?P<α>b)(?P=g2)", "aba", "abb", "g2", "α")]
    [InlineData("(?P<é>a)(?P<g1>b)(?P=é)", "aba", "abb", "é", "g1")]
    [InlineData("(?P<𐐀>a)(?P<g1>b)(?P=𐐀)", "aba", "abb", "𐐀", "g1")]
    [InlineData("(?P<α>a)(?P<g1>b)\\1", "aba", "abb", "α", "g1")]
    [InlineData("(?P<α>a)+(?P<g1>b)(?P=α)", "aaba", "aabb", "α", "g1")]
    [InlineData("(?P<α>a)?(?P<g1>b)(?(α)a|c)", "aba", "abb", "α", "g1")]
    public void NativeAliasesAndOwnedNamesPreserveDistinctCaptureIdentities(
        string pattern, string hit, string miss, string first, string second)
    {
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        var translation = PythonReTranslator.Translate(parse, true);
        var native = new Utf8Regex(translation.NativePattern, translation.NativeRegexOptions);
        var input = Encoding.UTF8.GetBytes(hit);
        var absent = Encoding.UTF8.GetBytes(miss);
        Assert.True(native.IsMatch(input));
        Assert.False(native.IsMatch(absent));
        Assert.Equal(["0", "g1", "g2"], native.GetGroupNames());
        var comparator = new System.Text.RegularExpressions.Regex(PythonReDotNetReference.Render(parse));
        Assert.Matches(comparator, hit);
        Assert.DoesNotMatch(comparator, miss);
        var owned = PythonReScalarCompiler.Compile(parse);
        Assert.Equal(["0", first, second], owned.GroupNames);
        foreach (var finite in new[] { false, true })
        {
            var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
                finite ? new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 2048,
                    MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 } : default);
            Assert.Equal(["0", first, second], regex.GetGroupNames());
            Assert.True(regex.IsMatch(input));
            Assert.False(regex.IsMatch(absent));
            Assert.Equal(1, regex.Count(input));
            Assert.True(regex.FullMatch(input).Success);
            var retained = regex.SearchDetailedData(input);
            Assert.Equal(2, retained.LastIndex);
            Assert.Equal(second, retained.LastGroup);
            Assert.True(retained.TryGetFirstSetGroup(first, out var a));
            Assert.True(retained.TryGetFirstSetGroup(second, out var b));
            Assert.Equal("a", a.ValueText);
            Assert.Equal("b", b.ValueText);
            Assert.Equal(["a", "b"], Assert.Single(regex.FindAllToStrings(input).TupleValues));
            Assert.Equal("a|b", regex.ReplaceToString(input, "\\g<" + first + ">|\\g<" + second + ">"));
            Assert.Equal("a", retained.Groups[1].ValueText);
            Assert.Equal("b", retained.Groups[2].ValueText);
            Assert.Throws<ArgumentException>(() => regex.Count([0xff]));
        }
    }

    [Fact]
    public void MixedNumberedAndNamedReferencesKeepPythonOpeningOrder()
    {
        const string pattern = "(?P<α>a)(b)(?P<g1>c)(?P=α)\\2";
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        var translation = PythonReTranslator.Translate(parse, true);
        var native = new Utf8Regex(translation.NativePattern, translation.NativeRegexOptions);
        Assert.True(native.IsMatch("abcab"u8));
        Assert.False(native.IsMatch("abccb"u8));
        var regex = new Utf8PythonRegex(pattern);
        var match = regex.FullMatchDetailedData("abcab"u8);
        Assert.True(match.Success);
        Assert.Equal(["0", "α", "2", "g1"], regex.GetGroupNames());
        Assert.Equal(3, match.LastIndex);
        Assert.Equal("g1", match.LastGroup);
        Assert.Equal(["abcab", "a", "b", "c"], match.Groups.Select(group => group.ValueText).ToArray());
    }

    [Fact]
    public void DistinctNamesKeepCompleteLiteralDiscoveryEligible()
    {
        var regex = new Utf8PythonRegex("(?P<α>a)(?P<g1>b)");
        var core = Assert.IsType<Utf8Regex>(typeof(Utf8PythonRegex)
            .GetField("_utf8Regex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex));
        Assert.True(core.CanFindNativeLiteralForResult);
        Assert.Equal(["0", "α", "g1"], regex.GetGroupNames());
        Assert.True(regex.IsMatch("é😀ab"u8));
        var match = regex.SearchDetailedData("é😀ab"u8);
        Assert.Equal(6, match.Value.StartOffsetInBytes);
        Assert.Equal("a", match.Groups[1].ValueText);
        Assert.Equal("b", match.Groups[2].ValueText);
    }
}
