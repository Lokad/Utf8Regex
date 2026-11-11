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
            }, inputValidation: Utf8InputValidation.AssumeValid);
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(value, regex.MatchToString(input, 6, cancellation.Token));
        Assert.Equal(value, regex.SearchToString(input, 6, cancellation.Token));

        using var strict = new Utf8PythonRegex($"({value})");
        input[^1] = 0xff;
        Assert.Throws<ArgumentException>(() => strict.MatchToString(input, 6));
        cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => regex.MatchToString(input, 6, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void OneShotTextAdmitsTheActualStringBeforeAllocation(Utf8InputValidation inputValidation)
    {
        const string text = "é😀";
        var required = Utf8ConstructionBudget.StringBytes(text.Length);
        using var regex = new Utf8PythonRegex($"({text})",
            resourcePolicy: new Utf8ResourcePolicy { MaxWorkingBytes = 32768, MaxResultBytes = required - 1 },
            inputValidation: inputValidation);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.MatchToString("é😀tail"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, failure.ResourceKind);
        Assert.Equal(required - 1, failure.Limit);
        Assert.Equal(required, failure.Requested);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void OneShotTextKeepsLargeScalarRangesAndExactAdmission(Utf8InputValidation inputValidation)
    {
        var expected = new string('a', 4093) + "é😀";
        var input = Encoding.UTF8.GetBytes("é😀" + expected);
        using var regex = new Utf8PythonRegex("(?s)(.+)",
            resourcePolicy: new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 32768, MaxResultBytes = Utf8ConstructionBudget.StringBytes(expected.Length),
            }, inputValidation: inputValidation);
        var retained = regex.MatchToString(input, 6);
        Assert.Equal(expected, retained);
        Assert.Equal(expected, regex.SearchToString(input, 6));
        Assert.Equal(expected, regex.FullMatchToString(input, 6));
        Assert.Equal(expected, retained);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void OneShotOwnedTextReturnsProgramFundingAndSurvivesClosure(Utf8InputValidation inputValidation)
    {
        var allowance = new Allowance(16 << 20);
        Assert.True(allowance.TryReserve(17));
        using var regex = Utf8PythonRegex.Compile("(é😀)", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default,
            allowance, inputValidation);
        var held = allowance.Used;
        var retained = regex.MatchToString("é😀suffix"u8);
        Assert.Equal("é😀", retained);
        Assert.Equal(held, allowance.Used);
        regex.Dispose();
        Assert.Equal(17UL, allowance.Used);
        Assert.Equal("é😀", retained);
        allowance.Release(17);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void ValuesAtSubjectEndsKeepRealUtf16Coordinates(Utf8InputValidation policy)
    {
        var input = "é😀 item-1234"u8.ToArray();
        var regex = new Utf8PythonRegex("(item)-([0-9]+)", inputValidation: policy);
        var value = regex.Search(input);
        Assert.True(value.Success);
        Assert.Equal((7, 16, 4, 13), (value.StartOffsetInBytes, value.EndOffsetInBytes,
            value.StartOffsetInUtf16, value.EndOffsetInUtf16));
        Assert.False(regex.Match(input).Success);
        Assert.True(regex.FullMatch(input, 7).Success);
        Assert.Equal(new[] { "item", "1234" }, regex.FindAllToStrings(input).TupleValues[0]);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate, false)]
    [InlineData(Utf8InputValidation.Validate, true)]
    [InlineData(Utf8InputValidation.AssumeValid, false)]
    [InlineData(Utf8InputValidation.AssumeValid, true)]
    public void RangeAliasesCanRequestCoordinatesLaterAndRefund(Utf8InputValidation policy, bool unicode)
    {
        var prefix = unicode ? "é😀 " : "abc ";
        var input = Encoding.UTF8.GetBytes(prefix + "item-1234 " + new string(unicode ? 'é' : 'x', 8192));
        var account = new Allowance(1 << 20);
        var regex = new Utf8PythonRegex("(item)-([0-9]+)", inputValidation: policy);
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

    [Theory]
    [InlineData("(?!)")]
    [InlineData("(missing)-([0-9]+)")]
    public void StrictEarlyFailureStillRejectsMalformedSuffix(string pattern)
    {
        byte[] input = [.. "item-1234"u8, 0xff];
        var regex = new Utf8PythonRegex(pattern);
        Assert.Throws<ArgumentException>(() => regex.Match(input));
        Assert.Throws<ArgumentException>(() => regex.Search(input));
        Assert.Throws<ArgumentException>(() => regex.CreateMatchCursor(input));
    }
}
