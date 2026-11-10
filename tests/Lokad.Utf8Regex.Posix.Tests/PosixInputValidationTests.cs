using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixInputValidationTests
{
    [Theory]
    [InlineData("(a|aa)(a*)", "é😀 aaa aa")]
    [InlineData("(é|é😀)(.*)", "😀é😀x")]
    [InlineData("()", "é😀x")]
    public void PoliciesPreserveSelectionCapturesAndCursorValidation(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var strict = new Utf8PosixRegex(pattern);
        var assumed = new Utf8PosixRegex(pattern, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8InputValidation.Validate, strict.InputValidation);
        Assert.Equal(Utf8InputValidation.AssumeValid, assumed.InputValidation);
        Assert.Equal(strict.IsMatch(input), assumed.IsMatch(input));
        Assert.Equal(strict.Count(input), assumed.Count(input));
        var before = new Utf8ValueMatch[strict.GroupCount + 1];
        var after = new Utf8ValueMatch[before.Length];
        Assert.Equal(strict.Match(input, before), assumed.Match(input, after));
        Assert.Equal(before, after);
        foreach (var regex in new[] { strict, assumed })
        {
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                using var cursor = regex.CreateMatchCursor(input);
                while (cursor.MoveNext(after, out _)) { }
                Assert.InRange(session.SubjectValidations, 0, regex.InputValidation == Utf8InputValidation.Validate ? 1 : 0);
            }
            finally { session.Complete(); }
        }
    }

    [Fact]
    public void DefaultRejectsMalformedSuffixAndFactoryKeepsPolicy()
    {
        byte[] invalid = [.. "needle"u8, 0xff];
        var strict = new Utf8PosixRegex("needle");
        Assert.Throws<ArgumentException>(() => strict.IsMatch(invalid));
        Assert.Throws<ArgumentException>(() => strict.Count(invalid));
        Assert.Throws<ArgumentException>(() => strict.Match(invalid, new Utf8ValueMatch[1]));
        Assert.Throws<ArgumentException>(() => strict.CreateMatchCursor(invalid));
        var assumed = Utf8PosixRegex.Compile("needle", default, Timeout.InfiniteTimeSpan, default, default,
            Timeout.InfiniteTimeSpan, default, inputValidation: Utf8InputValidation.AssumeValid);
        Assert.Equal(Utf8InputValidation.AssumeValid, assumed.InputValidation);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PosixRegex("x", inputValidation: (Utf8InputValidation)10));
    }
}
