using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed partial class PythonReScalarFoldingTests
{
    public static IEnumerable<object[]> ReferenceBlocks()
    {
        for (var start = 0; start < ReferenceControls.Length; start += 128)
            yield return [start];
    }

    [Theory]
    [MemberData(nameof(ReferenceBlocks))]
    public void EveryChangedScalarAndExtraLiteralPartnerUsesCpythonReferenceLower(int start)
    {
        var regex = new Utf8PythonRegex(@"(?s:(.))(?i:\1)");
        using var source = new CancellationTokenSource();
        foreach (var row in ReferenceControls.Skip(start).Take(128))
        {
            var first = char.ConvertFromUtf32(row.Captured);
            var second = char.ConvertFromUtf32(row.Referenced);
            var input = Encoding.UTF8.GetBytes(first + second);
            var match = regex.FullMatchDetailedData(input, 0, source.Token);
            Assert.True(row.Matches == match.Success,
                $"reference U+{row.Captured:X}/U+{row.Referenced:X}");
            if (!match.Success) continue;
            Assert.Equal(input.Length, match.Value.EndOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(first), match.Groups[1].EndOffsetInBytes);
            Assert.Equal(first, match.Groups[1].ValueText);
        }
        Assert.False(regex.DebugIsManagedRegexValueCreated);
        Assert.False(regex.DebugIsManagedFullRegexValueCreated);
    }

    [Theory]
    [MemberData(nameof(ReferenceBlocks))]
    public void LiteralAndNegatedClassPartnersStayScalarExact(int start)
    {
        foreach (var row in ReferenceControls.Skip(start).Take(128))
        {
            var first = char.ConvertFromUtf32(row.Captured);
            var second = char.ConvertFromUtf32(row.Referenced);
            var scalarEscape = $"\\U{row.Captured:X8}";
            var literal = new Utf8PythonRegex(scalarEscape, PythonReCompileOptions.IgnoreCase);
            var negative = new Utf8PythonRegex("[^" + scalarEscape + "]", PythonReCompileOptions.IgnoreCase);
            Assert.True(literal.FullMatch(Encoding.UTF8.GetBytes(second)).Success,
                $"literal U+{row.Captured:X}/U+{row.Referenced:X}");
            Assert.False(negative.FullMatch(Encoding.UTF8.GetBytes(second)).Success);
            Assert.True(literal.FullMatch(Encoding.UTF8.GetBytes(first)).Success);
            Assert.False(literal.DebugIsManagedRegexValueCreated);
            Assert.False(negative.DebugIsManagedFullRegexValueCreated);
        }
    }

    [Theory]
    [MemberData(nameof(GlobalControls))]
    public void ScopedScalarGlobalsMatchCpython(string pattern, string subject,
        PythonReCompileOptions options, int limit, string replacement, string expectedJson)
        => PythonReScalarGlobalTests.AssertFrozenGlobalControls(pattern, subject,
            0, limit, replacement, expectedJson, options);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void OneShotScalarDiscoveryKeepsCancellationAndRecovers(int operation)
    {
        var regex = new Utf8PythonRegex(@"(?i:(.)\1+)");
        var input = Encoding.UTF8.GetBytes(new string('K', 65536));
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            entered = true;
            source.Cancel();
            if (steps >= 128) throw new InvalidOperationException("Scalar one-shot operation dropped its token.");
        });
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() =>
            {
                switch (operation)
                {
                    case 0: regex.Search(input, 0, source.Token); break;
                    case 1: regex.Match(input, 0, source.Token); break;
                    case 2: regex.FullMatch(input, 0, source.Token); break;
                    case 3: regex.SearchDetailed(input, 0, source.Token); break;
                    case 4: regex.MatchDetailed(input, 0, source.Token); break;
                    case 5: regex.FullMatchDetailed(input, 0, source.Token); break;
                    case 6: regex.SearchDetailedData(input, 0, source.Token); break;
                    case 7: regex.MatchDetailedData(input, 0, source.Token); break;
                    default: regex.FullMatchDetailedData(input, 0, source.Token); break;
                }
            });
            Assert.True(entered);
            Assert.Equal(source.Token, error.CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(regex.FullMatch("KK"u8).Success);
        Assert.False(regex.DebugIsManagedRegexValueCreated);
    }

    [Fact]
    public void FailedNamedScalarDiscoveryKeepsDefaultResultMetadata()
    {
        var regex = new Utf8PythonRegex("(?P<x>i)", PythonReCompileOptions.IgnoreCase);
        var search = regex.SearchDetailed("!"u8);
        Assert.False(search.Success);
        Assert.Equal(0, search.CaptureSlotCount);
        Assert.Equal(0, search.NameEntryCount);
        var match = regex.MatchDetailed("!"u8);
        Assert.False(match.Success);
        Assert.Equal(0, match.CaptureSlotCount);
        Assert.Equal(0, match.NameEntryCount);
        var full = regex.FullMatchDetailed("!"u8);
        Assert.False(full.Success);
        Assert.Equal(0, full.CaptureSlotCount);
        Assert.Equal(0, full.NameEntryCount);
    }

    [Fact]
    public void FullMatchBacktracksAtTheOriginalByteStart()
    {
        var regex = new Utf8PythonRegex(@"(?<=x)(?i:k|kk)");
        Assert.True(regex.FullMatch("xKK"u8, 1).Success);
        Assert.False(regex.FullMatch("yKK"u8, 1).Success);
        Assert.Equal("KK", regex.FullMatchDetailedData("xKK"u8, 1).Value.ValueText);
        Assert.False(new Utf8PythonRegex(@"(?i:^kk)").FullMatch("xKK"u8, 1).Success);
    }
}
