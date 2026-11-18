using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SharedNameMetadataTests
{
    [Theory]
    [InlineData(@"(?<x>a)(?=(?<y>b))(?<z>b)\k<x>", "aba", 3)]
    [InlineData(@"(?J)(?<x>a)(?=(?=(?<x>b)))b\k<x>", "aba", 2)]
    [InlineData(@"(?(?=(?<x>a))\k<x>|b)", "a", 1)]
    [InlineData(@"(?<x>a)(?=(?&x))(?&x)", "aa", 1)]
    public void AssertionsConditionsAndSubroutinesPreserveNamesAndPublicCopyIsolation(
        string pattern, string subject, int expectedNames)
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
            Assert.Equal(expectedNames, retained.NameEntries.Length);
            Assert.True(retained.TryGetFirstSetGroup("x", out var first));
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString((first.Success ? retained1Input.Span.Slice(first.StartOffsetInBytes, first.EndOffsetInBytes - first.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
            Assert.Equal(expectedNames, regex.NameEntryCount);
            for (var index = 0; index < expectedNames; index++)
            {
                var expectedName = Encoding.UTF8.GetBytes(retained.NameEntries[index].Name);
                var nameBytes = new byte[expectedName.Length];
                Assert.Equal(System.Buffers.OperationStatus.Done,
                    regex.TryCopyNameEntryUtf8(index, nameBytes, out var bytesRequired, out var groupNumber));
                Assert.Equal(expectedName, nameBytes);
                Assert.Equal(expectedName.Length, bytesRequired);
                Assert.Equal(retained.NameEntries[index].Number, groupNumber);
                nameBytes.AsSpan().Fill(0xff);
            }
            Assert.Equal(1, regex.Count(input));
            Assert.Equal("<a>", Encoding.UTF8.GetString(regex.Replace(input, "<$x>")));
            Assert.True(retained.TryGetFirstSetGroup("x", out first));
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString((first.Success ? retained1Input.Span.Slice(first.StartOffsetInBytes, first.EndOffsetInBytes - first.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        }
    }
}
