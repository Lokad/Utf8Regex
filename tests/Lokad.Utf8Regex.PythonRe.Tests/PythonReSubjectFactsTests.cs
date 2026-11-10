using System.Text;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReSubjectFactsTests
{
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
