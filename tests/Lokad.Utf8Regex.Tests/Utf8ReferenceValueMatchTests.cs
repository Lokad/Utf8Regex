using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ReferenceValueMatchTests
{
    private const string Cell = @"^(?<col>[a-z])(?<row>(\d)+)$";
    private const string Range = @"^(?<col1>[a-z])(?<row1>(\d)+):?(?<col2>[a-z])(?<row2>(\d)+)$";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000,
        MaxDepth = 8192, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteAsciiProofReusesTheBoundedOriginalInvocation(bool range)
    {
        var pattern = range ? Range : Cell;
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = mode | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan,
                Funded with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            Assert.Equal(range ? Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole :
                Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole, regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
            var oracle = new Regex(pattern, options);
            foreach (var text in new[] { "", "a", "a1", "A123", "I14", "K1", "a1\n", "a1\r\n", "a1\n\n",
                "a1b2", "A123:B456", "I14k1", "a1:b2\n", "a1:b2\r\n", "a1::b2", "aa1", "a1!", " a1", "a1\0" })
            {
                var bytes = Encoding.ASCII.GetBytes(text); byte[] storage = [0xff, .. bytes, 0xff];
                var expected = oracle.Match(text); var diagnostics = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    var actual = regex.Match(storage.AsSpan(1, bytes.Length), token.Token);
                    Assert.Equal((expected.Success, expected.Length), (actual.Success, actual.LengthInUtf16));
                    Assert.Equal(Utf8ExecutionRoute.NativeAsciiAnchoredValidator, diagnostics.ExecutionRoute);
                }
                finally { diagnostics.Complete(); }
            }
        }
    }

    [Fact]
    public void ConstructionCultureOwnsTheAsciiProofAndWiderDigitsStayScalar()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "en-US", "tr-TR", "az-Latn-AZ" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                foreach (var pattern in new[] { Cell, Range })
                {
                    var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase);
                    var oracle = new Regex(pattern, RegexOptions.IgnoreCase);
                    if (name != "en-US") Assert.Equal(Utf8FallbackDirectFamilyKind.None,
                        regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    foreach (var text in new[] { "I1", "i1", "K1", "K1", "İ1", "ı1", "a١٢", "I1:b2", "a١:b٢" })
                    {
                        var expected = oracle.Match(text); var actual = regex.Match(Encoding.UTF8.GetBytes(text));
                        Assert.Equal((expected.Success, expected.Length), (actual.Success, actual.LengthInUtf16));
                    }
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                }
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }
        var supplementary = new Utf8Regex(Cell, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Match("a𝟙"u8);
        Assert.True(supplementary.Success);
        Assert.Equal((5, 3), (supplementary.LengthInBytes, supplementary.LengthInUtf16));
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void WholeSubjectBoundKeepsLongInputsOnTheScalarProgram(int length)
    {
        foreach (var pattern in new[] { Cell, Range })
        {
            var text = pattern == Cell ? "a" + new string('1', length - 1) : "a1:b" + new string('2', length - 4);
            var bytes = Encoding.ASCII.GetBytes(text);
            var tight = new Utf8Regex(pattern, RegexOptions.IgnoreCase, Timeout.InfiniteTimeSpan,
                Funded with { MaxWorkingBytes = 1, MaxResultBytes = 1 });
            if (length <= 2048) Assert.Equal(length, tight.Match(bytes).LengthInBytes);
            else Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => tight.Match(bytes)).ResourceKind);
        }
    }

    [Fact]
    public void DetailedDigitHistoriesAliasesDeclinesAndStrictTailsRemainOwned()
    {
        var regex = new Utf8Regex(Cell, RegexOptions.IgnoreCase);
        var bytes = "a123"u8.ToArray(); var value = regex.Match(bytes); var detailed = regex.MatchDetailed(bytes);
        Assert.Equal("123", detailed.GetGroup("row").GetValueString());
        Assert.Equal(3, detailed.GetGroup(1).CaptureCount);
        Assert.Equal("2", detailed.GetGroup(1).GetCapture(1).GetValueString());
        var cursor = regex.EnumerateMatches(bytes); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        _ = regex.Match("b456"u8); Assert.Equal("123", detailed.GetGroup("row").GetValueString());
        Assert.False(regex.MatchFromUtf16Offset(bytes, 1).Success);
        foreach (var mode in new[] { RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var subject = "a123"; var expected = new Regex(Cell, mode | RegexOptions.IgnoreCase).Match(subject);
            var actual = new Utf8Regex(Cell, mode | RegexOptions.IgnoreCase).Match(bytes);
            Assert.Equal((expected.Success, expected.Length), (actual.Success, actual.LengthInUtf16));
        }
        byte[] malformed = [.. bytes, 0xff]; Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(malformed, token.Token)).CancellationToken);
        var denied = new Utf8Regex(Cell, RegexOptions.IgnoreCase, Timeout.InfiniteTimeSpan, Funded with { MaxWorkSteps = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Match(bytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationKeepsTheOriginalTokenAndDeadline(bool expire)
    {
        var reads = 0; var trigger = int.MaxValue; long ticks = 0;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == trigger) { if (expire) ticks = 100; else token.Cancel(); }
            return ticks;
        });
        var regex = new Utf8Regex(Range, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50), Funded);
        Assert.True(regex.Match("a123:b456"u8, token.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("a123:b456"u8, token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("a123:b456"u8, token.Token)).CancellationToken);
    }
}
