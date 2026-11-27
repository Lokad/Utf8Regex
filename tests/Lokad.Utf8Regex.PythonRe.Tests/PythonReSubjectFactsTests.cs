using System.Text;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSubjectFactsTests
{
    [Fact]
    public void GovernedUnicodeStartDoesNotClassifyAnUnusedAsciiSuffix()
    {
        var input = Encoding.UTF8.GetBytes("é" + new string('x', 1 << 20));
        using var regex = new Utf8PythonRegex("([a-zé])", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 32768,
                MaxResultBytes = 512
            }
        });
        var value = regex.Match(input, mode: Utf8PythonMatchMode.Search);
        Assert.True(value.Success);
        Assert.Equal((0, 2), (value.StartOffsetInBytes, value.EndOffsetInBytes));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.Match(input, 0, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.Search));
        Assert.Equal(cancellation.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData("x", "needle")]
    [InlineData("é", "é😀")]
    [InlineData("😀", "needle")]
    public void ByteValuesKeepTheReturnedTrustedRange(string suffixScalar, string value)
    {
        var input = Encoding.UTF8.GetBytes("é😀" + value + string.Concat(Enumerable.Repeat(suffixScalar, 1 << 20)));
        using var regex = new Utf8PythonRegex($"({value})", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 32768,
                MaxResultBytes = 128
            }
        });
        using var cancellation = new CancellationTokenSource();
        PythonReMatchAssert.Equal(value, regex.Match(input, 6, cancellationToken: cancellation.Token));
        PythonReMatchAssert.Equal(value, regex.Match(input, 6, cancellationToken: cancellation.Token, mode: Utf8PythonMatchMode.Search));

        cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.Match(input, 6, cancellationToken: cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }

    [Theory]
    [InlineData("needle", 'x')]
    [InlineData("é😀", 'é')]
    public void AnchoredCapturedStringsSizeOnlyTheirPublishedValue(string value, char suffix)
    {
        var input = Encoding.UTF8.GetBytes(value + new string(suffix, 1 << 20));
        using var regex = new Utf8PythonRegex($@"\A({value})", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 32768,
                MaxResultBytes = 512
            }
        });
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(new[] { value }, snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
        {
            ReadOnlyMemory<byte> snapshots2Input = input;
            var snapshots2 = new List<Utf8PythonRangeMatch>();
            using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, cancellation.Token))
            {
                while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                    snapshots2.Add(snapshots2Match);
            }
            return snapshots2;
        });
        Assert.Equal(cancellation.Token, error.CancellationToken);
    }
    [Fact]

    public void ByteValuesKeepLargeScalarRangesWithoutTextAdmission()
    {
        var expected = new string('a', 4093) + "é😀";
        var input = Encoding.UTF8.GetBytes("é😀" + expected);
        using var regex = new Utf8PythonRegex("(?s)(.+)", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 32768,
                MaxResultBytes = 1
            }
        });
        var retained = regex.Match(input, 6);
        PythonReMatchAssert.Equal(expected, retained);
        PythonReMatchAssert.Equal(expected, regex.Match(input, 6, mode: Utf8PythonMatchMode.Search));
        PythonReMatchAssert.Equal(expected, regex.Match(input, 6, mode: Utf8PythonMatchMode.FullMatch));
        PythonReMatchAssert.Equal(expected, regex.Match(Encoding.UTF8.GetBytes(expected), mode: Utf8PythonMatchMode.FullMatch));
        PythonReMatchAssert.Equal(expected, retained);
    }
    [Fact]

    public void ByteValuesReturnProgramFundingAndSurviveClosure()
    {
        var allowance = new Allowance(16 << 20);
        Assert.True(allowance.TryReserve(17));
        using var regex = Utf8PythonRegex.Compile("(é😀)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default,
            ConstructionPolicy = default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan,
            ProgramMemoryAllowance = allowance
        }, cancellationToken: default);
        var held = allowance.Used;
        var retained = regex.Match("é😀suffix"u8);
        PythonReMatchAssert.Equal("é😀", retained);
        Assert.Equal(held, allowance.Used);
        regex.Dispose();
        Assert.Equal(17UL, allowance.Used);
        PythonReMatchAssert.Equal("é😀", retained);
        allowance.Release(17);
    }
    [Fact]

    public void ValuesAtSubjectEndsKeepByteCoordinates()
    {
        var input = "é😀 item-1234"u8.ToArray();
        var regex = new Utf8PythonRegex("(item)-([0-9]+)");
        var value = regex.Match(input, mode: Utf8PythonMatchMode.Search);
        Assert.True(value.Success);
        Assert.Equal((7, 16), (value.StartOffsetInBytes, value.EndOffsetInBytes));
        Assert.False(regex.Match(input).Success);
        Assert.True(regex.Match(input, 7, mode: Utf8PythonMatchMode.FullMatch).Success);
        ReadOnlyMemory<byte> snapshots3Input = input;
        var snapshots3 = new List<Utf8PythonRangeMatch>();
        using (var snapshots3Cursor = regex.CreateMatchCursor(snapshots3Input, 0, default))
        {
            while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                snapshots3.Add(snapshots3Match);
        }
        Assert.Equal(new[] { "item", "1234" }, snapshots3.Select(captureSnapshot2 => Enumerable.Range(1, captureSnapshot2.Groups.Length - 1).Select(captureNumber2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureNumber2).Span)).ToArray()).ToArray()[0]);
    }
    [Theory]
    [InlineData(false, 64)]
    [InlineData(false, 8192)]
    [InlineData(true, 64)]
    [InlineData(true, 8192)]
    public void RangeAliasesKeepRetainedByteCapturesAndRefund(bool unicode, int suffixLength)
    {
        var prefix = (unicode ? "é😀 " : "abc ") + new string(unicode ? 'é' : 'a', suffixLength);
        var input = Encoding.UTF8.GetBytes(prefix + "item-1234 " + new string(unicode ? 'é' : 'x', suffixLength));
        var account = new Allowance(1 << 20);
        var regex = new Utf8PythonRegex("(item)-([0-9]+)");
        using var cursor = regex.CreateMatchCursor(input, 0, default, account);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out var retained));
        var beforeQueries = account.Used;
        var start = Encoding.UTF8.GetByteCount(prefix);
        Assert.True(retained.TryGetGroup(1, out var first));
        Assert.Equal((start, start + 4), (first.StartOffsetInBytes, first.EndOffsetInBytes));
        for (var repeat = 0; repeat < 8; repeat++)
        {
            Assert.True(retained.TryGetGroup(2, out var number));
            Assert.Equal((start + 5, start + 9), (number.StartOffsetInBytes, number.EndOffsetInBytes));
            Assert.Equal(beforeQueries, account.Used);
        }
        Assert.False(alias.MoveNext(out _));
        Assert.Equal(0UL, account.Used);
        Assert.Equal("item-1234", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }
}
