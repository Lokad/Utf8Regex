using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

internal static class PythonReRangeAssert
{
    internal static void Equal(Utf8PythonDetailedMatchData expected, Utf8PythonRangeMatch actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.LastIndex, actual.LastIndex);
        Assert.Equal(expected.LastGroup, actual.LastGroup);
        Assert.Equal(expected.NameEntries, actual.NameEntries.ToArray());
        Assert.Equal(expected.Groups.Length, actual.Groups.Length);
        for (var number = 0; number < expected.Groups.Length; number++)
        {
            var wanted = expected.Groups[number];
            Assert.True(actual.TryGetGroup(number, out var range));
            Assert.Equal(wanted.Number, range.Number);
            Assert.Equal(wanted.Success, range.Success);
            Assert.Equal(wanted.StartOffsetInBytes, range.StartOffsetInBytes);
            Assert.Equal(wanted.EndOffsetInBytes, range.EndOffsetInBytes);
            Assert.Equal(wanted.ValueText, Encoding.UTF8.GetString(actual.GetGroupMemory(number).Span));
        }
    }
}
