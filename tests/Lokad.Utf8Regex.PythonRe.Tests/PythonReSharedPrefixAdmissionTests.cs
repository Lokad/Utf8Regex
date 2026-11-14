using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSharedPrefixAdmissionTests
{
    [Fact]
    public void SharedPrefixSkipsFalseHeadsWithinTheOriginalFinitePolicy()
    {
        var input = Encoding.UTF8.GetBytes(new string('i', 65536) + "tem-12");
        using var regex = new Utf8PythonRegex("item-([0-9]+)", resourcePolicy: new()
        { MaxWorkSteps = 2000, MaxWorkingBytes = 1UL << 20, MaxResultBytes = 1UL << 20 });
        using var cancellation = new CancellationTokenSource();
        var result = regex.FindAllToStrings(input, 0, cancellation.Token);
        Assert.Equal(Utf8PythonFindAllShape.SingleGroup, result.Shape);
        Assert.Equal("12", Assert.Single(result.ScalarValues));
        Assert.Empty(result.TupleValues);
        _ = regex.FindAllToStrings("no match"u8);
        Assert.Equal("12", result.ScalarValues[0]);
    }
}
