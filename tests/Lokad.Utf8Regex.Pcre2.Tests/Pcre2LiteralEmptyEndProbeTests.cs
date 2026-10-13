using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2LiteralEmptyEndProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["\\z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["\\z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 4];
        yield return ["\\z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 4];
        yield return ["\\z", "\ud83d\ude00", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\A\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\A\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\A\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\A\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\A\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\G\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\G\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\G\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 1, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.Anchored, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "\ud83d\ude00", 4, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "\ud83d\ude00", 4, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["\\z", "\ud83d\ude00", 4, Pcre2CompileOptions.FirstLine, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 4, 4];
        yield return ["", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["", "\ud83d\ude00", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", "\ud83d\ude00", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", "\ud83d\ude00", 4, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\A", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\G", "x", 1, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmptyAtStart, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.NotEmpty, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 0];
        yield return ["\\z", "", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 0];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 1];
        yield return ["\\z", "x", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 1, 1];
        yield return ["abc\\z", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\z", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["abc\\z", "abc", 0, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void EmptyLiteralEndProbesPreserveNativeFlagsAndAnchors(string pattern, string text, int start,
        Pcre2CompileOptions options, Pcre2MatchOptions flags, Pcre2PartialMode mode,
        Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind != Utf8Pcre2ProbeKind.FullMatch) return;
        var match = probe.GetMatch();
        Assert.Equal(1, match.CaptureSlotCount);
        Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(begin, end - begin)), match.GetValueString());
    }

    [Fact]
    public void EmptyEndHasExactResultsAndRetainedScalarEofAliases()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create(@"\z", Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀"u8, Pcre2PartialMode.Soft).GetMatch();
        var alias = retained.GetGroup(0);
        Assert.Equal((4, 4), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("x"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal((4, 4), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal("", retained.GetValueString());
        var partial = Create(1).Probe("😀"u8, Pcre2PartialMode.Soft, 4, Pcre2MatchOptions.NotEmptyAtStart);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, partial.Kind);
        Assert.Equal((4, 4), (partial.Value.StartOffsetInBytes, partial.Value.EndOffsetInBytes));
    }

    [Fact]
    public void EmptyEndKeepsOriginalWorkAndWorkingAllowance()
    {
        Utf8Pcre2Regex Create(uint work, ulong heap) => new(@"\z", Pcre2CompileOptions.None, default,
            new() { MatchLimit = work, HeapLimitInBytes = heap }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(new string('é', 100));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(1, 1 << 20).Probe(input, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.NotEmpty); }).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(100000, 1).Probe("x"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(100000, 1 << 20).Probe("x"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.NotEmpty).Kind);
    }

    [Fact]
    public void AbsoluteStartRejectsEmptyFullAtRequestedStartAcrossPublicMatching()
    {
        var regex = new Utf8Pcre2Regex(@"\A");
        Assert.False(regex.IsMatch("x"u8, 0, Pcre2MatchOptions.NotEmptyAtStart));
        Assert.Equal(0, regex.MatchDetailed("x"u8, 0, Pcre2MatchOptions.NotEmptyAtStart).CaptureSlotCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EmptyEndKeepsOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"\z", Pcre2CompileOptions.None, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 4) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("x"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.NotEmpty, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

}
