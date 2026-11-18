using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2FullProbeInvocationAuthorityTests
{
    public static IEnumerable<object[]> Routes()
    {
        yield return [@"(?<x>a)\k<x>", "aa", "a", "Pcre2SliceDirectProgram"];
        yield return ["(?<x>a)b", "ab", "a", "Pcre2BacktrackingDirectProgram"];
        yield return [@"(?<x>\x{41}+)", "AAA", "AAA", "Pcre2BacktrackingDirectProgram"];
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void SearchStopsAtWorkDenial(
        string pattern, string text, string capture, string route)
    {
        Assert.NotEmpty(capture);
        var regex = Create(pattern, 3);
        Assert.Equal(route, regex.DebugCompiledProgram.Operations.Match.GetType().Name);
        var input = Encoding.UTF8.GetBytes(new string('x', 16385) + text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.None).GetMatch(); }).ErrorKind);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void SearchObservesOriginalInterruption(
        string pattern, string text, string capture, string route)
    {
        foreach (var cancel in new[] { false, true })
        {
            using var source = new CancellationTokenSource();
            Assert.NotEmpty(capture);
            var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.FromMilliseconds(20)
            });
            Assert.Equal(route, regex.DebugCompiledProgram.Operations.Match.GetType().Name);
            var reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            {
                if (++reads < 4) return 0;
                if (cancel) { source.Cancel(); return 0; }
                return 25;
            });
            var input = Encoding.UTF8.GetBytes(new string('x', 16385) + text);
            Exception? failure = null;
            try { _ = regex.Probe(input, Pcre2PartialMode.None, cancellationToken: source.Token).GetMatch(); }
            catch (Exception error) { failure = error; }
            if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void OneAllowanceCoversDiscoveryAndPublication(
        string pattern, string text, string capture, string route)
    {
        Assert.Equal(route, Create(pattern, 100000).DebugCompiledProgram.Operations.Match.GetType().Name);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(pattern, 3).Probe(input, Pcre2PartialMode.None).GetMatch(); }).ErrorKind);
        var retained = Create(pattern, 100000).Probe(input, Pcre2PartialMode.None).GetMatch();
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
        Assert.True(retained.TryGetGroup(1, out var named)); Assert.True(named.Success);
        Assert.Equal(capture, System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(pattern, 100000, result: 1).Probe(input, Pcre2PartialMode.None).GetMatch(); }).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(pattern, 100000, heap: 1).Probe(input, Pcre2PartialMode.None).GetMatch(); }).ErrorKind);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
        Assert.Equal(capture, System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void ScalarStartsFlagsNamesAndRetainedCoordinatesStayValid(
        string pattern, string text, string capture, string route)
    {
        var regex = Create(pattern, 100000);
        Assert.Equal(route, regex.DebugCompiledProgram.Operations.Match.GetType().Name);
        foreach (var lead in new[] { 2046, 2047, 2048 })
            foreach (var flags in new[] { Pcre2MatchOptions.None, Pcre2MatchOptions.NotEmpty })
            {
                var prefix = new string('z', lead) + "😀";
                var input = Encoding.UTF8.GetBytes(prefix + text);
                var retained = regex.Probe(input, Pcre2PartialMode.None, lead + 4, flags).GetMatch();
                var whole = retained.GetGroup(0);
                Assert.Equal((lead + 4, input.Length), (whole.StartOffsetInBytes, whole.EndOffsetInBytes));
                Assert.True(retained.TryGetGroup(1, out var named)); Assert.True(named.Success);
                Assert.Equal(capture, System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
                Assert.Throws<ArgumentOutOfRangeException>(() => { _ = regex.Probe(input, Pcre2PartialMode.None, lead + 1, flags).GetMatch(); });
                _ = regex.Probe(Encoding.UTF8.GetBytes(text), Pcre2PartialMode.None).GetMatch();
                Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
                Assert.Equal(capture, System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
            }
    }

    private static Utf8Pcre2Regex Create(string pattern, uint work, ulong heap = 1 << 20, ulong result = 1 << 20)
        => new(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = (work) == 0 ? null : (work),
                MaxDepth = 256,
                MaxWorkingBytes = (heap) == 0 ? null : (heap),
                MaxResultBytes = (result) == 0 ? null : (result)
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });

}
