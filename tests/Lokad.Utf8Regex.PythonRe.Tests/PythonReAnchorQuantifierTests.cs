using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAnchorQuantifierTests
{
    [Theory]
    [InlineData("^*", 1)]
    [InlineData("$+", 1)]
    [InlineData(@"\b?", 2)]
    [InlineData(@"\B{,2}", 2)]
    [InlineData(@"\A{0}", 2)]
    [InlineData(@"\Z{2}", 2)]
    [InlineData("^(?#x)*", 6)]
    [InlineData(@"(?a)\b+", 6)]
    [InlineData(@"(?a)\B*", 6)]
    public void DirectAnchorsAndBoundariesCannotBeQuantified(string pattern, int position)
    {
        foreach (var bytes in new[] { false, true })
        {
            var error = Assert.Throws<PythonRePatternException>(() =>
            {
                if (bytes) _ = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern));
                else _ = new Utf8PythonRegex(pattern);
            });
            Assert.Equal("nothing to repeat", error.Message);
            Assert.Equal(position, error.Position);
            Assert.False(Utf8ConstructionScope.IsActive);
        }
    }

    [Theory]
    [InlineData("(?:^)*", "")]
    [InlineData("(?=a)*a", "a")]
    [InlineData("(?!b)+a", "a")]
    [InlineData("(?<=a)?b", "b")]
    [InlineData(@"[\b]*", "\b\b")]
    [InlineData("^{}$", "{}")]
    [InlineData("^a{0}$", "")]
    [InlineData(@"(?:\b)+a", "a")]
    [InlineData("(?=(?P<x>😀))*😀", "😀")]
    public void GroupsAssertionsLiteralBracesAndClassEscapesKeepTheirGrammar(string pattern, string text)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Utf8PythonRangeMatch snapshot1 = default;
        using (var captureCursor1 = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(text), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out snapshot1);
        }
        Assert.True(snapshot1.Success);
    }

    [Theory]
    [InlineData("^{2,1}", "min repeat greater than max repeat", 2)]
    [InlineData(@"[z-\n]", @"bad character range z-\n", 1)]
    // This profile supplies the offending UTF-16 digit position for overflow;
    // CPython uses OverflowError without a pattern position.
    [InlineData("^{4294967295}", "the repetition number is too large", 11)]
    public void QuantifierBoundErrorsStillPrecedeAtomEligibility(string pattern, string message, int position)
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Equal(message, error.Message);
        Assert.Equal(position, error.Position);
    }

    [Theory]
    [InlineData(@"\a", '\a', 'a')]
    [InlineData(@"\b", '\b', 'b')]
    [InlineData(@"\f", '\f', 'f')]
    [InlineData(@"\n", '\n', 'n')]
    [InlineData(@"\r", '\r', 'r')]
    [InlineData(@"\t", '\t', 't')]
    [InlineData(@"\v", '\v', 'v')]
    public void BracketEscapesKeepControlValuesInsteadOfLetters(string escape, char control, char letter)
    {
        foreach (var options in new[] { PythonReCompileOptions.None, PythonReCompileOptions.Ascii, PythonReCompileOptions.IgnoreCase })
        {
            var positive = new Utf8PythonRegex("[" + escape + "]+", options);
            var negative = new Utf8PythonRegex("[^" + escape + "]", options);
            var controlInput = Encoding.UTF8.GetBytes(new string(control, 2));
            var letterInput = Encoding.UTF8.GetBytes(letter.ToString());
            Assert.True(positive.Match(controlInput,mode: Utf8PythonMatchMode.FullMatch).Success);
            Assert.False(positive.Match(letterInput,mode: Utf8PythonMatchMode.FullMatch).Success);
            Assert.True(negative.Match(letterInput,mode: Utf8PythonMatchMode.FullMatch).Success);
            Assert.False(negative.Match(controlInput.AsSpan(0, 1),mode: Utf8PythonMatchMode.FullMatch).Success);
        }
    }

    [Fact]
    public void ControlEscapesAlsoSupplyActualClassRangeEndpoints()
    {
        Assert.True(new Utf8PythonRegex(@"[\b-\r]+").Match("\b\t\n\v\f\r"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(new Utf8PythonRegex(@"[\b-\r]+").Match("bfnrtv"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
    }
}
