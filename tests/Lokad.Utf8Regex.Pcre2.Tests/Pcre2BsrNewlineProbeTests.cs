using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2BsrNewlineProbeTests
{
    // Frozen PCRE2 10.47 / private PCRE.NET 1.5.0 outcomes; no native test dependency.
    public static IEnumerable<object[]> NativeCases()
    {
        yield return ["^\\R", "\u2028", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^\\R", "\u2028", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^\\R", "\u2028", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 3];
        yield return ["^\\R", "\u0085", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^\\R", "\u0085", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^\\R", "\u0085", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^\\R", "\u000b", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\u000b", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\u000b", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\u2028", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "\u2028", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "\u2028", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "\r", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\r", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\r", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^\\R", "\r\n", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^\\R", "\r\n", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^\\R", "\r\n", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 2];
        yield return ["^\\R", "\n", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\n", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\n", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "x\u2028", 1, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "x\u2028", 1, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "x\u2028", 1, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "x\n\u2028", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.Multiline, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["^\\R", "x\n\u2028", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.Multiline, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["^\\R", "x\n\u2028", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.Multiline, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 5];
        yield return ["^\\R{2,3}x", "\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return ["^\\R{2,3}x", "\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return ["^\\R{2,3}x", "\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return ["^\\R{2,3}?x", "\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return ["^\\R{2,3}?x", "\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return ["^\\R{2,3}?x", "\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 7];
        yield return ["^\\R{2,3}x", "\u2028\u2029", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R{2,3}x", "\u2028\u2029", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 6];
        yield return ["^\\R{2,3}x", "\u2028\u2029", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 6];
        yield return ["^\\R{2,3}x", "\u2028\u2029x", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R{2,3}x", "\u2028\u2029x", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R{2,3}x", "\u2028\u2029x", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R{2,3}x", "\r\n\r\nx", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^\\R{2,3}x", "\r\n\r\nx", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^\\R{2,3}x", "\r\n\r\nx", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 5];
        yield return ["^\\R{2,3}x", "\n\n\n\nx", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R{2,3}x", "\n\n\n\nx", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R{2,3}x", "\n\n\n\nx", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R{2,3}?x", "\r\r", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R{2,3}?x", "\r\r", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^\\R{2,3}?x", "\r\r", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 2];
        yield return ["^\\R?x", "\u2028x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["^\\R?x", "\u2028x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["^\\R?x", "\u2028x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["^\\R?x", "x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R?x", "x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R?x", "x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R?x", "\r", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R?x", "\r", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^\\R?x", "\r", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^\\R?x", "", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R?x", "", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R?x", "", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R+x", "\u0085\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 9];
        yield return ["^\\R+x", "\u0085\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 9];
        yield return ["^\\R+x", "\u0085\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 9];
        yield return ["^\\R+x", "\u0085\u2028\u2029", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R+x", "\u0085\u2028\u2029", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.PartialMatch, 0, 8];
        yield return ["^\\R+x", "\u0085\u2028\u2029", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 8];
        yield return ["^\\R+x", "\ud83d\ude00\n\u2028x", 5, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.Multiline, Pcre2MatchOptions.Anchored, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 5, 9];
        yield return ["^\\R+x", "\ud83d\ude00\n\u2028x", 5, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.Multiline, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 5, 9];
        yield return ["^\\R+x", "\ud83d\ude00\n\u2028x", 5, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.Multiline, Pcre2MatchOptions.Anchored, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 5, 9];
        yield return ["^\\R{2,3}x", "x\n\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.Multiline, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 2, 9];
        yield return ["^\\R{2,3}x", "x\n\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.Multiline, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 2, 9];
        yield return ["^\\R{2,3}x", "x\n\u2028\u2029x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.Multiline, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 2, 9];
        yield return ["^\\R?x", "x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.NotBol, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R?x", "x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R?x", "x", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.NotBol, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.NoMatch, 0, 0];
        yield return ["^\\R", "\r", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\r", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 1];
        yield return ["^\\R", "\r", 0, Pcre2BsrConvention.AnyCrlf, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.PartialMatch, 0, 1];
        yield return ["^\\R{2,3}?x", "\n\r\nx", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.None, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["^\\R{2,3}?x", "\n\r\nx", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Soft, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
        yield return ["^\\R{2,3}?x", "\n\r\nx", 0, Pcre2BsrConvention.Unicode, Pcre2CompileOptions.None, Pcre2MatchOptions.None, Pcre2PartialMode.Hard, Utf8Pcre2ProbeKind.FullMatch, 0, 4];
    }

    [Theory]
    [MemberData(nameof(NativeCases))]
    public void NewlineProbesPreserveNativeBsrScalarsAndAtomicCrlf(string pattern, string text, int start,
        Pcre2BsrConvention bsr, Pcre2CompileOptions compile, Pcre2MatchOptions flags,
        Pcre2PartialMode mode, Utf8Pcre2ProbeKind kind, int begin, int end)
    {
        var regex = new Utf8Pcre2Regex(pattern, compile, new() { Bsr = bsr }, default, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        var probe = regex.Probe(input, mode, start, flags);
        Assert.Equal(kind, probe.Kind);
        Assert.Equal((begin, end), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
        if (kind == Utf8Pcre2ProbeKind.FullMatch)
            Assert.Equal(Encoding.UTF8.GetString(input.AsSpan(begin, end - begin)), probe.GetMatch().GetValueString());
    }
    [Fact]
    public void ScalarNewlineFullPreservesFiniteAdmissionAndRetainedAlias()
    {
        var capacity = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        Utf8Pcre2Regex Create(ulong bytes) => Utf8Pcre2Regex.Create(@"^\R{2,3}x", Pcre2CompileOptions.Multiline,
            new() { Bsr = Pcre2BsrConvention.Unicode },
            new() { MaxWorkSteps = 100000, MaxDepth = 256, MaxWorkingBytes = 1 << 20, MaxResultBytes = bytes },
            Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(capacity - 1).Probe("😀\n\u2028\u2029x"u8, Pcre2PartialMode.Soft); }).ErrorKind);
        var regex = Create(capacity);
        var retained = regex.Probe("😀\n\u2028\u2029x"u8, Pcre2PartialMode.Soft).GetMatch();
        var alias = retained.GetGroup(0);
        Assert.Equal((5, 12), (alias.StartOffsetInBytes, alias.EndOffsetInBytes));
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("qx"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal("\u2028\u2029x", retained.GetValueString());
        Assert.Equal("\u2028\u2029x", alias.GetValueString());
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("😀\n\u2028\u2029x"u8, Pcre2PartialMode.Soft).Kind);
        Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(1).Probe("\u2028\u2029"u8, Pcre2PartialMode.Hard).Kind);
    }

    [Fact]
    public void NewlineRepeatCannotBypassFiniteWork()
    {
        var regex = new Utf8Pcre2Regex(@"^\R+x", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(new string('\n', 1000) + "x");
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, regex.Probe("\nx"u8, Pcre2PartialMode.Soft).Kind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NewlineRepeatObservesOriginalInterruption(bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"^\R+x", Pcre2CompileOptions.None, default, default, TimeSpan.FromMilliseconds(20));
        var polls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++polls < 10) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        Exception? failure = null;
        try { _ = regex.Probe("\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\nx"u8, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

    [Fact]
    public void CompiledAnchorCannotSearchAnotherLine()
    {
        var regex = new Utf8Pcre2Regex(@"^\R+x", Pcre2CompileOptions.Anchored | Pcre2CompileOptions.Multiline);
        Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("x\n\u2028x"u8, Pcre2PartialMode.Soft).Kind);
        var probe = regex.Probe("x\n\u2028x"u8, Pcre2PartialMode.Soft, 2);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, probe.Kind);
        Assert.Equal((2, 6), (probe.Value.StartOffsetInBytes, probe.Value.EndOffsetInBytes));
    }
}
