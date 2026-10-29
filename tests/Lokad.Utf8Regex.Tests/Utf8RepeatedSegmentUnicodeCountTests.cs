using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RepeatedSegmentUnicodeCountTests
{
    public static IEnumerable<object[]> Separators()
    {
        foreach (var value in new[] { 9, 10, 11, 12, 13, 32, 0x85, 0xa0, 0x1680,
            0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007,
            0x2008, 0x2009, 0x200a, 0x2028, 0x2029, 0x202f, 0x205f, 0x3000,
            0x200b, 0xfeff, 0x1f600 }) yield return [char.ConvertFromUtf32(value)];
    }

    [Theory]
    [MemberData(nameof(Separators))]
    public void CountPreservesUnicodeAndAsciiSeparatorMembership(string separator)
    {
        foreach (var pattern in new[] { @"(?:[A-Z][a-z]+\s*){2,4}",
            @"(?:[A-Z][a-z]+\s{2,}){2,4}", @"(?:[A-Z][a-z]+[ \t\r\n\v\f]*){2,4}" })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var text = "😀Aa" + separator + separator + "Bb Cc Dd Ee Ff Gg Hh I!Aa BbC Dd Ee";
            var bytes = Encoding.UTF8.GetBytes(text);
            byte[] storage = [0xff, .. bytes, 0xff];
            var regex = new Utf8Regex(pattern, options); var oracle = new Regex(pattern, options);
            Assert.Equal(NativeExecutionKind.AsciiStructuralRepeatedSegment, regex.Inspection.PreparedRegex.ExecutionKind);
            Assert.Equal(pattern.Contains(@"\s"), regex.Inspection.PreparedRegex.StructuralRepeatedSegmentPlan.HasUnicodeSeparator);
            var diagnostics = Utf8SearchDiagnosticsSession.Start();
            try { Assert.Equal(oracle.Count(text), regex.Count(storage.AsSpan(1, bytes.Length))); }
            finally { diagnostics.Complete(); }
            Assert.Equal(Utf8ExecutionRoute.NativeStructuralLinearAutomaton, diagnostics.ExecutionRoute);
            // Count admission must not allow byte coordinates to become UTF-16 coordinates.
            var expected = oracle.Match(text); var actual = regex.Match(bytes);
            Assert.Equal(expected.Success, actual.Success);
            if (actual.Success)
            {
                Assert.Equal(expected.Index, actual.IndexInUtf16);
                Assert.Equal(expected.Length, actual.LengthInUtf16);
                Assert.Equal(expected.Value, regex.MatchDetailed(bytes).GetValueString());
            }
        }
    }

    [Fact]
    public void CandidateMasksPreserveGapsAndWordOrdering()
    {
        foreach (var (pattern, text) in new[] {
            (@"(?:[1-9][A-Z]+\s*){2,3}", "😀1AB\u00a02CD 3EF 4GH!"),
            (@"(?:[AZ][a-z]+\s*){2,3}", "😀Bb Aa\u2028Zz Cc Aa Zz"),
            (@"(?:[\x00-\x08][a-z]+\s*){2,3}", "😀\0ab\u2003\u0001cd \u0007ef"),
            (@"(?:[\x7a-\x7f][a-c]+\s*){2,3}", "😀zab\u2003\u007fcc xab zac") })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options); var oracle = new Regex(pattern, options);
            Assert.Equal(NativeExecutionKind.AsciiStructuralRepeatedSegment, regex.Inspection.PreparedRegex.ExecutionKind);
            Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Fact]
    public void ControlledRepeatedCountRetainsOriginalScalarInvocation()
    {
        const string pattern = @"(?:[A-Z][a-z]+\s*){2,4}";
        var text = "😀" + string.Concat(Enumerable.Repeat("Aa\u2003Bb Cc Dd ", 512));
        var bytes = Encoding.UTF8.GetBytes(text); var expected = new Regex(pattern).Count(text);
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var funded = new Utf8Regex(pattern, options, Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy
            { MaxWorkSteps = 1_000_000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
            Assert.Equal(new Regex(pattern, options).Count(text), funded.Count(bytes, token.Token));
        }
        var denied = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(bytes));
        var regex = new Utf8Regex(pattern);
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ => token.Cancel());
        try { Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, token.Token)).CancellationToken); }
        finally { diagnostics.Complete(); }
        Assert.NotEqual(Utf8ExecutionRoute.NativeStructuralLinearAutomaton, diagnostics.ExecutionRoute);
        byte[] invalid = [.. bytes, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(invalid));
        Assert.Equal(expected, regex.Count(bytes));
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(invalid, token.Token)).CancellationToken);
    }
}
