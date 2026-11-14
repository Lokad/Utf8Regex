using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReTextInputContractTests
{
    [Theory]
    [InlineData("é")] [InlineData("😀")] [InlineData("夏洛克·福尔摩斯")]
    [InlineData("abcé")] [InlineData("needle")]
    public void DelegatedLiteralCountKeepsTheAlreadyValidTextContract(string literal)
    {
        using var regex = new Utf8PythonRegex(literal);
        using var compiled = Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(literal), default,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default);
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            foreach (var length in new[] { 0, 63, 4096 })
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
            Assert.Equal(0, session.SubjectValidations);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData("(needle)", "é😀 needle!", 1, 7, 6)]
    [InlineData("(é|😀)", "é😀", 2, 0, 2)]
    [InlineData("", "é😀", 3, 0, 0)]
    public void TextOperationsAndCursorsDoNotValidateSubjects(string pattern, string text,
        int count, int firstByte, int firstLength)
    {
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8PythonRegex(pattern);
        using var compiled = Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(pattern), default,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default);
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.True(regex.IsMatch(input));
            Assert.Equal(count, regex.Count(input));
            Assert.Equal(count, compiled.Count(input));
            var first = regex.Search(input);
            Assert.Equal(firstByte, first.StartOffsetInBytes);
            Assert.Equal(firstByte + firstLength, first.EndOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetCharCount(input.AsSpan(0, firstByte)), first.StartOffsetInUtf16);
            _ = regex.Match(input);
            _ = regex.FullMatch(input);
            _ = regex.FindAllToStrings(input);
            _ = regex.FindAllToUtf8(input);
            _ = regex.SplitToStrings(input);
            _ = regex.Replace(input, "X");
            using var cursor = regex.CreateMatchCursor(input);
            var observed = 0;
            while (cursor.MoveNext(out _)) observed++;
            Assert.Equal(count, observed);
            Assert.Equal(0, session.SubjectValidations);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void EarlyRangeDoesNotPrepareUnrequestedWholeTextFacts()
    {
        var input = Encoding.UTF8.GetBytes("needle " + new string('é', 65536));
        using var regex = new Utf8PythonRegex("(needle)", resourcePolicy: new()
        { MaxWorkSteps = 64, MaxWorkingBytes = 1 << 20 });
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
