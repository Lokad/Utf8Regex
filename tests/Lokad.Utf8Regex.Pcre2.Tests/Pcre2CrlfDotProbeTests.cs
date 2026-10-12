using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CrlfDotProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return [".", "\ud83d\ude00", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return [".", "\ud83d\ude00", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return [".", "\ud83d\ude00", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return [".", "a", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return [".", "a", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return [".", "a", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return [".", "", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return [".", "\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return [".", "\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return [".", "\r\n\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 4];
        yield return [".", "\r\n\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 4];
        yield return [".", "\r\n\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 4];
        yield return [".", "\r\n\u00e9", 1, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 2];
        yield return [".", "\r\n\u00e9", 1, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 2];
        yield return [".", "\r\n\u00e9", 1, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 2];
        yield return [".", "\r\n\u00e9", 0, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "\r\n\u00e9", 0, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "\r\n\u00e9", 0, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".", "\r", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return [".", "\r", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return [".", "\r", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["\\N", "\r", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["\\N", "\r", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["\\N", "\r", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["\\N", "\r\n\u00e9", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 4];
        yield return ["\\N", "\r\n\u00e9", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 4];
        yield return ["\\N", "\r\n\u00e9", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 4];
        yield return [".{2,3}", "\ud83d\ude00", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".{2,3}", "\ud83d\ude00", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return [".{2,3}", "\ud83d\ude00", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 4];
        yield return [".{2,3}", "\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return [".{2,3}", "\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return [".{2,3}", "\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 6];
        yield return [".{2,3}", "\ud83d\ude00\u00e9x", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return [".{2,3}", "\ud83d\ude00\u00e9x", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return [".{2,3}", "\ud83d\ude00\u00e9x", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return [".{2,3}", "ab", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}", "ab", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}", "ab", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return [".{2,3}", "abc", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return [".{2,3}", "abc", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return [".{2,3}", "abc", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return [".{2,3}", "a\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}", "a\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}", "a\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return [".{2,3}", "ab\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return [".{2,3}", "ab\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return [".{2,3}", "ab\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 3];
        yield return [".{2,3}", "a\r\nbc", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 5];
        yield return [".{2,3}", "a\r\nbc", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 5];
        yield return [".{2,3}", "a\r\nbc", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 3, 5];
        yield return [".{2,3}", "\r\n\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 8];
        yield return [".{2,3}", "\r\n\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 8];
        yield return [".{2,3}", "\r\n\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 2, 8];
        yield return [".{2,3}", "\r\n\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".{2,3}", "\r\n\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".{2,3}", "\r\n\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return [".{2,3}?", "\ud83d\ude00\u00e9x", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return [".{2,3}?", "\ud83d\ude00\u00e9x", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return [".{2,3}?", "\ud83d\ude00\u00e9x", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return [".{2,3}?", "\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return [".{2,3}?", "\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return [".{2,3}?", "\ud83d\ude00\u00e9", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 6];
        yield return [".{2,3}?", "ab\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}?", "ab\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}?", "ab\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}?", "a\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}?", "a\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}?", "a\r", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return [".{2,3}?", "a\r\nbc", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 3, 5];
        yield return [".{2,3}?", "a\r\nbc", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 3, 5];
        yield return [".{2,3}?", "a\r\nbc", 0, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 3, 5];
        yield return [".{2,3}", "\ud83d\ude00x\ud83d\ude00\u00e9", 4, false, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return [".{2,3}", "\ud83d\ude00x\ud83d\ude00\u00e9", 4, false, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return [".{2,3}", "\ud83d\ude00x\ud83d\ude00\u00e9", 4, false, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 4, 11];
        yield return [".{2,3}", "\ud83d\ude00x\ud83d\ude00\u00e9", 5, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 5, 11];
        yield return [".{2,3}", "\ud83d\ude00x\ud83d\ude00\u00e9", 5, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 5, 11];
        yield return [".{2,3}", "\ud83d\ude00x\ud83d\ude00\u00e9", 5, false, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 5, 11];
        yield return [".{2,3}?", "x\ud83d\ude00\u00e9y", 1, false, Pcre2MatchOptions.NotEmpty | Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 1, 7];
        yield return [".{2,3}?", "x\ud83d\ude00\u00e9y", 1, false, Pcre2MatchOptions.NotEmpty | Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 1, 7];
        yield return [".{2,3}?", "x\ud83d\ude00\u00e9y", 1, false, Pcre2MatchOptions.NotEmpty | Pcre2MatchOptions.NotBol | Pcre2MatchOptions.NotEol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 1, 7];
        yield return [".{2,3}", "a\r\nb", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return [".{2,3}", "a\r\nb", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return [".{2,3}", "a\r\nb", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return [".{2,3}?", "a\r\nb", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}?", "a\r\nb", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return [".{2,3}?", "a\r\nb", 0, true, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void CrlfDotPreservesNativeScalarEndpointAndRepeatPreference(string pattern, string text, int start,
        bool dotAll, Pcre2MatchOptions flags, Pcre2PartialMode mode, Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, dotAll ? Pcre2CompileOptions.DotAll : Pcre2CompileOptions.None,
            new() { Newline = Pcre2NewlineConvention.Crlf }, default, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind == Utf8Pcre2ProbeKind.FullMatch)
        {
            var match = probe.GetMatch();
            Assert.Equal(1, match.CaptureSlotCount);
            Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(begin, end - begin)), match.GetValueString());
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InlineDotAllUsesAdmittedTokenOptions(bool dotAll)
    {
        var regex = new Utf8Pcre2Regex(dotAll ? "(?s:.{2,3})" : "(?-s:.{2,3})", Pcre2CompileOptions.DotAll,
            new() { Newline = Pcre2NewlineConvention.Crlf }, default, Timeout.InfiniteTimeSpan);
        var probe = regex.Probe("a\r\nb"u8, Pcre2PartialMode.Soft);
        Assert.Equal(dotAll ? Utf8Pcre2ProbeKind.FullMatch : Utf8Pcre2ProbeKind.PartialMatch, probe.Kind);
        Assert.Equal(dotAll ? (0, 3) : (3, 4), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UngreedyOptionPreservesAdmittedRepeatPreference(bool explicitLazy)
    {
        var regex = new Utf8Pcre2Regex(explicitLazy ? ".{2,3}?" : ".{2,3}", Pcre2CompileOptions.Ungreedy,
            new() { Newline = Pcre2NewlineConvention.Crlf }, default, Timeout.InfiniteTimeSpan);
        var probe = regex.Probe("😀éx"u8, Pcre2PartialMode.Hard);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        Assert.Equal((0, explicitLazy ? 7 : 6), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
    }

    [Fact]
    public void FiniteFullPublicationKeepsExactScalarRangeAndRetainedAlias()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create(".{2,3}?", Pcre2CompileOptions.None,
            new() { Newline = Pcre2NewlineConvention.Crlf },
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("\r\n😀éx"u8, Pcre2PartialMode.Hard); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("\r\n😀éx"u8, Pcre2PartialMode.Hard).GetMatch();
        var alias = retained.GetGroup(0);
        Assert.Equal((2, 8), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal("😀é", retained.GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("\r\n"u8, Pcre2PartialMode.Hard).Kind);
        Assert.Equal("😀é", alias.GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("\r\n😀éx"u8, Pcre2PartialMode.Hard).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("😀"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompiledAnchorRetainsOriginalCrlfCandidate(bool lazy)
    {
        var regex = new Utf8Pcre2Regex(lazy ? ".{2,3}?" : ".{2,3}", Pcre2CompileOptions.Anchored,
            new() { Newline = Pcre2NewlineConvention.Crlf }, default, Timeout.InfiniteTimeSpan);
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("\r\n😀é"u8, Pcre2PartialMode.Hard).Kind);
        var probe = regex.Probe("\r\n😀é"u8, Pcre2PartialMode.Hard, 2);
        Assert.Equal(lazy ? Utf8Pcre2ProbeKind.FullMatch : Utf8Pcre2ProbeKind.PartialMatch, probe.Kind);
        Assert.Equal((2, 8), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
    }

    [Fact]
    public void CrlfCandidateWorkCannotBypassFiniteAllowance()
    {
        var regex = new Utf8Pcre2Regex(".{2,3}", Pcre2CompileOptions.None,
            new() { Newline = Pcre2NewlineConvention.Crlf }, new() { MatchLimit = 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\r\n", 4096)));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Hard); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("abc"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CrlfCandidateSearchObservesOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(".{2,3}", Pcre2CompileOptions.None,
            new() { Newline = Pcre2NewlineConvention.Crlf }, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 10) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n"u8, Pcre2PartialMode.Hard, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }
}
