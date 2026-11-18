using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed partial class PythonReScalarFoldingTests
{
    [Theory]
    [InlineData("\u1C89", "\u1C8A")]
    [InlineData("\u0264", "\uA7CB")]
    [InlineData("\uA7CC", "\uA7CD")]
    [InlineData("\u019B", "\uA7DC")]
    [InlineData("\uA7DA", "\uA7DB")]
    public void CoreUnicode16SupportDoesNotExpandPinnedPython151Folding(string left, string right)
    {
        Assert.False(new Utf8PythonRegex("(?i:" + left + ")").IsMatch(Encoding.UTF8.GetBytes(right)));
        Assert.False(new Utf8PythonRegex("(?i:" + right + ")").IsMatch(Encoding.UTF8.GetBytes(left)));
    }

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
            Utf8PythonRangeMatch match = default;
            using (var captureCursor1 = regex.CreateMatchCursor(input, 0, source.Token, mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor1.MoveNext(out match);
            }
            Assert.True(row.Matches == match.Success,
                $"reference U+{row.Captured:X}/U+{row.Referenced:X}");
            if (!match.Success) continue;
            Assert.Equal(input.Length, match.Value.EndOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(first), match.Groups[1].EndOffsetInBytes);
            Assert.Equal(first, System.Text.Encoding.UTF8.GetString((match.Groups[1].Success ? match.GetGroupMemory(match.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        }
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
            Assert.True(literal.Match(Encoding.UTF8.GetBytes(second),mode: Utf8PythonMatchMode.FullMatch).Success,
                $"literal U+{row.Captured:X}/U+{row.Referenced:X}");
            Assert.False(negative.Match(Encoding.UTF8.GetBytes(second),mode: Utf8PythonMatchMode.FullMatch).Success);
            Assert.True(literal.Match(Encoding.UTF8.GetBytes(first),mode: Utf8PythonMatchMode.FullMatch).Success);
        }
    }

    [Theory]
    [MemberData(nameof(GlobalControls))]
    public void ScopedScalarGlobalsMatchCpython(string pattern, string subject,
        PythonReCompileOptions options, int limit, string replacement, string expectedJson)
        => PythonReScalarGlobalTests.AssertFrozenGlobalControls(pattern, subject,
            0, limit, replacement, expectedJson, options);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
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
                    case 0: regex.Match(input,0,cancellationToken: source.Token,mode: Utf8PythonMatchMode.Search); break;
                    case 1: regex.Match(input,0,cancellationToken: source.Token); break;
                    case 2: regex.Match(input,0,cancellationToken: source.Token,mode: Utf8PythonMatchMode.FullMatch); break;
                    default:
                        using (var cursor = regex.CreateMatchCursor(input, 0, source.Token,
                            mode: (Utf8PythonMatchMode)(operation - 3)))
                            cursor.MoveNext(out _);
                        break;
                }
            });
            Assert.True(entered);
            Assert.Equal(source.Token, error.CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(regex.Match("KK"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void FailedNamedScalarDiscoveryKeepsDefaultResultMetadata()
    {
        var regex = new Utf8PythonRegex("(?P<x>i)", PythonReCompileOptions.IgnoreCase);
        Utf8PythonRangeMatch search = default;
        using (var captureCursor8 = regex.CreateMatchCursor(("!"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor8.MoveNext(out search);
        }
        Assert.False(search.Success);
        Assert.Equal(0, search.Groups.Length);
        Assert.Equal(0, search.NameEntries.Length);
        Utf8PythonRangeMatch match = default;
        using (var captureCursor9 = regex.CreateMatchCursor(("!"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor9.MoveNext(out match);
        }
        Assert.False(match.Success);
        Assert.Equal(0, match.Groups.Length);
        Assert.Equal(0, match.NameEntries.Length);
        Utf8PythonRangeMatch full = default;
        using (var captureCursor10 = regex.CreateMatchCursor(("!"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor10.MoveNext(out full);
        }
        Assert.False(full.Success);
        Assert.Equal(0, full.Groups.Length);
        Assert.Equal(0, full.NameEntries.Length);
    }

    [Fact]
    public void FullMatchBacktracksAtTheOriginalByteStart()
    {
        var regex = new Utf8PythonRegex(@"(?<=x)(?i:k|kk)");
        Assert.True(regex.Match("xKK"u8,1,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("yKK"u8,1,mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch snapshot11 = default;
        using (var captureCursor11 = regex.CreateMatchCursor(("xKK"u8).ToArray(), 1, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor11.MoveNext(out snapshot11);
        }
        Assert.Equal("KK", System.Text.Encoding.UTF8.GetString(snapshot11.GetValueMemory().Span));
        Assert.False(new Utf8PythonRegex(@"(?i:^kk)").Match("xKK"u8,1,mode: Utf8PythonMatchMode.FullMatch).Success);
    }
}
