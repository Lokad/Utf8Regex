using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

internal static class PythonReMatchAssert
{
    internal static void Equal(string? expected, Utf8PythonValueMatch actual)
    {
        Assert.Equal(expected is not null, actual.Success);
        if (expected is not null)
            Assert.Equal(Encoding.UTF8.GetBytes(expected), actual.GetValueBytes().ToArray());
    }
}
