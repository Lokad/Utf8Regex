using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

internal static class PythonReCaptureAssert
{
    internal static string[] Values(Utf8PythonRangeMatch match)
    {
        var result = new string[match.Groups.Length];
        for (var number = 0; number < result.Length; number++)
            result[number] = Encoding.UTF8.GetString(match.GetGroupMemory(number).Span);
        return result;
    }

    internal static void Equal(Utf8PythonRangeMatch expected, Utf8PythonRangeMatch actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.LastIndex, actual.LastIndex);
        Assert.Equal(expected.LastGroup, actual.LastGroup);
        Assert.Equal(expected.NameEntries.ToArray(), actual.NameEntries.ToArray());
        Assert.Equal(expected.Groups.ToArray(), actual.Groups.ToArray());
        for (var number = 0; number < expected.Groups.Length; number++)
            Assert.True(expected.GetGroupMemory(number).Span.SequenceEqual(actual.GetGroupMemory(number).Span));
    }

    internal static void Equal(Utf8PythonRangeMatch[] expected, Utf8PythonRangeMatch[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++) Equal(expected[index], actual[index]);
    }

}
