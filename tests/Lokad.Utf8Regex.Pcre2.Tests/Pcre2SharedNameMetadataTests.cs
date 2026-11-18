using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SharedNameMetadataTests
{
    [Theory]
    [InlineData(@"(?<x>a)(?=(?<y>b))(?<z>b)\k<x>", "aba", "xyz")]
    [InlineData(@"(?J)(?<x>a)(?=(?=(?<x>b)))b\k<x>", "aba", "xx")]
    [InlineData(@"(?(?=(?<x>a))\k<x>|b)", "a", "x")]
    [InlineData(@"(?<x>a)(?=(?&x))(?&x)", "aa", "x")]
    public void AssertionsConditionsAndSubroutinesPreserveNamesAndPublicCopyIsolation(
        string pattern, string subject, string expectedNames)
    {
        var input = Encoding.UTF8.GetBytes(subject);
        foreach (var regex in new[] { new Utf8Pcre2Regex(pattern), new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern)) })
        {
            ReadOnlyMemory<byte> retained1Input = input;
            using var retained1Cursor = regex.CreateMatchCursor(retained1Input);
            retained1Cursor.MoveNext(out var retained1Result);
            retained1Cursor.Dispose();
            var retained = retained1Result;
            Assert.True(retained.Success);
            Assert.True(retained.TryGetGroup(1, out var first)); Assert.True(first.Success);
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString((first.Success ? retained1Input.Span.Slice(first.StartOffsetInBytes, first.EndOffsetInBytes - first.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
            Assert.Equal(expectedNames.Length, regex.NameEntryCount);
            for (var index = 0; index < expectedNames.Length; index++)
            {
                var expectedName = Encoding.UTF8.GetBytes(expectedNames[index].ToString());
                var nameBytes = new byte[expectedName.Length];
                Assert.Equal(System.Buffers.OperationStatus.Done,
                    regex.TryCopyNameEntryUtf8(index, nameBytes, out var bytesRequired, out var groupNumber));
                Assert.Equal(expectedName, nameBytes);
                Assert.Equal(expectedName.Length, bytesRequired);
                Assert.Equal(index + 1, groupNumber);
                nameBytes.AsSpan().Fill(0xff);
            }
            Assert.Equal(1, regex.Count(input));
            Assert.Equal("<a>", Encoding.UTF8.GetString(regex.Replace(input, "<$x>"u8)));
            Assert.True(retained.TryGetGroup(1, out var after));
            Assert.Equal(first, after);
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString((first.Success ? retained1Input.Span.Slice(first.StartOffsetInBytes, first.EndOffsetInBytes - first.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        }
    }
}
