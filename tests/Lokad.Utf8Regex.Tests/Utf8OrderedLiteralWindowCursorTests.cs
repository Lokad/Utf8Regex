using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OrderedLiteralWindowCursorTests
{
    public static IEnumerable<object[]> Windows()
    {
        yield return [@"a\s{2,}b", "xa  ba\t\nb end a b"];
        yield return [@"a\s+?b", "xa ba\t\nb end acb"];
        yield return [@"a.{0,4}b", "a1b2b a3b a\nb"];
        yield return [@"river.{10,25}Tom", "river123456789Tom river1234567890Tom riverxxxxxxxxxxxxxxxxxxxxxxxxxxTom"];
        yield return [@"Holmes.{0,25}Watson|Watson.{0,25}Holmes", "Holmes and Watson\nWatson met Holmes\nHolmes nobody"];
        yield return [@"\b(?:public|private|internal)\b\s+.{0,80}\bclass\b", "publicclass noise public sealed class One\ninternal partial class Two\nprivate struct Skip"];
        yield return [@"HttpClient(?=\s+client)", "HttpClient client HttpClient\tclient HttpClient other"];
        yield return [@"\ba.{0,4}b\b", "xa-b a-b a.b a-bx"];
        yield return [@"a\s{2,}b", "no match here"];
        yield return [@"a\s{2,}b", ""];
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void WindowCursorsOwnEnumerationSplitsAndReplacement(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        foreach (var finiteTimeout in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var timeout = finiteTimeout ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout;
            var oracle = new Regex(pattern, options, timeout);
            var regex = new Utf8Regex(pattern, options, timeout);
            Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, regex.Inspection.ExecutionKind);

            var expectedMatches = oracle.Matches(text).Select(m => (m.Index, m.Length)).ToArray();
            var actualMatches = new List<(int, int)>();
            var cursor = regex.EnumerateMatches(input);
            while (cursor.MoveNext())
            {
                var match = cursor.Current;
                Assert.True(match.IsByteAligned);
                Assert.Equal(match.IndexInUtf16, match.IndexInBytes);
                Assert.Equal(match.LengthInUtf16, match.LengthInBytes);
                actualMatches.Add((match.IndexInUtf16, match.LengthInUtf16));
            }
            Assert.Equal(expectedMatches, actualMatches);
            Assert.False(cursor.MoveNext());
            AssertOwned(regex);

            foreach (var count in new[] { 0, 2, 3, int.MaxValue })
            {
                var expectedSplits = new List<(string, int, int)>();
                foreach (var range in oracle.EnumerateSplits(text, count))
                {
                    var (start, length) = range.GetOffsetAndLength(text.Length);
                    expectedSplits.Add((text.Substring(start, length), start, length));
                }
                var actualSplits = new List<(string, int, int)>();
                foreach (var split in regex.EnumerateSplits(input, count))
                {
                    actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
                }
                Assert.Equal(expectedSplits, actualSplits);
                AssertOwned(regex);
            }

            foreach (var replacement in new[] { "Q", "", "\u03bb" })
            {
                var expected = oracle.Replace(text, replacement);
                var expectedBytes = Encoding.UTF8.GetBytes(expected);
                Assert.Equal(expectedBytes, regex.Replace(input, replacement));
                Assert.Equal(expectedBytes, regex.Replace(input, Encoding.UTF8.GetBytes(replacement)));
                Assert.Equal(expected, regex.ReplaceToString(input, replacement));
                var destination = new byte[expectedBytes.Length];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, replacement, destination, out var written));
                Assert.Equal(expectedBytes.Length, written);
                Assert.Equal(expectedBytes, destination);
                AssertOwned(regex);
                if (expectedBytes.Length > 0)
                {
                    var shortDestination = Enumerable.Repeat((byte)0xcc, expectedBytes.Length - 1).ToArray();
                    Assert.Equal(OperationStatus.DestinationTooSmall,
                        regex.TryReplace(input, replacement, shortDestination, out written));
                    Assert.Equal(0, written);
                    Assert.All(shortDestination, value => Assert.Equal((byte)0xcc, value));
                }
            }

            if (!finiteTimeout)
            {
                const string template = "<$&>:$`:$':$_:$$";
                Assert.Equal(oracle.Replace(text, template), regex.ReplaceToString(input, template));
                Assert.Equal(Encoding.UTF8.GetBytes(oracle.Replace(text, template)), regex.Replace(input, template));
                AssertOwned(regex);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OffsetWindowEnumerationKeepsOriginalBoundaryContext(bool compiled)
    {
        const string pattern = @"\ba.{0,4}b\b";
        const string text = "xa-b a-b a.b";
        var input = Encoding.UTF8.GetBytes(text);
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var oracle = new Regex(pattern, options);
        for (var offset = 0; offset <= text.Length; offset++)
        {
            var regex = new Utf8Regex(pattern, options);
            var expected = new List<(int, int)>();
            foreach (var match in oracle.EnumerateMatches(text, offset))
            {
                expected.Add((match.Index, match.Length));
            }
            var actual = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatchesFromUtf16Offset(input, offset))
            {
                actual.Add((match.IndexInUtf16, match.LengthInUtf16));
            }
            Assert.Equal(expected, actual);
            AssertOwned(regex);
        }
    }

    [Theory]
    [InlineData("\u03bb a  b", @"a\s{2,}b")]
    [InlineData("a\u00a0\u00a0b", @"a\s{2,}b")]
    [InlineData("a\U0001f600b", @"a.{1,1}b")]
    [InlineData("a\U0001f600b", @"a.{2,2}b")]
    public void NonAsciiWindowSubjectsKeepScalarCoordinates(string text, string pattern)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WindowCursorMapsAnExpiredDeadlineToThePublicTimeout(bool compiled)
    {
        const string pattern = @"a.{0,4}b";
        var text = new string('a', 16_384);
        var input = Encoding.UTF8.GetBytes(text);
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var timeout = TimeSpan.FromMilliseconds(1);
        var regex = new Utf8Regex(pattern, options, timeout);
        var cursor = regex.EnumerateMatches(input);
        // Expire the invocation after its creation; the candidate loop must
        // observe its own deadline, rather than relying on construction checks.
        Thread.Sleep(20);
        RegexMatchTimeoutException? observed = null;
        try
        {
            while (cursor.MoveNext()) { }
        }
        catch (RegexMatchTimeoutException exception)
        {
            observed = exception;
        }
        Assert.NotNull(observed);
        Assert.Equal(pattern, observed.Pattern);
        Assert.Equal(text, observed.Input);
        Assert.Equal(timeout, observed.MatchTimeout);
        AssertOwned(regex);
    }

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
