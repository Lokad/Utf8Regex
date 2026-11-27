using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OrderedWindowInvocationTests
{
    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    [InlineData(4095)] [InlineData(4096)]
    public void LongPrefixesPreserveScalarGapsAndPublicAdmission(int length)
    {
        var head = string.Concat(Enumerable.Range(0, length).Select(index => (char)('a' + index % 23)));
        var bytes = Encoding.UTF8.GetBytes("é!" + head + "😀z");
        var regex = new Utf8Regex(head + ".{1,2}z");
        using var token = new CancellationTokenSource();
        byte[] storage = [0xff, .. bytes, 0xff];
        var borrowed = storage.AsSpan(1, bytes.Length);
        Assert.Equal(1, regex.Count(borrowed, token.Token));
        var match = regex.Match(borrowed, cancellationToken: token.Token);
        Assert.True(match.Success);
        Assert.Equal(3, match.StartOffsetInBytes);
        Assert.Equal(3 + length + 5, match.EndOffsetInBytes);
        var denied = new Utf8Regex(head + ".{1,2}z", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxDepth = 4096
            }
        });
        using var tinyWorking = new Utf8Regex(head + ".{1,2}z", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1
            }
        });
        Assert.Equal(1, tinyWorking.Count(bytes));
    }

    private static readonly string[] Patterns = [@"a\s+b", @"a\s{2,}b", @"a\s+?b", @"a.{0,4}b", @"river.{10,25}Tom",
        @"Holmes.{0,25}Watson|Watson.{0,25}Holmes", @"\b(?:public|private|internal)\b\s+.{0,80}\bclass\b",
        @"HttpClient(?=\s+client)", @"(?:a|ab).{0,4}z", @"a\s+ b", @"a\s+ ", @"a.{0,4}\nb", @"(?:foo|bar).{0,8}z",
        @"a[ \t]+b", @"a[ \t]+?.{0,4}b", @"a\s*.{0,4}b", @"a\s+?.{0,4}b",
        @"a[ ]+.{0,4}z|b[\t]+?.{0,4}z", @"a[ \t]+bb", @"a\s+ bb", @"a \s+bb", @"(?:a|long )\s+.{0,4}bb"];

    private static readonly string[] Texts = ["", "xa  ba\t\nb a1b2b a3b a\nb a  b a\n\nb abz a1z2z foo1z2z barz foobaz",
        "Holmes and Watson Watson met Holmes Holmes and Watson", "public sealed class One private class Two internal partial class Three",
        "HttpClient client HttpClient\tclient HttpClient other", "river123456789Tom river1234567890Tom",
        "a\t\t b a     a\r\nb a\n\nb a\tb a\v\fb a   z b\t\tz a\nbb a\tbb a  bb long  bb",
        "é😀 a\u2003b Holmes and Watson public\u2003class One HttpClient\u2003client"];

    public static IEnumerable<object[]> ValueCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        foreach (var liveToken in new[] { false, true }) yield return [pattern, compiled, finite, liveToken];
    }

    [Theory]
    [MemberData(nameof(ValueCases))]
    public void ValueAndEnumerationModesPreserveGreedyRangesSeparatorsAndBranchPriority(string pattern, bool compiled, bool finite, bool liveToken)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout
        });
        var oracle = new Regex(pattern, options);
        using var source = new CancellationTokenSource();
        var token = liveToken ? source.Token : default;
        foreach (var text in Texts)
        {
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, token));
            Assert.Equal(oracle.Count(text), regex.Count(input, token));
            var expectedMatch = oracle.Match(text);
            var actualMatch = regex.Match(input, cancellationToken: token);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (actualMatch.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
                Assert.Equal(expectedMatch.Value, Encoding.UTF8.GetString(input.AsSpan(actualMatch.StartOffsetInBytes, (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes))));
            }
            var expected = oracle.EnumerateMatches(text);
            var actual = regex.EnumerateMatches(input, cancellationToken: token);
            while (expected.MoveNext())
            {
                Assert.True(actual.MoveNext());
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Current.Index)), actual.Current.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Current.Index, expected.Current.Length)), (actual.Current.EndOffsetInBytes - actual.Current.StartOffsetInBytes));
            }
            Assert.False(actual.MoveNext());
            Assert.False(actual.MoveNext());
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, 17)]
    [InlineData(true, 17)]
    [InlineData(false, 83)]
    [InlineData(true, 83)]
    public void SeededAsciiWindowsAgreeWithIndependentRegexOnEveryAcceptedRange(bool compiled, int seed)
    {
        var random = new Random(seed);
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        foreach (var pattern in Patterns)
        {
            var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.FromSeconds(10)
            });
            var oracle = new Regex(pattern, options);
            for (var trial = 0; trial < 40; trial++)
            {
                const string alphabet = "aabbzzfoo bar\t\r\n";
                var text = new string(Enumerable.Range(0, 72).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
                var input = Encoding.UTF8.GetBytes(text);
                var actual = regex.EnumerateMatches(input);
                foreach (Match expected in oracle.Matches(text))
                {
                    Assert.True(actual.MoveNext(), $"{pattern}: {text}");
                    Assert.Equal((expected.Index, expected.Index + expected.Length),
                        (actual.Current.StartOffsetInBytes, actual.Current.EndOffsetInBytes));
                }
                Assert.False(actual.MoveNext());
            }
        }
    }

    public static IEnumerable<object[]> InterruptedCases()
    {
        foreach (var separator in new[] { false, true })
        foreach (var compiled in new[] { false, true })
        foreach (var operation in new[] { "ismatch", "count", "match", "enum" })
        foreach (var phase in new[] { "search", "consumption" })
        foreach (var expire in new[] { false, true }) yield return [separator, compiled, operation, phase, expire];
    }

    [Theory]
    [MemberData(nameof(InterruptedCases))]
    public void PublicWindowPhasesCarryEntryInvocationAndMapCancellationOrExpiry(bool separator, bool compiled, string operation, string phase, bool expire)
    {
        var pattern = separator ? @"a\s+b" : @"a.{0,16384}b";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var text = phase == "consumption" ? "a" + new string(separator ? ' ' : 'x', 8192) + "b" : new string('x', 8192);
        var input = Encoding.UTF8.GetBytes(text);
        long ticks = 0; var reads = 0; var trigger = int.MaxValue;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == trigger) { if (expire) ticks = 100; else source.Cancel(); } return ticks; });
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50)
        });
        void Run()
        {
            if (operation == "ismatch") _ = regex.IsMatch(input, source.Token);
            else if (operation == "count") _ = regex.Count(input, source.Token);
            else if (operation == "match") _ = regex.Match(input, cancellationToken: source.Token);
            else { var cursor = regex.EnumerateMatches(input, cancellationToken: source.Token); _ = cursor.MoveNext(); }
        }
        reads = 0; Run(); Assert.True(reads >= 2); trigger = Math.Max(2, reads / 2); reads = 0;
        if (expire)
        {
            var exception = Assert.Throws<RegexMatchTimeoutException>(Run);
            Assert.Equal(pattern, exception.Pattern); Assert.Equal(text, exception.Input);
            Assert.Equal(regex.MatchTimeout, exception.MatchTimeout);
        }
        else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
        Assert.True(regex.IsMatch("a b"u8));
    }

    public static IEnumerable<object[]> CompleteCases()
    {
        foreach (var pattern in new[] { @"a\s+b", @"a.{0,4}\nb", @"Holmes.{0,25}Watson|Watson.{0,25}Holmes", @"(?<x>a)\s+(?<y>b)" })
        foreach (var compiled in new[] { false, true }) yield return [pattern, compiled];
    }

    [Theory]
    [MemberData(nameof(CompleteCases))]
    public void CompleteOperationsKeepCapturesOffsetsSplitsAndReplacement(string pattern, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(10)
        });
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, options), Texts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountInterruptionCannotPublishPartialTotal(bool compiled)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("a b ", 4000)));
        long ticks = 0; var reads = 0; var trigger = int.MaxValue;
        using var source = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == trigger) source.Cancel(); return ticks; });
        var regex = new Utf8Regex(@"a\s+b", options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None), policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50)
        });
        reads = 0; Assert.Equal(4000, regex.Count(input, source.Token));
        Assert.True(reads > 4); trigger = reads / 2; reads = 0;
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
        Assert.Equal(4000, regex.Count(input));
    }
}
