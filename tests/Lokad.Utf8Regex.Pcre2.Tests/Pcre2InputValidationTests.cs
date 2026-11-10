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
}
