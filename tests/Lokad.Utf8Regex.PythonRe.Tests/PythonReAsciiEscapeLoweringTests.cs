using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAsciiEscapeLoweringTests
{
    [Theory]
    [InlineData('w')]
    [InlineData('d')]
    [InlineData('s')]
    [InlineData('u')]
    public void AsciiBracketUnionsKeepResolvedOperandsAndScalarComplements(char kind)
    {
        var body = kind switch { 'w' => @"\w.-", 'd' => @"\dA-F", 's' => @"\s\x1c-", _ => @"\w\d\s-" };
        foreach (var negate in new[] { false, true })
            foreach (var bytes in new[] { false, true })
            {
                var pattern = "(?ai:(?P<x>[" + (negate ? "^" : "") + body + "]))";
                using var regex = bytes
                    ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern))
                    : new Utf8PythonRegex(pattern);
                foreach (var scalar in Enumerable.Range(0, 128).Concat(new[] { 0xE9, 0x660, 0xA0, 0x212A, 0x10400, 0x1F600 }))
                {
                    var word = scalar is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';
                    var digit = scalar is >= '0' and <= '9';
                    var space = scalar is >= 9 and <= 13 or 32;
                    var positive = kind switch
                    {
                        'w' => word || scalar is '.' or '-',
                        'd' => digit || scalar is >= 'A' and <= 'F' or >= 'a' and <= 'f',
                        's' => space || scalar is 0x1C or '-',
                        _ => word || digit || space || scalar == '-',
                    };
                    var text = char.ConvertFromUtf32(scalar);
                    var input = Encoding.UTF8.GetBytes(text);
                    Utf8PythonRangeMatch actual = default;
                    using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
                    {
                        captureCursor1.MoveNext(out actual);
                    }
                    Assert.Equal(negate ? !positive : positive, actual.Success);
                    if (!actual.Success) continue;
                    Assert.Equal(text, System.Text.Encoding.UTF8.GetString((actual.Groups[1].Success ? actual.GetGroupMemory(actual.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
                    Assert.Equal((0, input.Length), (actual.Groups[1].StartOffsetInBytes, actual.Groups[1].EndOffsetInBytes));
                }
            }
    }
    [Theory]
    [InlineData('w')]
    [InlineData('W')]
    [InlineData('d')]
    [InlineData('D')]
    [InlineData('s')]
    [InlineData('S')]
    public void AsciiEscapesKeepTheirScalarComplementAndCaptureCoordinates(char escape)
    {
        foreach (var bytes in new[] { false, true })
        {
            var pattern = @"(?ai:(?P<x>\" + escape + "))";
            using var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern))
                : new Utf8PythonRegex(pattern);
            // ASCII whitespace deliberately omits U+001C..U+001F. The wider
            // scalars also distinguish ASCII classes from Unicode predicates
            // and catch accidental folding of Kelvin sign into ASCII word.
            foreach (var scalar in Enumerable.Range(0, 128).Concat(new[] { 0xE9, 0x660, 0xA0, 0x212A, 0x10400, 0x1F600 }))
            {
                var text = char.ConvertFromUtf32(scalar);
                var input = Encoding.UTF8.GetBytes(text);
                var positive = char.ToLowerInvariant(escape) switch
                {
                    'w' => scalar is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_',
                    'd' => scalar is >= '0' and <= '9',
                    _ => scalar is >= 9 and <= 13 or 32,
                };
                var expected = char.IsLower(escape) ? positive : !positive;
                Utf8PythonRangeMatch actual = default;
                using (var captureCursor2 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
                {
                    captureCursor2.MoveNext(out actual);
                }
                Assert.Equal(expected, actual.Success);
                if (!expected) continue;
                Assert.Equal(text, System.Text.Encoding.UTF8.GetString((actual.Groups[1].Success ? actual.GetGroupMemory(actual.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
                Assert.Equal((0, input.Length), (actual.Groups[1].StartOffsetInBytes, actual.Groups[1].EndOffsetInBytes));
            }
            Assert.False(regex.Match(ReadOnlySpan<byte>.Empty,mode: Utf8PythonMatchMode.FullMatch).Success);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScopedModesRestoreUnicodePredicatesAndKeepGlobalResults(bool union)
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var regex = new Utf8PythonRegex(union ? @"(?ai:([\w-]))(?u:([\w-]))(?a:([^\w-]))"
            : @"(?ai:(\w))(?u:(\w))(?a:(\W))");
        var input = "aé😀 bé😀"u8.ToArray();
        Assert.Equal(2, regex.Count(input));
        var actual = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)).TupleValues;
        Assert.Equal(new[] { "a", "é", "😀" }, actual[0]);
        Assert.Equal(new[] { "b", "é", "😀" }, actual[1]);
        Assert.Equal(2, actual.Length);
        var cursor = regex.CreateMatchCursor(input);
        try
        {
            Assert.True(cursor.MoveNext(out cursorRange));
            Assert.Equal((3, 7), (cursorRange.Groups[3].StartOffsetInBytes, cursorRange.Groups[3].EndOffsetInBytes));
            Assert.True(cursor.MoveNext(out cursorRange));
            Assert.False(cursor.MoveNext(out cursorRange));
        }
        finally { cursor.Dispose(); }
    }
}
