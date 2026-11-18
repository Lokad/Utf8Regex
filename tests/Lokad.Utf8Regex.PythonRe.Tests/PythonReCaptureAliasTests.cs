using System.Reflection;
using System.Text;
using Lokad.Utf8Regex.Benchmarks;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureAliasTests
{
    // CPython 3.13.2 facts. Subjects are ASCII here, so the reference renderer
    // also tests the same language without a UTF-16/scalar semantic difference.
    [Theory]
    [InlineData("(?P<α>a)(?P<g1>b)(?P=α)", "aba", "abb", "α", "g1")]
    [InlineData("(?P<g2>a)(?P<α>b)(?P=g2)", "aba", "abb", "g2", "α")]
    [InlineData("(?P<é>a)(?P<g1>b)(?P=é)", "aba", "abb", "é", "g1")]
    [InlineData("(?P<𐐀>a)(?P<g1>b)(?P=𐐀)", "aba", "abb", "𐐀", "g1")]
    [InlineData("(?P<α>a)(?P<g1>b)\\1", "aba", "abb", "α", "g1")]
    [InlineData("(?P<α>a)+(?P<g1>b)(?P=α)", "aaba", "aabb", "α", "g1")]
    [InlineData("(?P<α>a)?(?P<g1>b)(?(α)a|c)", "aba", "abb", "α", "g1")]
    public void NativeAliasesAndOwnedNamesPreserveDistinctCaptureIdentities(
        string pattern, string hit, string miss, string first, string second)
    {
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        var translation = PythonReTranslator.Translate(parse, true);
        var native = new Utf8Regex(translation.NativePatternUtf8, options: translation.NativeRegexOptions, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default
        });
        var input = Encoding.UTF8.GetBytes(hit);
        var absent = Encoding.UTF8.GetBytes(miss);
        Assert.True(native.IsMatch(input));
        Assert.False(native.IsMatch(absent));
        var copiedNames31 = new string[native.CaptureGroupCount + 1];
        for (var captureSlot31 = 0; captureSlot31 < copiedNames31.Length; captureSlot31++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                native.TryCopyGroupNameUtf8(captureSlot31, [], out var nameLength31));
            var nameBytes31 = new byte[nameLength31];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                native.TryCopyGroupNameUtf8(captureSlot31, nameBytes31, out var written31));
            Assert.Equal(nameLength31, written31);
            copiedNames31[captureSlot31] = System.Text.Encoding.UTF8.GetString(nameBytes31);
        }
        Assert.Equal(["0", "g1", "g2"], copiedNames31);
        var comparator = new System.Text.RegularExpressions.Regex(PythonReDotNetReference.Render(parse));
        Assert.Matches(comparator, hit);
        Assert.DoesNotMatch(comparator, miss);
        var owned = PythonReScalarCompiler.Compile(parse);
        Assert.Equal(["0", first, second], owned.GroupNames);
        foreach (var finite in new[] { false, true })
        {
            var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = finite ? new Utf8ResourcePolicy
                {
                    MaxWorkSteps = 1_000_000,
                    MaxDepth = 2048,
                    MaxWorkingBytes = 1_048_576,
                    MaxResultBytes = 1_048_576
                }

                : default
            });
            var copiedNames32 = new string[regex.CaptureGroupCount + 1];
            for (var captureSlot32 = 0; captureSlot32 < copiedNames32.Length; captureSlot32++)
            {
                Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                    regex.TryCopyGroupNameUtf8(captureSlot32, [], out var nameLength32));
                var nameBytes32 = new byte[nameLength32];
                Assert.Equal(System.Buffers.OperationStatus.Done,
                    regex.TryCopyGroupNameUtf8(captureSlot32, nameBytes32, out var written32));
                Assert.Equal(nameLength32, written32);
                copiedNames32[captureSlot32] = System.Text.Encoding.UTF8.GetString(nameBytes32);
            }
            Assert.Equal(["0", first, second], copiedNames32);
            Assert.True(regex.IsMatch(input));
            Assert.False(regex.IsMatch(absent));
            Assert.Equal(1, regex.Count(input));
            Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
            Utf8PythonRangeMatch retained = default;
            using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor1.MoveNext(out retained);
            }
            Assert.Equal(2, retained.LastIndex);
            Assert.True(retained.TryGetGroup(1, out var a));
            Assert.True(retained.TryGetGroup(2, out var b));
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString((a.Success ? retained.GetGroupMemory(a.Number).Span : ReadOnlySpan<byte>.Empty)));
            Assert.Equal("b", System.Text.Encoding.UTF8.GetString((b.Success ? retained.GetGroupMemory(b.Number).Span : ReadOnlySpan<byte>.Empty)));
            ReadOnlyMemory<byte> snapshots1Input = input;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            Assert.Equal(["a", "b"], Assert.Single(snapshots1.Select(captureSnapshot1 => Enumerable.Range(1, captureSnapshot1.Groups.Length - 1).Select(captureNumber1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureNumber1).Span)).ToArray()).ToArray()));
            Assert.Equal("a|b", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "\\g<" + first + ">|\\g<" + second + ">")));
            Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained.Groups[1].Success ? retained.GetGroupMemory(retained.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
            Assert.Equal("b", System.Text.Encoding.UTF8.GetString((retained.Groups[2].Success ? retained.GetGroupMemory(retained.Groups[2].Number).Span : ReadOnlySpan<byte>.Empty)));

        }
    }

    [Fact]
    public void MixedNumberedAndNamedReferencesKeepPythonOpeningOrder()
    {
        const string pattern = "(?P<α>a)(b)(?P<g1>c)(?P=α)\\2";
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        var translation = PythonReTranslator.Translate(parse, true);
        var native = new Utf8Regex(translation.NativePatternUtf8, options: translation.NativeRegexOptions, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default
        });
        Assert.True(native.IsMatch("abcab"u8));
        Assert.False(native.IsMatch("abccb"u8));
        var regex = new Utf8PythonRegex(pattern);
        Utf8PythonRangeMatch match = default;
        using (var captureCursor2 = regex.CreateMatchCursor(("abcab"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor2.MoveNext(out match);
        }
        Assert.True(match.Success);
        var copiedNames33 = new string[regex.CaptureGroupCount + 1];
        for (var captureSlot33 = 0; captureSlot33 < copiedNames33.Length; captureSlot33++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                regex.TryCopyGroupNameUtf8(captureSlot33, [], out var nameLength33));
            var nameBytes33 = new byte[nameLength33];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                regex.TryCopyGroupNameUtf8(captureSlot33, nameBytes33, out var written33));
            Assert.Equal(nameLength33, written33);
            copiedNames33[captureSlot33] = System.Text.Encoding.UTF8.GetString(nameBytes33);
        }
        Assert.Equal(["0", "α", "2", "g1"], copiedNames33);
        Assert.Equal(3, match.LastIndex);
        Assert.Equal(["abcab", "a", "b", "c"], PythonReCaptureAssert.Values(match).ToArray());
    }

    [Fact]
    public void DistinctNamesKeepCompleteLiteralDiscoveryEligible()
    {
        var regex = new Utf8PythonRegex("(?P<α>a)(?P<g1>b)");
        var core = Assert.IsType<Utf8Regex>(typeof(Utf8PythonRegex)
            .GetField("_utf8Regex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex));
        Assert.True(core.CanFindNativeLiteralForResult);
        var copiedNames34 = new string[regex.CaptureGroupCount + 1];
        for (var captureSlot34 = 0; captureSlot34 < copiedNames34.Length; captureSlot34++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                regex.TryCopyGroupNameUtf8(captureSlot34, [], out var nameLength34));
            var nameBytes34 = new byte[nameLength34];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                regex.TryCopyGroupNameUtf8(captureSlot34, nameBytes34, out var written34));
            Assert.Equal(nameLength34, written34);
            copiedNames34[captureSlot34] = System.Text.Encoding.UTF8.GetString(nameBytes34);
        }
        Assert.Equal(["0", "α", "g1"], copiedNames34);
        Assert.True(regex.IsMatch("é😀ab"u8));
        Utf8PythonRangeMatch match = default;
        using (var captureCursor3 = regex.CreateMatchCursor(("é😀ab"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor3.MoveNext(out match);
        }
        Assert.Equal(6, match.Value.StartOffsetInBytes);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((match.Groups[1].Success ? match.GetGroupMemory(match.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((match.Groups[2].Success ? match.GetGroupMemory(match.Groups[2].Number).Span : ReadOnlySpan<byte>.Empty)));
    }
}
