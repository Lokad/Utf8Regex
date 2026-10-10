using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ResidualScalarRouteTests
{
    [Theory]
    [InlineData("(?m:\\Aab)", "x\nab", -1, 0)]
    [InlineData("(?m:\\Aab)", "ab", 0, 2)]
    [InlineData("(?m:a)$", "x\na\n", 2, 1)]
    [InlineData("^ab$", "ab\n", 0, 2)]
    public void AnchorsAndFinalNewlineStayOwned(string pattern, string text, int index, int length)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        foreach (var timeout in new[] { Regex.InfiniteMatchTimeout, TimeSpan.FromSeconds(3) })
        {
            var regex = new Utf8Regex(pattern, Options(compiled), timeout);
            Assert.Equal(index >= 0, regex.IsMatch(input));
            Assert.Equal(index >= 0 ? 1 : 0, regex.Count(input));
            var match = regex.Match(input);
            Assert.Equal(index >= 0, match.Success);
            if (match.Success)
                Assert.Equal((index, length), (match.IndexInBytes, match.LengthInBytes));
            var ranges = new List<(int, int)>();
            foreach (var found in regex.EnumerateMatches(input))
                ranges.Add((found.IndexInBytes, found.LengthInBytes));
            Assert.Equal(index >= 0 ? new[] { (index, length) } : Array.Empty<(int, int)>(), ranges);
            // Adapter callers can reach the compiled router directly.
            Assert.Equal(index >= 0, regex.Inspection.DebugIsMatchViaCompiledEngine(input, Utf8Validation.Validate(input)));
            Assert.Equal(index >= 0 ? 1 : 0, regex.Inspection.DebugCountViaCompiledEngine(input));
            AssertOwned(regex);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrderedWindowsMeasureCompleteScalarsInPreparedAndPublicCount(bool compiled)
    {
        const string pattern = "Holmes.{0,25}Watson|Watson.{0,25}Holmes";
        foreach (var gap in new[] { 25, 26 })
        foreach (var reverse in new[] { false, true })
        {
            var text = (reverse ? "Watson" : "Holmes") +
                string.Concat(Enumerable.Repeat("\U0001f600", gap)) + (reverse ? "Holmes" : "Watson");
            var bytes = Encoding.UTF8.GetBytes(text);
            var regex = new Utf8Regex(pattern, Options(compiled));
            var expected = gap == 25 ? 1 : 0;
            Assert.Equal(expected, regex.Count(bytes));
            Assert.Equal(expected, regex.Inspection.DebugCountViaCompiledEngine(bytes));
            var input = Utf8ValidatedInput.Create(bytes);
            Assert.Equal(expected, regex.ByteOffsetExecution.CountPrepared(input, input.GetBytePosition(0, "start")));
            var match = regex.Match(bytes);
            Assert.Equal(expected != 0, match.Success);
            if (match.Success) Assert.Equal((0, bytes.Length), (match.IndexInBytes, match.LengthInBytes));
            AssertOwned(regex);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StructuralLiteralReplacementsUseScalarCursorAcrossOutputForms(bool compiled, bool repeated)
    {
        var pattern = repeated ? "(?:[A-Z][a-z]+\\s*){10,100}" :
            "[A-Za-z]{10}\\s+[\\s\\S]{0,100}Result[\\s\\S]{0,100}\\s+[A-Za-z]{10}";
        var body = repeated ? string.Concat(Enumerable.Repeat("Abc ", 10)) : "ABCDEFGHIJ \U0001f600Result\U0001f600 KLMNOPQRST";
        var text = "\U0001f600 " + body + "\U0001f600";
        var input = Encoding.UTF8.GetBytes(text);
        var expected = "\U0001f600 Q\U0001f600";
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        foreach (var liveToken in new[] { false, true })
        {
            using var source = new CancellationTokenSource();
            var token = liveToken ? source.Token : CancellationToken.None;
            var regex = new Utf8Regex(pattern, Options(compiled));
            Assert.Equal(expectedBytes, regex.Replace(input, "Q", token));
            Assert.Equal(expectedBytes, regex.Replace(input, "Q"u8, token));
            Assert.Equal(expected, regex.ReplaceToString(input, "Q", token));
            var destination = new byte[expectedBytes.Length];
            Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "Q", destination, out var written, token));
            Assert.Equal(expectedBytes.Length, written);
            Assert.Equal(expectedBytes, destination);
            Assert.Equal(input, regex.Replace(input, "$&", token));
            AssertOwned(regex);
        }
    }

    [Theory]
    [InlineData(",")]
    [InlineData("a+")]
    [InlineData("(?<=a),")]
    public void InvalidSplitCountFailsWithoutConstructingComparator(string pattern)
    {
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex(pattern, Options(compiled));
            var input = "a,\U0001f600,b"u8.ToArray();
            Assert.Equal("count", Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                _ = regex.EnumerateSplits(input, -1);
            }).ParamName);
            using var source = new CancellationTokenSource();
            Assert.Equal("count", Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                _ = regex.EnumerateSplits(input, -1, source.Token);
            }).ParamName);
            source.Cancel();
            Assert.Equal(source.Token, Assert.ThrowsAny<OperationCanceledException>(() =>
            {
                _ = regex.EnumerateSplits(input, -1, source.Token);
            }).CancellationToken);
            AssertOwned(regex);
        }
    }

    private static RegexOptions Options(bool compiled) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : RegexOptions.None);

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
