using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2InputValidationTests
{
    private static (bool, int, int, int, int) Range(Utf8Pcre2ValueMatch value)
        => (value.Success, value.StartOffsetInBytes, value.EndOffsetInBytes, value.StartOffsetInUtf16, value.EndOffsetInUtf16);

    private static T Observe<T>(Utf8Pcre2Regex regex, Func<T> action)
    {
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = action();
            Assert.InRange(session.SubjectValidations, 0, regex.InputValidation == Utf8InputValidation.Validate ? 1 : 0);
            return result;
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData("café|😀", "z😀 café!")]
    [InlineData("(a+)(b*)", "é😀 aabb aa")]
    [InlineData("(?|(a)|(b))", "é😀 ab")]
    [InlineData("", "é😀x")]
    public void PoliciesPreserveResultsAndCursorLifetime(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var strict = new Utf8Pcre2Regex(pattern);
        var assumed = new Utf8Pcre2Regex(pattern, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8InputValidation.Validate, strict.InputValidation);
        Assert.Equal(Utf8InputValidation.AssumeValid, assumed.InputValidation);
        Assert.Equal(Observe(strict, () => strict.IsMatch(input)), Observe(assumed, () => assumed.IsMatch(input)));
        Assert.Equal(Observe(strict, () => strict.Count(input)), Observe(assumed, () => assumed.Count(input)));
        Assert.Equal(Observe(strict, () => Range(strict.Match(input))), Observe(assumed, () => Range(assumed.Match(input))));
        Assert.Equal(Observe(strict, () => strict.Replace(input, "X")), Observe(assumed, () => assumed.Replace(input, "X")));
        foreach (var regex in new[] { strict, assumed })
        {
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                using var cursor = regex.CreateMatchCursor(input);
                var count = 0;
                while (cursor.MoveNext(out _)) count++;
                Assert.InRange(session.SubjectValidations, 0, regex.InputValidation == Utf8InputValidation.Validate ? 1 : 0);
                Assert.Equal(regex.Count(input), count);
            }
            finally { session.Complete(); }
        }
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void GraphemeBooleanPreservesScalarFloorAndWholeInputPolicy(Utf8InputValidation policy)
    {
        var regex = new Utf8Pcre2Regex(@"\X", inputValidation: policy);
        var input = Encoding.UTF8.GetBytes("é😀");
        Assert.True(Observe(regex, () => regex.IsMatch(input)));
        Assert.True(Observe(regex, () => regex.IsMatch(input, 2)));
        Assert.False(Observe(regex, () => regex.IsMatch(input, input.Length)));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(input, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(input, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(input, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(input, input.Length + 1));
        if (policy == Utf8InputValidation.Validate)
            Assert.Throws<ArgumentException>(() => regex.IsMatch(new byte[] { (byte)'x', 0xff }));
    }

    [Fact]
    public void DefaultsAndTemplatePatternValidationStayStrict()
    {
        byte[] invalid = [.. "needle"u8, 0xff];
        var regex = new Utf8Pcre2Regex("needle");
        Assert.Throws<ArgumentException>(() => regex.IsMatch(invalid));
        Assert.Throws<ArgumentException>(() => regex.Count(invalid));
        Assert.Throws<ArgumentException>(() => regex.Replace(invalid, "X"));
        Assert.Throws<ArgumentException>(() => regex.CreateMatchCursor(invalid));
        var assumed = new Utf8Pcre2Regex("needle"u8, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Throws<ArgumentException>(() => new Utf8Pcre2Regex(new byte[] { 0xff }, inputValidation: Utf8InputValidation.AssumeValid));
        Assert.Throws<ArgumentException>(() => assumed.Replace("needle"u8, new byte[] { 0xff }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Pcre2Regex("x", inputValidation: (Utf8InputValidation)10));
        Assert.Equal(Utf8InputValidation.Validate, new Utf8Pcre2Regex("x", default).InputValidation);
        var created = Utf8Pcre2Regex.Create("needle"u8, default, default, default, Timeout.InfiniteTimeSpan,
            inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8InputValidation.AssumeValid, created.InputValidation);
        Assert.Equal(1, created.Count("é😀 needle"u8));
    }

    [Theory]
    [InlineData("needle")]
    [InlineData("(needle)")]
    [InlineData("needle|other")]
    [InlineData("(?=needle)needle")]
    [InlineData("n[ae]+dle")]
    public void NonzeroStartChecksTheNativeWindowWithoutRejectingAnIgnoredPrefix(string pattern)
    {
        byte[] input = [0xff, (byte)' ', .. "needle"u8];
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.True(regex.IsMatch(input, 2));
        Assert.Equal(1, regex.Count(input, 2));
        var match = regex.Match(input, 2);
        Assert.True(match.Success);
        Assert.Equal((2, input.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        Assert.True(match.IsUtf8SliceWellFormed);
        Assert.False(match.HasUtf16Projection);
        Assert.Equal("needle", match.GetValueString());
        var detailed = regex.MatchDetailed(input, 2);
        Assert.True(detailed.GetGroup(0).Success);
        Assert.False(detailed.GetGroup(0).HasUtf16Projection);
        Assert.Equal("needle", detailed.GetGroup(0).GetValueString());
        var enumerator = regex.EnumerateMatches(input, 2);
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.Current.HasUtf16Projection);
        Assert.False(enumerator.MoveNext());
        using var cursor = regex.CreateMatchCursor(input, 2);
        Assert.True(cursor.MoveNext(out var range));
        Assert.True(range.Success);
        Assert.False(cursor.MoveNext(out _));
        Span<Utf8Pcre2MatchData> buffer = stackalloc Utf8Pcre2MatchData[2];
        Assert.Equal(1, regex.MatchMany(input, buffer, out var more, 2, Pcre2MatchOptions.None));
        Assert.False(more);
        Assert.False(buffer[0].HasUtf16Projection);
        Assert.Throws<ArgumentException>(() => regex.IsMatch(input, 0));
        byte[] badTail = [.. input, 0xff];
        var error = Assert.Throws<ArgumentException>(() => regex.Count(badTail, 2));
        Assert.Contains($"byte offset {input.Length}.", error.Message);
    }

    [Theory]
    [InlineData(@"\bneedle", " ")]
    [InlineData(@"\Bneedle", "x")]
    [InlineData(@"(?<=ab)needle", "ab")]
    [InlineData(@"(?<!zz)needle", "ab")]
    [InlineData(@"(?<=é😀)needle", "é😀")]
    public void LookbehindMovesByScalarsAndReportsAbsoluteInvalidOffsets(string pattern, string context)
    {
        var prefix = Encoding.UTF8.GetBytes(context);
        byte[] input = [0xff, .. prefix, .. "needle"u8];
        var start = prefix.Length + 1;
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.True(regex.IsMatch(input, start));
        Assert.Equal(1, regex.Count(input, start));
        using var cursor = regex.CreateMatchCursor(input, start);
        Assert.True(cursor.MoveNext(out _));
        input[1] = 0xff;
        var error = Assert.Throws<ArgumentException>(() => regex.IsMatch(input, start));
        Assert.Contains("byte offset 1.", error.Message);
        Assert.Throws<ArgumentException>(() => regex.CreateMatchCursor(input, start));
    }

    [Theory]
    [InlineData(@"\Aneedle")]
    [InlineData(@"(?#comment)\Aneedle")]
    public void NativeAbsoluteStartRecordsLookbehindEvenWhenTheAnchorWillMiss(string pattern)
    {
        byte[] input = [0xff, .. "needle"u8];
        Assert.Throws<ArgumentException>(() => new Utf8Pcre2Regex(pattern).IsMatch(input, 1));
        Assert.False(new Utf8Pcre2Regex("^needle").IsMatch(input, 1));
        Assert.True(new Utf8Pcre2Regex(@"\Gneedle").IsMatch(input, 1));
        Assert.True(new Utf8Pcre2Regex("needle", Pcre2CompileOptions.Anchored).IsMatch(input, 1));
    }

    [Fact]
    public void TrustedByteCursorDoesNotPrepareCoordinatesOrClassifyAnUnconsumedSuffix()
    {
        byte[] input = [.. "needle "u8, .. Encoding.UTF8.GetBytes(new string('é', 65536))];
        var regex = new Utf8Pcre2Regex("(needle)", defaultExecutionLimits: new() { MatchLimit = 64 },
            inputValidation: Utf8InputValidation.AssumeValid);
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var match));
        Assert.True(match.Success);
        var strict = new Utf8Pcre2Regex("(needle)", defaultExecutionLimits: new() { MatchLimit = 64 });
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => strict.CreateMatchCursor(input)).ErrorKind);
    }

    [Fact]
    public void ControlledTrustedBooleanDoesNotDescribeAnUnconsumedSuffix()
    {
        byte[] input = [.. "foo123bar "u8, .. Encoding.UTF8.GetBytes(new string('é', 65536))];
        var regex = new Utf8Pcre2Regex("foo[0-9]+bar", defaultExecutionLimits: new() { MatchLimit = 64 },
            inputValidation: Utf8InputValidation.AssumeValid);
        Assert.True(regex.IsMatch(input));
        Assert.True(regex.IsMatch(input, 0, Pcre2MatchOptions.None));
    }
}
