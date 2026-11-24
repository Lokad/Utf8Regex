using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTextInputContractTests
{
    [Theory]
    [InlineData("é")]
    [InlineData("😀")]
    [InlineData("夏洛克·福尔摩斯")]
    [InlineData("abcé")]
    [InlineData("needle")]
    public void DelegatedLiteralCountKeepsTheAlreadyValidTextContract(string literal)
    {
        using var regex = new Utf8PythonRegex(literal);
        using var compiled = Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(literal), options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default);
        foreach (var length in new[] { 0, 63, 4096 })
        {
            foreach (var withMatches in new[] { false, true })
            {
                var text = new string('a', length) + (withMatches ? literal + " " + literal : "");
                var bytes = Encoding.UTF8.GetBytes(text);
                var storage = new byte[bytes.Length + 13];
                Array.Fill(storage, (byte)0xff);
                bytes.CopyTo(storage.AsSpan(5));
                var expected = withMatches ? 2 : 0;
                Assert.Equal(expected, regex.Count(storage.AsSpan(5, bytes.Length)));
                Assert.Equal(expected, compiled.Count(storage.AsSpan(5, bytes.Length)));
            }
        }
    }

    [Theory]
    [InlineData("(needle)", "é😀 needle!", 1, 7, 6, false, "é😀 X!", new[] { "é😀 ", "needle", "!" })]
    [InlineData("(é|😀)", "é😀", 2, 0, 2, false, "XX", new[] { "", "é", "", "😀", "" })]
    [InlineData("", "é😀", 3, 0, 0, false, "XéX😀X", new[] { "", "é", "😀", "" })]
    [InlineData("(é)", "é", 1, 0, 2, true, "X", new[] { "", "é", "" })]
    public void TextOperationsAndCursorsPreserveScalarByteCoordinates(string pattern, string text,
        int count, int firstByte, int firstLength, bool fullMatch, string replacement, string[] split)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8PythonRegex(pattern);
        using var compiled = Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(pattern), options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default);

        Assert.True(regex.IsMatch(input));
        Assert.Equal(count, regex.Count(input));
        Assert.Equal(count, compiled.Count(input));
        var first = regex.Match(input, mode: Utf8PythonMatchMode.Search);
        Assert.True(first.Success);
        Assert.Equal((firstByte, firstByte + firstLength), (first.StartOffsetInBytes, first.EndOffsetInBytes));
        Assert.Equal(firstByte == 0, regex.Match(input).Success);
        Assert.Equal(fullMatch, regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(split, PythonReSplitAssert.Decode(input, regex.SplitRanges(input)));
        Assert.Equal(replacement, Encoding.UTF8.GetString(regex.Replace(input, "X"u8)));

        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var cursorFirst));
        Assert.Equal((firstByte, firstByte + firstLength),
            (cursorFirst.Value.StartOffsetInBytes, cursorFirst.Value.EndOffsetInBytes));
        var observed = 1;
        while (cursor.MoveNext(out _))
            observed++;
        Assert.Equal(count, observed);
    }

    [Fact]
    public void EarlyRangeDoesNotPrepareUnrequestedWholeTextFacts()
    {
        var input = Encoding.UTF8.GetBytes("needle " + new string('é', 65536));
        using var regex = new Utf8PythonRegex("(needle)", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 64,
                MaxWorkingBytes = 1 << 20
            }
        });
        Assert.Equal(6, regex.Match(input).EndOffsetInBytes);
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var first));
        Assert.Equal(6, first.GetValueMemory().Length);
    }

    [Fact]
    public void PatternReplacementAndOriginalCancellationRemainIndependent()
    {
        Assert.Throws<ArgumentException>(() => new Utf8PythonRegex(new byte[] { 0xff }));
        using var regex = new Utf8PythonRegex("needle");
        Assert.Throws<ArgumentException>(() => regex.Subn("needle"u8, new byte[] { 0xff }));
        using var source = new CancellationTokenSource();
        source.Cancel();
        var error = Assert.ThrowsAny<OperationCanceledException>(() => regex.IsMatch("needle"u8, 0,
            cancellationToken: source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
    }
}
