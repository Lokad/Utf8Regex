using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

internal readonly record struct RegexParityCase(string Pattern, string Input, RegexOptions Options)
{
    public byte[] Utf8Input => Encoding.UTF8.GetBytes(Input);
}

internal sealed class RegexParityContext
{
    private RegexParityContext(RegexParityCase parityCase)
    {
        Case = parityCase;
        Utf8 = new Utf8Regex(parityCase.Pattern, options: parityCase.Options);
        Utf8Compiled = new Utf8Regex(parityCase.Pattern, options: parityCase.Options | RegexOptions.Compiled);
        DotNet = new Regex(parityCase.Pattern, parityCase.Options, Regex.InfiniteMatchTimeout);
        DotNetCompiled = new Regex(parityCase.Pattern, parityCase.Options | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
    }

    public RegexParityCase Case { get; }

    public Utf8Regex Utf8 { get; }

    public Utf8Regex Utf8Compiled { get; }

    public Regex DotNet { get; }

    public Regex DotNetCompiled { get; }

    public static RegexParityContext Create(string pattern, string input, RegexOptions options)
        => new(new RegexParityCase(pattern, input, options));

    public void AssertIsMatchParity()
    {
        var bytes = Case.Utf8Input;

        var expected = DotNet.IsMatch(Case.Input);
        Assert.Equal(expected, DotNetCompiled.IsMatch(Case.Input));
        Assert.Equal(expected, Utf8.IsMatch(bytes));
        Assert.Equal(expected, Utf8Compiled.IsMatch(bytes));
    }

    public void AssertMatchParity()
    {
        var bytes = Case.Utf8Input;

        var expected = DotNet.Match(Case.Input);
        var expectedCompiled = DotNetCompiled.Match(Case.Input);
        var actual = Utf8.Match(bytes);
        var actualCompiled = Utf8Compiled.Match(bytes);

        AssertEquivalentMatch(expected, actual);
        AssertEquivalentMatch(expectedCompiled, actualCompiled);
        AssertEquivalentMatch(expected, actualCompiled);
    }

    public void AssertReplaceParity(string replacement)
    {
        var bytes = Case.Utf8Input;

        var expected = Encoding.UTF8.GetBytes(DotNet.Replace(Case.Input, replacement));
        var expectedCompiled = Encoding.UTF8.GetBytes(DotNetCompiled.Replace(Case.Input, replacement));
        var actual = Utf8.Replace(bytes, Encoding.UTF8.GetBytes(replacement));
        var actualCompiled = Utf8Compiled.Replace(bytes, Encoding.UTF8.GetBytes(replacement));

        Assert.Equal(expected, expectedCompiled);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, actualCompiled);
    }

    public void AssertCountParity(int startAt = 0)
    {
        var bytes = Case.Utf8Input;

        var expected = DotNet.Count(Case.Input, startAt);
        var expectedCompiled = DotNetCompiled.Count(Case.Input, startAt);
        var actual = CountMatches(Utf8.EnumerateMatches(bytes, Encoding.UTF8.GetByteCount(Case.Input.AsSpan(0, startAt))));
        var actualCompiled = CountMatches(Utf8Compiled.EnumerateMatches(bytes, Encoding.UTF8.GetByteCount(Case.Input.AsSpan(0, startAt))));

        Assert.Equal(expected, expectedCompiled);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, actualCompiled);
    }

    public void AssertCaptureMatchParity()
    {
        var bytes = Case.Utf8Input;
        Utf8RangeMatch actual, actualCompiled;
        using (var cursor = Utf8.CreateMatchCursor(bytes)) cursor.MoveNext(out actual);
        using (var cursor = Utf8Compiled.CreateMatchCursor(bytes)) cursor.MoveNext(out actualCompiled);
        AssertEquivalentCaptureMatch(DotNet, DotNet.Match(Case.Input), actual);
        AssertEquivalentCaptureMatch(DotNetCompiled, DotNetCompiled.Match(Case.Input), actualCompiled);
    }

    public void AssertCaptureMatchParityFromOracleOffset(int offset)
    {
        var bytes = Case.Utf8Input;

        var expected = DotNet.Match(Case.Input, offset);
        var expectedCompiled = DotNetCompiled.Match(Case.Input, offset);
        var startByte = Encoding.UTF8.GetByteCount(Case.Input.AsSpan(0, offset));
        Utf8RangeMatch actual, actualCompiled;
        using (var cursor = Utf8.CreateMatchCursor(bytes, startByte)) cursor.MoveNext(out actual);
        using (var cursor = Utf8Compiled.CreateMatchCursor(bytes, startByte)) cursor.MoveNext(out actualCompiled);
        AssertEquivalentCaptureMatch(DotNet, expected, actual);
        AssertEquivalentCaptureMatch(DotNetCompiled, expectedCompiled, actualCompiled);
    }

    public void AssertEnumeratedCaptureParity()
    {
        var bytes = Case.Utf8Input;
        foreach (var (oracle, regex) in new[] { (DotNet, Utf8), (DotNetCompiled, Utf8Compiled) })
        {
            using var cursor = regex.CreateMatchCursor(bytes);
            foreach (Match expected in oracle.Matches(Case.Input))
            {
                Assert.True(cursor.MoveNext(out var actual));
                AssertEquivalentCaptureMatch(oracle, expected, actual);
            }
            Assert.False(cursor.MoveNext(out _));
        }
    }

    private static int CountMatches(Utf8ValueMatchEnumerator matches)
    {
        var count = 0;
        while (matches.MoveNext()) count = checked(count + 1);
        return count;
    }

    private void AssertEquivalentMatch(Match expected, Utf8CaptureRange actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(Encoding.UTF8.GetByteCount(Case.Input.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), actual.EndOffsetInBytes - actual.StartOffsetInBytes);
    }

    private void AssertEquivalentCaptureMatch(Regex oracle, Match expected, Utf8RangeMatch actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        AssertRange(expected, actual.Value);
        Assert.Equal(Encoding.UTF8.GetBytes(expected.Value), actual.GetValueMemory().ToArray());
        if (!expected.Success) return;
        var numbers = oracle.GetGroupNumbers();
        Assert.Equal(numbers, actual.GroupNumbers.ToArray());

        foreach (var number in numbers)
        {
            var expectedGroup = expected.Groups[number];
            Assert.True(actual.TryGetGroup(number, out var group));
            Assert.Equal(expectedGroup.Success, group.Success);
            AssertRange(expectedGroup, group);
            Assert.Equal(Encoding.UTF8.GetBytes(expectedGroup.Value), actual.GetGroupMemory(number).ToArray());
            var captures = actual.GetCaptures(number);
            Assert.Equal(expectedGroup.Captures.Count, captures.Length);
            for (var index = 0; index < captures.Length; index++)
            {
                Assert.True(captures[index].Success);
                AssertRange(expectedGroup.Captures[index], captures[index]);
            }
        }
        Assert.False(actual.TryGetGroup(numbers[^1] + 1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => actual.GetCaptures(numbers[^1] + 1).Length);
    }

    private void AssertRange(Capture expected, Utf8CaptureRange actual)
    {
        Assert.Equal(Encoding.UTF8.GetByteCount(Case.Input.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(Case.Input.AsSpan(0, expected.Index + expected.Length)), actual.EndOffsetInBytes);
    }
}
