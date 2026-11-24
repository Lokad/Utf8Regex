using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Tests;

internal static class Utf8GroupNames
{
    internal static string[] Read(Utf8Regex regex)
    {
        var names = new string[regex.CaptureGroupCount + 1];
        for (var slot = 0; slot < names.Length; slot++)
        {
            Assert.Equal(OperationStatus.DestinationTooSmall, regex.TryCopyGroupNameUtf8(slot, [], out var length));
            var bytes = new byte[length];
            Assert.Equal(OperationStatus.Done, regex.TryCopyGroupNameUtf8(slot, bytes, out var written));
            Assert.Equal(length, written);
            names[slot] = Encoding.UTF8.GetString(bytes);
        }
        return names;
    }
}
