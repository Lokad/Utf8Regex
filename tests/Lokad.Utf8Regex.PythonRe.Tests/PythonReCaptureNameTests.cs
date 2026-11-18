using System.Buffers;
using System.Globalization;
using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureNameTests
{
    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(299)]
    [InlineData(300)]
    [InlineData(999)]
    [InlineData(1000)]
    public void ScalarNamesKeepInvariantDecimalLabelsAcrossDigitBoundaries(int count)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            var regex = new Utf8PythonRegex(string.Concat(Enumerable.Repeat("(a)", count)));
            Assert.Equal(count, regex.CaptureGroupCount);
            Span<byte> destination = stackalloc byte[12];
            for (var slot = 0; slot <= count; slot++)
            {
                Assert.Equal(OperationStatus.Done, regex.TryCopyGroupNameUtf8(slot, destination, out var required));
                var expected = Encoding.UTF8.GetBytes(slot.ToString(CultureInfo.InvariantCulture));
                Assert.True(destination[..required].SequenceEqual(expected));
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Fact]
    public void SharedScalarMetadataRetainsClosingOrderForLastUnnamedCapture()
    {
        var regex = new Utf8PythonRegex("(?P<first>a)(b)(?P<third>c)(d)");
        string[] expected = ["0", "first", "2", "third", "4"];
        Span<byte> destination = stackalloc byte[16];
        for (var slot = 0; slot < expected.Length; slot++)
        {
            Assert.Equal(OperationStatus.Done, regex.TryCopyGroupNameUtf8(slot, destination, out var required));
            Assert.Equal(expected[slot], Encoding.UTF8.GetString(destination[..required]));
        }
        using var cursor = regex.CreateMatchCursor("abcd"u8.ToArray());
        Assert.True(cursor.MoveNext(out var result));
        Assert.Equal(4, result.LastIndex);
    }
}
