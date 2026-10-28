using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuralBoundaryOverlapTests
{
    private const string Declaration = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";

    [Theory]
    [InlineData(Declaration, "class Alpha struct Beta record Gamma", false)]
    [InlineData(@"\b(?:ab|ba)\s+[A-Z][A-Za-z0-9_]+", "aba Wrong ab Good ba Right", false)]
    [InlineData(@"\b(?:foobar|bar)\s+[A-Z][A-Za-z0-9_]+", "foobar Good bar Right", false)]
    [InlineData(@"\b(?:foo|foobar)[A-Z][A-Za-z0-9_]*", "foobarAlpha fooBeta", true)]
    [InlineData(@"\b(?:foo:|:bar)[A-Z][A-Za-z0-9_]*", "foo:barAlpha foo:Beta", true)]
    [InlineData(@"(?:class|struct)\s+[A-Z][A-Za-z0-9_]+", "classstruct Good class Right", true)]
    public void BoundaryProofDistinguishesUnresolvedPrefixRetries(string pattern, string text, bool needsRetry)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(needsRetry, regex.Inspection.StructuralIdentifierFamilyPlan.HasUnresolvedPrefixOverlap);
            Assert.Equal(new Regex(pattern).Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Theory]
    [InlineData("class Alpha struct Beta record Gamma")]
    [InlineData("classstruct Wrong class lower struct Z record A_ class A1")]
    [InlineData("class\t\r\nAlpha struct Beta record Gamma")]
    [InlineData("class" + "                                                                " + "Alpha")]
    [InlineData("éclass Wrong 😀class\u2003Right struct lower record Good")]
    [InlineData("class Alphaé struct Beta中 record Gamma😀")]
    [InlineData("")]
    [InlineData("class A struct Z record a")]
    public void PublicOperationsPreserveRangesAndControlledRoutes(string text)
    {
        var expected = new Regex(Declaration).Matches(text);
        var buffer = Encoding.UTF8.GetBytes("pad" + text + "tail");
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        foreach (var controlled in new[] { false, true })
        {
            var regex = new Utf8Regex(Declaration, options, Regex.InfiniteMatchTimeout, controlled
                ? new() { MaxWorkSteps = 1000000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 }
                : default);
            var input = buffer.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            Assert.Equal(expected.Count, regex.Count(input));
            Assert.Equal(expected.Count, regex.Count(input, cancellation.Token));
            Assert.Equal(expected.Count != 0, regex.IsMatch(input));
            Assert.Equal(expected.Count != 0, regex.IsMatch(input, cancellation.Token));
            var matches = new Regex(Declaration, options).Matches(text);
            var first = regex.Match(input);
            Assert.Equal(matches.Count != 0, first.Success);
            var cursor = regex.EnumerateMatches(input);
            var index = 0;
            while (cursor.MoveNext())
            {
                var match = matches[index++];
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index)), cursor.Current.IndexInBytes);
                Assert.Equal(Encoding.UTF8.GetByteCount(match.Value), cursor.Current.LengthInBytes);
            }
            Assert.Equal(matches.Count, index);
            if (matches.Count != 0)
            {
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, matches[0].Index)), first.IndexInBytes);
                Assert.Equal(Encoding.UTF8.GetByteCount(matches[0].Value), first.LengthInBytes);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AsciiCountUsesTheExistingStructuralRoute(bool compiled)
    {
        var regex = new Utf8Regex(Declaration, compiled ? RegexOptions.Compiled : RegexOptions.None);
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.Equal(3, regex.Count("class Alpha struct Beta record Gamma"u8));
            Assert.Equal(RuntimeFeature.IsDynamicCodeSupported
                ? Utf8ExecutionRoute.NativeStructuralFamilyEmitUpperWordIdentifier
                : Utf8ExecutionRoute.NativeStructuralFamilyLinear, session.ExecutionRoute);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(@"\b(?:call|tell)\s+[A-Z][A-Za-z0-9_]+", "call A tell Bee call Z")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]{0,2}", "class ABCDE struct Z record A1")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]{2,3}", "class A struct B2 record AbcdE")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]{0,2}\b", "class ABCDE struct Z record A1")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]*", "class Alpha struct B record C")]
    [InlineData(@"\b(?:record|struct|class)[ \t]+[A-Z][A-Za-z0-9_]*", "class\nWrong struct\tGood record Right")]
    public void NativeIdentifierVerificationPreservesTailBounds(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Regex(pattern).Matches(text);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(expected.Count, regex.Count(input));
            Assert.Equal(expected.Count != 0, regex.IsMatch(input));
            var first = regex.Match(input);
            if (expected.Count != 0)
            {
                Assert.Equal(expected[0].Index, first.IndexInBytes);
                Assert.Equal(expected[0].Length, first.LengthInBytes);
            }
            var cursor = regex.EnumerateMatches(input);
            var index = 0;
            while (cursor.MoveNext())
            {
                var match = expected[index++];
                Assert.Equal(match.Index, cursor.Current.IndexInBytes);
                Assert.Equal(match.Length, cursor.Current.LengthInBytes);
            }
            Assert.Equal(expected.Count, index);
        }
    }

    [Fact]
    public void MalformedTailCannotBeHiddenByNativeHitsOrMisses()
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        foreach (var text in new[] { "class Alpha", "no declarations" })
        {
            var regex = new Utf8Regex(Declaration, options);
            var malformed = Encoding.UTF8.GetBytes(text).Append((byte)0xff).ToArray();
            Assert.Throws<ArgumentException>(() => regex.Count(malformed));
            Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
            Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        }
    }
}
