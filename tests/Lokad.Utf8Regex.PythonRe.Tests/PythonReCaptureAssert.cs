using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

internal static class PythonReCaptureAssert
{
    internal static string[] Values(Utf8PythonMatchContext match)
    {
        var result = new string[match.CaptureSlotCount];
        for (var number = 0; number < result.Length; number++)
            result[number] = Encoding.UTF8.GetString(match.GetGroup(number).Value.GetValueBytes());
        return result;
    }

    internal static void Equal(Utf8PythonMatchContext expected, Utf8PythonMatchContext actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.LastIndex, actual.LastIndex);
        Assert.Equal(expected.LastGroup, actual.LastGroup);
        Assert.Equal(expected.GetNameEntries(), actual.GetNameEntries());
        Assert.Equal(expected.CaptureSlotCount, actual.CaptureSlotCount);
        for (var number = 0; number < expected.CaptureSlotCount; number++)
        {
            var wanted = expected.GetGroup(number); var found = actual.GetGroup(number);
            Assert.Equal(wanted.Number, found.Number);
            Assert.Equal(wanted.Success, found.Success);
            Assert.Equal(wanted.Value.StartOffsetInBytes, found.Value.StartOffsetInBytes);
            Assert.Equal(wanted.Value.EndOffsetInBytes, found.Value.EndOffsetInBytes);
            Assert.True(wanted.Value.GetValueBytes().SequenceEqual(found.Value.GetValueBytes()));
        }
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

    internal static void Equal(Utf8PythonMatchContext expected, Utf8PythonRangeMatch actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.LastIndex, actual.LastIndex);
        Assert.Equal(expected.LastGroup, actual.LastGroup);
        Assert.Equal(expected.GetNameEntries(), actual.NameEntries.ToArray());
        Assert.Equal(expected.CaptureSlotCount, actual.Groups.Length);
        for (var number = 0; number < expected.CaptureSlotCount; number++)
        {
            var wanted = expected.GetGroup(number);
            Assert.True(actual.TryGetGroup(number, out var range));
            Assert.Equal(wanted.Number, range.Number);
            Assert.Equal(wanted.Success, range.Success);
            Assert.Equal(wanted.Value.StartOffsetInBytes, range.StartOffsetInBytes);
            Assert.Equal(wanted.Value.EndOffsetInBytes, range.EndOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetString(wanted.Value.GetValueBytes()), Encoding.UTF8.GetString(actual.GetGroupMemory(number).Span));
        }
    }
}
