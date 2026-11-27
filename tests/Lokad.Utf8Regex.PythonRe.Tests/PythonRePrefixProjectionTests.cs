using System.Text;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonRePrefixProjectionTests
{
    [Theory]
    [InlineData("(a)(b)", "ab", 0, 2)]
    [InlineData("a(?=(😀))", "a😀", 0, 1)]
    [InlineData("(?<=(é))(😀)", "é😀", 2, 6)]
    public void EarlyDetailedCursorAndCollectionIgnoreUnusedSuffix(string pattern, string prefix, int start, int endBytes)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = Encoding.UTF8.GetBytes(prefix + new string('界', 200000));
        using var regex = new Utf8PythonRegex(pattern, policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxDepth = 4096,
                MaxWorkingBytes = 4096,
                MaxResultBytes = 4096
            }
        });
        var account = new Allowance(4096); Assert.True(account.TryReserve(17));
        using var cursor = regex.CreateMatchCursor(input, start, default, account);
        Assert.True(cursor.MoveNext(out cursorRange)); var retained = cursorRange;
        Assert.Equal((start, endBytes), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(input, start, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var collection = Assert.Single(collectedRanges1.ToArray());
        Utf8PythonRangeMatch oneShot = default;
        using (var captureCursor1 = regex.CreateMatchCursor(input, start, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out oneShot);
        }
        PythonReCaptureAssert.Equal(oneShot, retained); PythonReCaptureAssert.Equal(oneShot, collection);
        cursor.Dispose(); Assert.Equal(17UL, account.Used); account.Release(17);
        PythonReCaptureAssert.Equal(oneShot, retained);
    }

    [Fact]
    public void MixedRangeAndDetailedAdvancesKeepPrefixIndependentResultTraffic()
    {
        Utf8PythonRangeMatch cursorRange = default;
        ulong? traffic = null;
        using var regex = new Utf8PythonRegex("(a)", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 65536,
                MaxResultBytes = 2048
            }
        });
        foreach (var prefix in new[] { "", new string('é', 4096) + "😀" })
        {
            var account = new Allowance(65536);
            using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(prefix + "aa"), 0, default, account);
            Assert.True(cursor.MoveNext(out var first));
            Assert.True(cursor.MoveNext(out cursorRange)); var detail = cursorRange;
            var current = cursor.WorkingInvocation.Budget.ResultBytes;
            if (traffic is { } previous) Assert.Equal(previous, current);
            traffic = current;
            var start = Encoding.UTF8.GetByteCount(prefix) + 1;
            Assert.Equal((start, start + 1), (detail.Groups[1].StartOffsetInBytes, detail.Groups[1].EndOffsetInBytes));
            cursor.Dispose(); Assert.Equal(0UL, account.Used);
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString(detail.GetGroupMemory(1).Span));
            Assert.Equal("a", Encoding.UTF8.GetString(first.GetValueMemory().Span));
        }
    }

    [Theory]
    [InlineData(253)]
    [InlineData(255)]
    [InlineData(256)]
    public void LookaroundCaptureBytesCrossFormerIndexCuts(int prefixLength)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var text = new string('x', prefixLength) + "éa😀" + new string('界', 10000);
        using var cursor = new Utf8PythonRegex("(?<=(é))a(?=(😀))", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 4096,
                MaxResultBytes = 4096
            }
        }).CreateMatchCursor(Encoding.UTF8.GetBytes(text));
        Assert.True(cursor.MoveNext(out cursorRange)); var result = cursorRange;
        Assert.Equal((prefixLength, prefixLength + 2), (result.Groups[1].StartOffsetInBytes, result.Groups[1].EndOffsetInBytes));
        Assert.Equal((prefixLength + 3, prefixLength + 7), (result.Groups[2].StartOffsetInBytes, result.Groups[2].EndOffsetInBytes));
        cursor.Dispose(); Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(result.GetGroupMemory(2).Span));
    }
}
