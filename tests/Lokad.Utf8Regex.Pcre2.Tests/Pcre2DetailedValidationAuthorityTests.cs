using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2DetailedValidationAuthorityTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var probe in new[] { false, true })
        {
            yield return [@"(?<x>a)\k<x>", "aa", "a", "Pcre2SliceDirectProgram", probe];
            yield return ["(?<x>a)b", "ab", "a", "Pcre2BacktrackingDirectProgram", probe];
            yield return [@"(?<x>\x{41}+)", "AAA", "AAA", "Pcre2BacktrackingDirectProgram", probe];
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void SearchStopsAtWorkDenial(
        string pattern, string text, string capture, string route, bool probe)
    {
        Assert.NotEmpty(capture);
        var regex = Create(pattern, 3);
        Assert.Equal(route, regex.DebugCompiledProgram.Operations.Match.GetType().Name);
        var input = Encoding.UTF8.GetBytes(new string('x', 16385) + text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Match(regex, input, probe); }).ErrorKind);
    }

    [Theory] [MemberData(nameof(Routes))]
    public void SearchObservesOriginalInterruption(
        string pattern, string text, string capture, string route, bool probe)
    {
        foreach (var cancel in new[] { false, true })
        {
            using var source = new CancellationTokenSource();
            Assert.NotEmpty(capture);
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
                default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(20));
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
            try { _ = Match(regex, input, probe, token: source.Token); }
            catch (Exception error) { failure = error; }
            if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void OneAllowanceCoversDiscoveryAndPublication(
        string pattern, string text, string capture, string route, bool probe)
    {
        Assert.Equal(route, Create(pattern, 100000).DebugCompiledProgram.Operations.Match.GetType().Name);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Match(Create(pattern, 3), input, probe); }).ErrorKind);
        var retained = Match(Create(pattern, 100000), input, probe);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
        Assert.True(retained.TryGetFirstSetGroup("x", out var named));
        Assert.Equal(capture, System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Match(Create(pattern, 100000, result: 1), input, probe); }).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Match(Create(pattern, 100000, heap: 1), input, probe); }).ErrorKind);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
        Assert.Equal(capture, System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
    }

    [Theory] [MemberData(nameof(Routes))]
    public void ScalarStartsFlagsNamesAndRetainedCoordinatesStayValid(
        string pattern, string text, string capture, string route, bool probe)
    {
        var regex = Create(pattern, 100000);
        Assert.Equal(route, regex.DebugCompiledProgram.Operations.Match.GetType().Name);
        foreach (var lead in new[] { 2046, 2047, 2048 })
        foreach (var flags in new[] { Pcre2MatchOptions.None, Pcre2MatchOptions.NotEmpty })
        {
            var prefix = new string('z', lead) + "😀";
            var input = Encoding.UTF8.GetBytes(prefix + text);
            var retained = Match(regex, input, probe, lead + 4, flags);
            var whole = retained.GetGroup(0);
            Assert.Equal((lead + 4, input.Length), (whole.StartOffsetInBytes, whole.EndOffsetInBytes));
            Assert.True(retained.TryGetFirstSetGroup("x", out var named));
            Assert.Equal(capture, System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = Match(regex, input, probe, lead + 1, flags); });
            _ = Match(regex, Encoding.UTF8.GetBytes(text), probe);
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.Value.GetValueBytes()));
            Assert.Equal(capture, System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
        }
    }

    private static Utf8Pcre2Regex Create(string pattern, uint work, ulong heap = 1 << 20, ulong result = 1 << 20)
        => new(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = work, DepthLimit = 256, HeapLimitInBytes = heap, ResultLimitInBytes = result },
            Timeout.InfiniteTimeSpan);

    private static Utf8Pcre2MatchContext Match(Utf8Pcre2Regex regex, byte[] input, bool probe,
        int start = 0, Pcre2MatchOptions flags = Pcre2MatchOptions.None, CancellationToken token = default)
        => probe ? regex.Probe(input, Pcre2PartialMode.None, start, flags, token).GetMatch()
            : regex.MatchDetailed(input, start, flags, token);
}
