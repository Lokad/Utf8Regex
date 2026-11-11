using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReAsciiEscapeLoweringTests
{
    [Theory]
    [InlineData('w')] [InlineData('d')] [InlineData('s')] [InlineData('u')]
    public void AsciiBracketUnionsKeepResolvedOperandsAndScalarComplements(char kind)
    {
        var body = kind switch { 'w' => @"\w.-", 'd' => @"\dA-F", 's' => @"\s\x1c-", _ => @"\w\d\s-" };
        foreach (var policy in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        foreach (var negate in new[] { false, true })
        foreach (var bytes in new[] { false, true })
        {
            var pattern = "(?ai:(?P<x>[" + (negate ? "^" : "") + body + "]))";
            using var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), inputValidation: policy)
                : new Utf8PythonRegex(pattern, inputValidation: policy);
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
                var actual = regex.FullMatchDetailedData(input);
                Assert.Equal(negate ? !positive : positive, actual.Success);
                if (!actual.Success) continue;
                Assert.Equal(text, actual.Groups[1].ValueText);
                Assert.Equal((0, input.Length), (actual.Groups[1].StartOffsetInBytes, actual.Groups[1].EndOffsetInBytes));
                Assert.Equal((0, text.Length), (actual.Groups[1].StartOffsetInUtf16, actual.Groups[1].EndOffsetInUtf16));
            }
        }
    }

    [Theory]
    [InlineData('w')] [InlineData('W')] [InlineData('d')]
    [InlineData('D')] [InlineData('s')] [InlineData('S')]
    public void AsciiEscapesKeepTheirScalarComplementAndCaptureCoordinates(char escape)
    {
        foreach (var policy in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        foreach (var bytes in new[] { false, true })
        {
            var pattern = @"(?ai:(?P<x>\" + escape + "))";
            using var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), inputValidation: policy)
                : new Utf8PythonRegex(pattern, inputValidation: policy);
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
                var actual = regex.FullMatchDetailedData(input);
                Assert.Equal(expected, actual.Success);
                if (!expected) continue;
                Assert.Equal(text, actual.Groups[1].ValueText);
                Assert.Equal((0, input.Length), (actual.Groups[1].StartOffsetInBytes, actual.Groups[1].EndOffsetInBytes));
                Assert.Equal((0, text.Length), (actual.Groups[1].StartOffsetInUtf16, actual.Groups[1].EndOffsetInUtf16));
            }
            Assert.False(regex.FullMatch(ReadOnlySpan<byte>.Empty).Success);
        }
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate, false)] [InlineData(Utf8InputValidation.AssumeValid, false)]
    [InlineData(Utf8InputValidation.Validate, true)] [InlineData(Utf8InputValidation.AssumeValid, true)]
    public void ScopedModesRestoreUnicodePredicatesAndKeepGlobalResults(Utf8InputValidation policy, bool union)
    {
        using var regex = new Utf8PythonRegex(union ? @"(?ai:([\w-]))(?u:([\w-]))(?a:([^\w-]))"
            : @"(?ai:(\w))(?u:(\w))(?a:(\W))", inputValidation: policy);
        var input = "aé😀 bé😀"u8.ToArray();
        Assert.Equal(2, regex.Count(input));
        var actual = regex.FindAllToStrings(input).TupleValues;
        Assert.Equal(new[] { "a", "é", "😀" }, actual[0]);
        Assert.Equal(new[] { "b", "é", "😀" }, actual[1]);
        Assert.Equal(2, actual.Length);
        var cursor = regex.CreateMatchCursor(input);
        try
        {
            Assert.True(cursor.MoveNext());
            Assert.Equal((3, 7), (cursor.Current.Groups[3].StartOffsetInBytes, cursor.Current.Groups[3].EndOffsetInBytes));
            Assert.True(cursor.MoveNext());
            Assert.False(cursor.MoveNext());
        }
        finally { cursor.Dispose(); }
    }
}
