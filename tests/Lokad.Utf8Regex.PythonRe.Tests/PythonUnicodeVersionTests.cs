using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// CPython 3.13.2 / Unicode 15.1: these scalars are unassigned there, even when
// the host runtime assigns them Unicode 16 letter/decimal categories.
public sealed class PythonUnicodeVersionTests
{
    public static IEnumerable<object[]> CategoryCases()
    {
        foreach (var scalar in new[] { 0x1C89, 0x1E5D0, 0x1E5F1 })
        foreach (var pattern in new[] { @"\w", @"[\w]", @"[a\w]", @"\d", @"[\d]", @"[a\d]" })
            yield return [pattern, char.ConvertFromUtf32(scalar), false];
        foreach (var scalar in new[] { 0x1C89, 0x1E5D0, 0x1E5F1 })
        foreach (var pattern in new[] { @"\W", @"[^\w]", @"[\W]", @"\D", @"[^\d]", @"[\D]" })
            yield return [pattern, char.ConvertFromUtf32(scalar), true];
        yield return [@"\w", char.ConvertFromUtf32(0x31350), true];
        yield return [@"\d", char.ConvertFromUtf32(0x1D7CE), true];
        yield return [@"\s", "\u001C", true];
        yield return [@"\S", "\u001C", false];
    }

    [Theory]
    [MemberData(nameof(CategoryCases))]
    public void ClassesUseTheProfileUnicodeVersion(string pattern, string text, bool expected)
    {
        var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
        var input = storage.AsSpan(7, Encoding.UTF8.GetByteCount(text));
        foreach (var policy in new[]
        {
            Utf8ResourcePolicy.Unlimited,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 1024,
                MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 },
        })
        foreach (var options in new[] { PythonReCompileOptions.None,
            PythonReCompileOptions.IgnoreCase | PythonReCompileOptions.Verbose })
        {
            var regex = new Utf8PythonRegex(pattern, options,
                Timeout.InfiniteTimeSpan, policy);
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.Equal(expected ? 1 : 0, regex.Count(input));
            Assert.Equal(expected, regex.SearchDetailed(input).Success);
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, 0, source.Token));
        }
    }

    [Theory]
    [InlineData(@"\b.\b", false)]
    [InlineData(@"\B.\B", true)]
    [InlineData(@"(?a:\W)", true)]
    [InlineData(@"(?a:\w)", false)]
    public void BoundariesAndAsciiScopesUseTheSameScalarClassification(string pattern, bool expected)
    {
        Assert.Equal(expected, new Utf8PythonRegex(pattern).IsMatch("\u1C89"u8));
    }

    [Theory]
    [InlineData(@"(?<=\w).", false)]
    [InlineData(@"(?<=\W).", true)]
    [InlineData(@"[\w.]+", true)]
    [InlineData(@"[^\W]", false)]
    [InlineData(@"(?a:[\w])", false)]
    [InlineData(@"(?a:(?u:\w))", false)]
    [InlineData(@"\\w", false)]
    public void AssertionsUnionsAndLexicalScopesKeepTheirMeaning(string pattern, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(pattern.Contains("<=", StringComparison.Ordinal) ? "\u1C89a"
            : pattern.Contains(".", StringComparison.Ordinal) ? "." : "\u1C89");
        Assert.Equal(expected, new Utf8PythonRegex(pattern).IsMatch(input));
        Assert.False(new Utf8PythonRegex(@"\w").IsMatch("."u8));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CapturedCursorsKeepTheirOriginalDeadlineTokenAndSnapshots(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 1024,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 };
        var regex = new Utf8PythonRegex(@"(?P<w>\w+)", PythonReCompileOptions.None,
            TimeSpan.FromMilliseconds(20), policy);
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("😀\u1C89é\U0001E5D0中"), 0, token.Token);
        var alias = cursor;
        ticks = 5; Assert.True(alias.MoveNext()); var retained = cursor.Current;
        Assert.Equal("é", retained.Groups[1].ValueText);
        Assert.Equal((7, 9), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        if (expire)
        { ticks = 25; Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext()); }
        else
        { token.Cancel(); Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext()).CancellationToken); }
        Assert.Equal("é", retained.Groups[1].ValueText);
        Assert.Equal("w", retained.LastGroup);
    }

    [Fact]
    public void PinnedClassesRetainAllFourFiniteDenials()
    {
        foreach (var kind in new[] { 0, 1, 2, 3 })
        {
            var policy = kind switch
            {
                0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 },
                1 => new Utf8ResourcePolicy { MaxDepth = 1 },
                2 => new Utf8ResourcePolicy { MaxWorkingBytes = 1 },
                _ => new Utf8ResourcePolicy { MaxResultBytes = 1 },
            };
            var regex = new Utf8PythonRegex(@"(?P<w>(?:\w|zz)+)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.SearchDetailedData("éé"u8));
            Assert.Equal(kind switch { 0 => Utf8ResourceLimitKind.ExecutionWorkSteps, 1 => Utf8ResourceLimitKind.ExecutionDepth,
                2 => Utf8ResourceLimitKind.ExecutionWorkingBytes, _ => Utf8ResourceLimitKind.ExecutionResultBytes }, error.ResourceKind);
        }
    }

    [Theory]
    [InlineData(@"cat\d+", false)]
    [InlineData(@"cat[\d]+", false)]
    [InlineData(@"cat\w+", false)]
    [InlineData(@"\bcat\d+\b", false)]
    [InlineData(@"cat\D+", true)]
    public void NativePrefixPlansCannotAdmitNewHostMembers(string pattern, bool expected)
    {
        var input = Encoding.UTF8.GetBytes("cat\U0001E5F1");
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected, regex.Match(input).Success);
        Assert.Equal(expected, regex.FullMatch(input).Success);
        Assert.Equal(expected, regex.SearchDetailed(input).Success);
        Assert.Equal(expected ? 1 : 0, regex.Count(input));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.Equal(expected, cursor.MoveNext());
    }

    [Theory]
    [InlineData(@"(?<=\B)a", 1)]
    [InlineData(@"(?a:(?<=\B)a)", 1)]
    [InlineData(@"(?<!\B)a", 0)]
    public void NonBoundaryLookbehindHasZeroScalarWidth(string pattern, int expectedStart)
    {
        var match = new Utf8PythonRegex(pattern).Search("aa"u8);
        Assert.True(match.Success);
        Assert.Equal(expectedStart, match.StartOffsetInBytes);
        Assert.Equal(expectedStart + 1, match.EndOffsetInBytes);
    }

    [Fact]
    public void NonBoundaryEmptyMatchesAdvanceAcrossWholeScalars()
    {
        var regex = new Utf8PythonRegex(@"\B");
        Assert.Equal(0, regex.Count(""u8));
        using var cursor = regex.CreateMatchCursor("😀"u8.ToArray());
        Assert.True(cursor.MoveNext()); Assert.Equal(0, cursor.Current.Value.StartOffsetInBytes);
        Assert.True(cursor.MoveNext()); Assert.Equal(4, cursor.Current.Value.StartOffsetInBytes);
        Assert.False(cursor.MoveNext());
    }
}
