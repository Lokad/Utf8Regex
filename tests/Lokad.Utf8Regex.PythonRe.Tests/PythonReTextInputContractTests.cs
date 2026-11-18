using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;

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
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
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
        finally
        {
            session.Complete();
        }
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
        using var compiled = Utf8PythonRegex.Compile(Encoding.UTF8.GetBytes(pattern), options: default, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default);
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.True(regex.IsMatch(input));
            Assert.Equal(count, regex.Count(input));
            Assert.Equal(count, compiled.Count(input));
            var first = regex.Match(input, mode: Utf8PythonMatchMode.Search);
            Assert.Equal(firstByte, first.StartOffsetInBytes);
            Assert.Equal(firstByte + firstLength, first.EndOffsetInBytes);
            _ = regex.Match(input);
            _ = regex.Match(input, mode: Utf8PythonMatchMode.FullMatch);
            ReadOnlyMemory<byte> snapshots1Input = input;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            _ = snapshots1;
            ReadOnlyMemory<byte> snapshots2Input = input;
            var snapshots2 = new List<Utf8PythonRangeMatch>();
            using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
            {
                while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                    snapshots2.Add(snapshots2Match);
            }
            _ = snapshots2;
            _ = PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, 0, default));
            _ = regex.Replace(input, "X");
            using var cursor = regex.CreateMatchCursor(input);
            var observed = 0;
            while (cursor.MoveNext(out _))
                observed++;
            Assert.Equal(count, observed);
        }
        finally
        {
            session.Complete();
        }
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
