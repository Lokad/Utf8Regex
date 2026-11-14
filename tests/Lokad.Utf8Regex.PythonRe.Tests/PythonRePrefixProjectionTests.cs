using System.Text;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonRePrefixProjectionTests
{
    [Theory]
    [InlineData("(a)(b)", "ab", 0, 2, 2)]
    [InlineData("a(?=(😀))", "a😀", 0, 1, 1)]
    [InlineData("(?<=(é))(😀)", "é😀", 2, 6, 3)]
    public void EarlyDetailedCursorAndCollectionIgnoreUnusedSuffix(string pattern, string prefix,
        int start, int endBytes, int endUtf16)
    {
        var input = Encoding.UTF8.GetBytes(prefix + new string('界', 200000));
        using var regex = new Utf8PythonRegex(pattern, resourcePolicy: new()
        {
            MaxWorkSteps = 10000000, MaxDepth = 4096, MaxWorkingBytes = 4096, MaxResultBytes = 4096
        });
        var account = new Allowance(4096); Assert.True(account.TryReserve(17));
        using var cursor = regex.CreateMatchCursor(input, start, default, account);
        Assert.True(cursor.MoveNext()); var retained = cursor.Current;
        Assert.Equal((start, endBytes, endUtf16),
            (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes, retained.Value.EndOffsetInUtf16));
        Assert.True(cursor.TryGetScalarRange(0, out _, out _));
        var collection = Assert.Single(regex.FindIterDetailed(input, start));
        var oneShot = regex.SearchDetailedData(input, start);
        Assert.Equal(oneShot.Groups, retained.Groups); Assert.Equal(oneShot.Groups, collection.Groups);
        cursor.Dispose(); Assert.Equal(17UL, account.Used); account.Release(17);
        Assert.Equal(oneShot.Groups, retained.Groups);
    }

    [Fact]
    public void DetailedIndexDoesNotConsumeResultTrafficAndRangesCanPrecedeDetails()
    {
        ulong? traffic = null;
        using var regex = new Utf8PythonRegex("(a)", resourcePolicy: new()
        {
            MaxWorkingBytes = 65536, MaxResultBytes = 2048
        });
        foreach (var prefix in new[] { "", new string('é', 4096) + "😀" })
        {
            var account = new Allowance(65536);
            using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(prefix + "aa"), 0, default, account);
            Assert.True(cursor.MoveNext(out var first));
            Assert.True(cursor.MoveNext()); var detail = cursor.Current;
            Assert.Equal(prefix.Length + 1, detail.Value.StartOffsetInUtf16);
            Assert.Equal(prefix.Length + 2, detail.Value.EndOffsetInUtf16);
            var current = cursor.WorkingInvocation.Budget.ResultBytes;
            if (traffic is { } previous) Assert.Equal(previous, current);
            traffic = current;
            Assert.True(cursor.TryGetScalarRange(1, out var start, out var end));
            Assert.Equal(prefix.EnumerateRunes().Count() + 1, start); Assert.Equal(start + 1, end);
            cursor.Dispose(); Assert.Equal(0UL, account.Used);
            Assert.Equal("a", detail.Groups[1].ValueText);
            Assert.Equal("a", Encoding.UTF8.GetString(first.GetValueMemory().Span));
        }
    }

    [Theory]
    [InlineData(253)] [InlineData(255)] [InlineData(256)]
    public void LookaroundEdgesAcrossAnIndexCutUseTheFurthestCapture(int prefixLength)
    {
        var text = new string('x', prefixLength) + "éa😀" + new string('界', 10000);
        using var cursor = new Utf8PythonRegex("(?<=(é))a(?=(😀))", resourcePolicy: new()
            { MaxWorkingBytes = 4096, MaxResultBytes = 4096 }).CreateMatchCursor(Encoding.UTF8.GetBytes(text));
        Assert.True(cursor.MoveNext()); var result = cursor.Current;
        Assert.Equal((prefixLength, prefixLength + 2, prefixLength, prefixLength + 1),
            (result.Groups[1].StartOffsetInBytes, result.Groups[1].EndOffsetInBytes,
             result.Groups[1].StartOffsetInUtf16, result.Groups[1].EndOffsetInUtf16));
        Assert.Equal((prefixLength + 3, prefixLength + 7, prefixLength + 2, prefixLength + 4),
            (result.Groups[2].StartOffsetInBytes, result.Groups[2].EndOffsetInBytes,
             result.Groups[2].StartOffsetInUtf16, result.Groups[2].EndOffsetInUtf16));
        Assert.True(cursor.TryGetScalarRange(2, out var start, out var end));
        Assert.Equal((prefixLength + 2, prefixLength + 3), (start, end));
        cursor.Dispose(); Assert.Equal("😀", result.Groups[2].ValueText);
    }
}
