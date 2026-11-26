using Lokad.Utf8Regex.Internal.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OperationCursorArchitectureTests
{
    [Theory]
    [InlineData("é", 2)]
    [InlineData("é|中", 3)]
    public void LiteralCursorCopiesKeepTheirPlanAcrossCompaction(string pattern, int expectedCount)
    {
        var cursor = OpenTemporaryLiteralCursor(pattern);
        var alias = cursor;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        var count = 0;
        while (cursor.MoveNext())
        {
            Assert.True(alias.MoveNext());
            Assert.Equal(cursor.Current, alias.Current);
            count++;
        }
        Assert.Equal(expectedCount, count);
        Assert.False(alias.MoveNext());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8ValueMatchEnumerator OpenTemporaryLiteralCursor(string pattern)
    {
        using var regex = new Utf8Regex(pattern, policy: new() { MatchTimeout = Regex.InfiniteMatchTimeout });
        var cursor = regex.EnumerateMatches("é é 中"u8);
        Assert.False(cursor.DebugIsOwnedSlice);
        return cursor;
    }

    [Fact]
    public void DiagnosticSessionsNestWithoutChangingResults()
    {
        var regex = new Utf8Regex("ab.*cd", options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("ab12cd xx ab34cd");
        var expected = regex.Count(input);
        var outer = Utf8SearchDiagnosticsSession.Start();

        var outerResult = regex.Count(input);
        var inner = Utf8SearchDiagnosticsSession.Start();
        var innerResult = regex.Count(input);
        inner.Complete();

        Assert.Same(outer, Utf8SearchDiagnosticsSession.Current);
        Assert.Equal(expected, outerResult);
        Assert.Equal(expected, innerResult);
        outer.Complete();
        Assert.Null(Utf8SearchDiagnosticsSession.Current);
        Assert.Equal(expected, regex.Count(input));
    }

    [Theory]
    [InlineData("ab", "xxabyyab", false)]
    [InlineData("", "abc", false)]
    [InlineData("(a)(b)", "xxabyyab", false)]
    [InlineData("ab.*cd", "ab12cd xx ab34cd", false)]
    [InlineData(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", "LogError( skip LogDebug( LogInformation (", false)]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "class lower record Customer struct Worker_State", false)]
    [InlineData("ab", "xxabyyab", true)]
    public void CompiledAndInterpretedOperationAdaptersRemainEquivalent(
        string pattern,
        string text,
        bool rightToLeft)
    {
        var baseOptions = RegexOptions.CultureInvariant |
            (rightToLeft ? RegexOptions.RightToLeft : RegexOptions.None);
        var interpreted = new Utf8Regex(pattern, options: baseOptions);
        var compiled = new Utf8Regex(pattern, options: baseOptions | RegexOptions.Compiled);
        var input = Encoding.UTF8.GetBytes(text);

        Assert.Equal(interpreted.IsMatch(input), compiled.IsMatch(input));
        Assert.Equal(interpreted.Match(input), compiled.Match(input));
        Assert.Equal(interpreted.Count(input), compiled.Count(input));
        Assert.Equal(CollectMatches(interpreted, input), CollectMatches(compiled, input));
        Assert.Equal(CollectSplits(interpreted, input), CollectSplits(compiled, input));
        Assert.Equal(interpreted.Replace(input, "<$&>"u8), compiled.Replace(input, "<$&>"u8));
    }

    private static List<Utf8CaptureRange> CollectMatches(Utf8Regex regex, byte[] input)
    {
        var matches = new List<Utf8CaptureRange>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            matches.Add(match);
        }

        return matches;
    }

    private static List<(int IndexInBytes, int LengthInBytes, string Value)> CollectSplits(
        Utf8Regex regex,
        byte[] input)
    {
        var splits = new List<(int, int, string)>();
        foreach (var split in regex.EnumerateSplits(input))
        {
            splits.Add((
                split.IndexInBytes,
                split.LengthInBytes,
                Encoding.UTF8.GetString(split.GetValueBytes())));
        }

        return splits;
    }

}
