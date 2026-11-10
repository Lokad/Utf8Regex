using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReInputValidationTests
{
    private static (bool, int, int, int, int) Range(Utf8PythonValueMatch value)
        => (value.Success, value.StartOffsetInBytes, value.EndOffsetInBytes, value.StartOffsetInUtf16, value.EndOffsetInUtf16);

    private static T Observe<T>(Utf8PythonRegex regex, Func<T> action)
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
    [InlineData("", "é😀x")]
    public void PoliciesPropagateToPublicOperationsAndCursors(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var strict = new Utf8PythonRegex(pattern);
        var assumed = new Utf8PythonRegex(pattern, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8InputValidation.Validate, strict.InputValidation);
        Assert.Equal(Utf8InputValidation.AssumeValid, assumed.InputValidation);
        Assert.Equal(Observe(strict, () => strict.IsMatch(input)), Observe(assumed, () => assumed.IsMatch(input)));
        Assert.Equal(Observe(strict, () => Range(strict.Search(input))), Observe(assumed, () => Range(assumed.Search(input))));
        Assert.Equal(Observe(strict, () => Range(strict.Match(input))), Observe(assumed, () => Range(assumed.Match(input))));
        Assert.Equal(Observe(strict, () => Range(strict.FullMatch(input))), Observe(assumed, () => Range(assumed.FullMatch(input))));
        Assert.Equivalent(Observe(strict, () => strict.FindAllToStrings(input)), Observe(assumed, () => assumed.FindAllToStrings(input)));
        Assert.Equal(Observe(strict, () => strict.SplitToStrings(input)), Observe(assumed, () => assumed.SplitToStrings(input)));
        Assert.Equal(Observe(strict, () => strict.Replace(input, "X")), Observe(assumed, () => assumed.Replace(input, "X")));
        foreach (var regex in new[] { strict, assumed })
        {
            var expectedCount = regex.Count(input);
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                using var cursor = regex.CreateMatchCursor(input);
                var count = 0;
                while (cursor.MoveNext(out _)) count++;
                Assert.Equal(expectedCount, count);
                Assert.InRange(session.SubjectValidations, 0, regex.InputValidation == Utf8InputValidation.Validate ? 1 : 0);
            }
            finally { session.Complete(); }
        }
    }

    [Fact]
    public void DefaultsAndPatternReplacementValidationStayStrict()
    {
        byte[] invalid = [.. "needle"u8, 0xff];
        var regex = new Utf8PythonRegex("needle");
        Assert.Throws<ArgumentException>(() => regex.IsMatch(invalid));
        Assert.Throws<ArgumentException>(() => regex.Count(invalid));
        Assert.Throws<ArgumentException>(() => regex.Replace(invalid, "X"));
        Assert.Throws<ArgumentException>(() => regex.SplitToStrings(invalid));
        Assert.Throws<ArgumentException>(() => regex.CreateMatchCursor(invalid));
        var assumed = new Utf8PythonRegex("needle"u8, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8InputValidation.AssumeValid, assumed.InputValidation);
        Assert.Throws<ArgumentException>(() => new Utf8PythonRegex(new byte[] { 0xff }, inputValidation: Utf8InputValidation.AssumeValid));
        Assert.Throws<ArgumentException>(() => assumed.Subn("needle"u8, new byte[] { 0xff }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PythonRegex("x", inputValidation: (Utf8InputValidation)10));
        Assert.Equal(Utf8InputValidation.Validate, new Utf8PythonRegex("x", default).InputValidation);
        using var compiled = Utf8PythonRegex.Compile("needle"u8, default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8InputValidation.AssumeValid, compiled.InputValidation);
        Assert.True(compiled.FullMatch("needle"u8).Success);
    }
}
