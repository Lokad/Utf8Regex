using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// Owned shorthand tables must reproduce their BCL oracles over the whole BMP:
// White_Space against char.IsWhiteSpace and invariant word against the BCL \\w
// engine. Astral members stay BCL-routed with astral inputs, so only the BMP
// ships in either table.
public sealed class Utf8ShorthandTableTests
{
    [Fact]
    public void WhiteSpaceTableMatchesBclAcrossBmp()
    {
        for (var value = 0; value <= 0xFFFF; value++)
        {
            Assert.Equal(char.IsWhiteSpace((char)value), Utf8WhiteSpaceRanges.IsWhiteSpace((uint)value));
        }
    }

    [Fact]
    public void WordTableMatchesBclEngineAcrossBmp()
    {
        var oracle = new Regex(@"\w", RegexOptions.CultureInvariant | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
        for (var value = 0; value <= 0xFFFF; value++)
        {
            Assert.Equal(oracle.IsMatch(((char)value).ToString()), Utf8WordRanges.IsWordChar((uint)value));
        }
    }
}
