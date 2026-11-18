using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureByteOutputTests
{
    private static Utf8ResourcePolicy Funded => new()
    {
        MaxWorkSteps = 10_000_000,
        MaxDepth = 100_000,
        MaxWorkingBytes = 16 * 1024 * 1024,
        MaxResultBytes = 16 * 1024 * 1024
    };

    private static Utf8PythonRegex Create(string pattern, Utf8ResourcePolicy policy)
        => new(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void AsciiCapturesAcrossBlockEdgesRetainOwnedValues(int length)
    {
        var first = new string('a', length);
        {
            using var regex = Create("(a+)(b)", Funded);
            ReadOnlyMemory<byte> snapshots1Input = Encoding.UTF8.GetBytes(first + "b");
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            var result = snapshots1;
            var tuple = Assert.Single(result.Select(captureSnapshot1 => Enumerable.Range(1, captureSnapshot1.Groups.Length - 1).Select(captureNumber1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureNumber1).Span)).ToArray()).ToArray());
            Assert.Equal(new[] { first, "b" }, tuple);
            ReadOnlyMemory<byte> snapshots2Input = ("aab"u8).ToArray();
            var snapshots2 = new List<Utf8PythonRangeMatch>();
            using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
            {
                while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                    snapshots2.Add(snapshots2Match);
            }
            Assert.Equal(new[] { "aa", "b" }, Assert.Single(snapshots2.Select(captureSnapshot2 => Enumerable.Range(1, captureSnapshot2.Groups.Length - 1).Select(captureNumber2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureNumber2).Span)).ToArray()).ToArray()));
            Assert.Equal(first, tuple[0]);
        }
    }
    [Theory]
    [InlineData("(a+)(b)", "😀aaabé", "aaa", "b")]
    [InlineData("(😀+)(é+)", "x😀😀ééx", "😀😀", "éé")]
    [InlineData("(a+)(b)", "aaab\0", "aaa", "b")]
    public void ValidTextPreservesAsciiAndUnicodeCaptureLengths(
        string pattern, string input, string first, string second)
    {
        {
            using var regex = Create(pattern, Funded);
            ReadOnlyMemory<byte> snapshots3Input = Encoding.UTF8.GetBytes(input);
            var snapshots3 = new List<Utf8PythonRangeMatch>();
            using (var snapshots3Cursor = regex.CreateMatchCursor(snapshots3Input, 0, default))
            {
                while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                    snapshots3.Add(snapshots3Match);
            }
            var result = snapshots3;
            Assert.Equal(new[] { first, second }, Assert.Single(result.Select(captureSnapshot3 => Enumerable.Range(1, captureSnapshot3.Groups.Length - 1).Select(captureNumber3 => System.Text.Encoding.UTF8.GetString(captureSnapshot3.GetGroupMemory(captureNumber3).Span)).ToArray()).ToArray()));
        }
    }
    [Fact]
    public void EmptyCapturesStillProduceOneOwnedTuple()
    {
        var snapshots4Regex = Create("(a*)(b?)", Funded);
        ReadOnlyMemory<byte> snapshots4Input = ReadOnlyMemory<byte>.Empty;
        var snapshots4 = new List<Utf8PythonRangeMatch>();
        using (var snapshots4Cursor = snapshots4Regex.CreateMatchCursor(snapshots4Input, 0, default))
        {
            while (snapshots4Cursor.MoveNext(out var snapshots4Match))
                snapshots4.Add(snapshots4Match);
        }
        var result = snapshots4;
        Assert.Equal(new[] { string.Empty, string.Empty }, Assert.Single(result.Select(captureSnapshot4 => Enumerable.Range(1, captureSnapshot4.Groups.Length - 1).Select(captureNumber4 => System.Text.Encoding.UTF8.GetString(captureSnapshot4.GetGroupMemory(captureNumber4).Span)).ToArray()).ToArray()));
    }
    [Fact]
    public void PreCancellationRetainsItsOriginalToken()
    {
        var regex = Create("(a)(b)", Funded);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() =>
        {
            ReadOnlyMemory<byte> snapshots5Input = ("ab"u8).ToArray();
            var snapshots5 = new List<Utf8PythonRangeMatch>();
            using (var snapshots5Cursor = regex.CreateMatchCursor(snapshots5Input, 0, cancellation.Token))
            {
                while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                    snapshots5.Add(snapshots5Match);
            }
            return snapshots5;
        });
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }
}
