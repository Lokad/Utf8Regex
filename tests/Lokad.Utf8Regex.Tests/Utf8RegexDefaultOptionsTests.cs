using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexDefaultOptionsTests
{
    [Fact]
    public void DefaultConstructorUsesCultureInvariant()
    {
        var regex = new Utf8Regex("abc");

        Assert.Equal(RegexOptions.CultureInvariant, regex.Options);
    }

}
