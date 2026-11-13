using System.Text;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSubjectFactsTests
{
    [Theory]
    [InlineData("x", "needle")]
    [InlineData("é", "é😀")]
    [InlineData("😀", "needle")]
    public void OneShotTextSizesOnlyTheReturnedTrustedRange(string suffixScalar, string value)
    {
        var input = Encoding.UTF8.GetBytes("é😀" + value + string.Concat(Enumerable.Repeat(suffixScalar, 1 << 20)));
        using var regex = new Utf8PythonRegex($"({value})",
            resourcePolicy: new Utf8ResourcePolicy
            {
                MaxWorkSteps = 128, MaxWorkingBytes = 32768, MaxResultBytes = 128,
            });
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(value, regex.MatchToString(input, 6, cancellation.Token));
        Assert.Equal(value, regex.SearchToString(input, 6, cancellation.Token));

        cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.MatchToString(input, 6, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }

    [Theory]
    [InlineData("needle", 'x')]
    [InlineData("é😀", 'é')]
    public void AnchoredCapturedStringsSizeOnlyTheirPublishedValue(string value, char suffix)
    {
        var input = Encoding.UTF8.GetBytes(value + new string(suffix, 1 << 20));
        using var regex = new Utf8PythonRegex($@"\A({value})",
            resourcePolicy: new() { MaxWorkSteps = 128, MaxWorkingBytes = 32768, MaxResultBytes = 512 });
        Assert.Equal(new[] { value }, regex.FindAllToStrings(input).ScalarValues);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.FindAllToStrings(input, 0, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
    }
    [Fact]

    public void OneShotTextAdmitsTheActualStringBeforeAllocation()
    {
        const string text = "é😀";
        var required = Utf8ConstructionBudget.StringBytes(text.Length);
        using var regex = new Utf8PythonRegex($"({text})",
            resourcePolicy: new Utf8ResourcePolicy { MaxWorkingBytes = 32768, MaxResultBytes = required - 1 });
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.MatchToString("é😀tail"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, failure.ResourceKind);
        Assert.Equal(required - 1, failure.Limit);
        Assert.Equal(required, failure.Requested);
    }
    [Fact]

    public void OneShotTextKeepsLargeScalarRangesAndExactAdmission()
    {
        var expected = new string('a', 4093) + "é😀";
        var input = Encoding.UTF8.GetBytes("é😀" + expected);
        using var regex = new Utf8PythonRegex("(?s)(.+)",
            resourcePolicy: new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 32768, MaxResultBytes = Utf8ConstructionBudget.StringBytes(expected.Length),
            });
        var retained = regex.MatchToString(input, 6);
        Assert.Equal(expected, retained);
        Assert.Equal(expected, regex.SearchToString(input, 6));
        Assert.Equal(expected, regex.FullMatchToString(input, 6));
        Assert.Equal(expected, regex.FullMatchToString(Encoding.UTF8.GetBytes(expected)));
        Assert.Equal(expected, retained);
    }
    [Fact]

    public void OneShotOwnedTextReturnsProgramFundingAndSurvivesClosure()
    {
        var allowance = new Allowance(16 << 20);
        Assert.True(allowance.TryReserve(17));
        using var regex = Utf8PythonRegex.Compile("(é😀)", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default,
            allowance);
        var held = allowance.Used;
        var retained = regex.MatchToString("é😀suffix"u8);
        Assert.Equal("é😀", retained);
        Assert.Equal(held, allowance.Used);
        regex.Dispose();
        Assert.Equal(17UL, allowance.Used);
        Assert.Equal("é😀", retained);
        allowance.Release(17);
    }
    [Fact]

    public void ValuesAtSubjectEndsKeepRealUtf16Coordinates()
    {
        var input = "é😀 item-1234"u8.ToArray();
        var regex = new Utf8PythonRegex("(item)-([0-9]+)");
        var value = regex.Search(input);
        Assert.True(value.Success);
        Assert.Equal((7, 16, 4, 13), (value.StartOffsetInBytes, value.EndOffsetInBytes,
            value.StartOffsetInUtf16, value.EndOffsetInUtf16));
        Assert.False(regex.Match(input).Success);
        Assert.True(regex.FullMatch(input, 7).Success);
        Assert.Equal(new[] { "item", "1234" }, regex.FindAllToStrings(input).TupleValues[0]);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RangeAliasesCanRequestCoordinatesLaterAndRefund( bool unicode)
    {
        var prefix = unicode ? "é😀 " : "abc ";
        var input = Encoding.UTF8.GetBytes(prefix + "item-1234 " + new string(unicode ? 'é' : 'x', 8192));
        var account = new Allowance(1 << 20);
        var regex = new Utf8PythonRegex("(item)-([0-9]+)");
        using var cursor = regex.CreateMatchCursor(input, 0, default, account);
        var alias = cursor;
        Assert.True(cursor.MoveNext(out var retained));
        Assert.False(cursor.HasCoordinateMap);
        Assert.True(alias.TryGetScalarRange(1, out var start, out var end));
        Assert.Equal(unicode ? 3 : 4, start);
        Assert.Equal(start + 4, end);
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(0UL, account.Used);
        Assert.Equal("item-1234", Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }
}
