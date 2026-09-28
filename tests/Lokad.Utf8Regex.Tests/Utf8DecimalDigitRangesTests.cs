using System.Globalization;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// The owned Nd table must reproduce BCL DecimalDigitNumber classification over
// the whole BMP: astral digits stay BCL-routed with astral inputs, so only the
// BMP ships here.
public sealed class Utf8DecimalDigitRangesTests
{
    [Fact]
    public void DecimalDigitTableMatchesBclCategoryAcrossBmp()
    {
        for (var value = 0; value <= 0xFFFF; value++)
        {
            var expected = char.GetUnicodeCategory((char)value) == UnicodeCategory.DecimalDigitNumber;
            Assert.Equal(expected, Utf8DecimalDigitRanges.IsDecimalDigit((uint)value));
        }
    }
}
