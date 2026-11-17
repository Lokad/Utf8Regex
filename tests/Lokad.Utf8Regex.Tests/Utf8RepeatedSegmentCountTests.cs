using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RepeatedSegmentCountTests
{
    [Theory]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}")]
    [InlineData(@"(?:[A-Z][a-z]{2,}\s+){1,3}")]
    [InlineData(@"(?:[AC][bd]+[ \t]*){2,4}")]
    [InlineData(@"(?:[A-Z][a-z]+[\sα😀]*){2,3}")]
    [InlineData(@"(?:[A-Z][a-z]+[ α]*){2,3}")]
    public void PublicCountAndOwnedDiscoveryAgreeAtEveryScalarOrigin(string pattern)
    {
        var texts = new[] { "", "Ab", "AbCd", "AbCdEf", "AbCdEfGh", "AbCdEfGhIjKlMn",
            "😀Ab\u00a0Cd\u2028EfGh!", "AbαCd😀Ef", "AbαCd", "ABcCd", "Ab Cd X", "Ab  Cd  EF",
            "Abbb\t Cddd \r\nAbbb Cddd ", "Ab\u2003Cd\u0085Ef\r\nGh", "Acbd Cbdd Acbdd Cbdd ",
            "xxxxAbCd\0EfGh", "Ab" + new string('b', 2050) + new string(' ', 2050) + "Cd" };
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);
            var reference = new Regex(pattern, options);
            using var live = new CancellationTokenSource();
            foreach (var text in texts)
            {
                var input = Encoding.UTF8.GetBytes(text);
                Assert.Equal(reference.Count(text), regex.Count(input));
                Assert.Equal(reference.Count(text), regex.Count(input, live.Token));
                var cursor = regex.EnumerateMatches(input); var discovered = 0;
                while (cursor.MoveNext()) discovered++;
                Assert.Equal(reference.Count(text), discovered);
                var boundaries = new List<int> { 0 };
                foreach (var scalar in text.EnumerateRunes()) boundaries.Add(boundaries[^1] + scalar.Utf8SequenceLength);
                foreach (var start in boundaries)
                {
                    var expected = reference.Count(text, Encoding.UTF8.GetCharCount(input.AsSpan(0, start)));
                    Assert.Equal(expected, Utf8MatchSequence.Count(regex.EnumerateMatches(input, start)));
                }

            }
        }
    }

    [Theory]
    [InlineData(@"(?<x>(?:[A-Z][a-z]+\s*){2,3})", RegexOptions.None)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){0,3}", RegexOptions.None)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}?", RegexOptions.None)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}!", RegexOptions.None)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}", RegexOptions.IgnoreCase)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}", RegexOptions.RightToLeft)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){2,3}", RegexOptions.NonBacktracking)]
    [InlineData(@"(?:[A-Z][A-Za-z]+\s*){2,3}", RegexOptions.None)]
    public void NeighbouringGrammarsPreservePublicCounts(string pattern, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options);
        Assert.Equal(new Regex(pattern, options).Count("Ab Cd Ef !"), regex.Count("Ab Cd Ef !"u8));
    }

    [Fact]
    public void FiniteCountPreservesSharedLoansAndFirstDenial()
    {
        const string pattern = @"(?:[A-Z][a-z]+\s*){2,3}";
        var input = "xx Ab\u00a0Cd\u2028Ef GhIj "u8.ToArray();
        var regex = new Utf8Regex(pattern, resourcePolicy: new() { MaxWorkSteps = 1_000_000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20 });
        var account = new WorkingAllowance(1 << 20); Assert.True(account.TryReserve(13));
        Assert.Equal(new Regex(pattern).Count(System.Text.Encoding.UTF8.GetString(input)), regex.Count(input, default, account));
        Assert.Equal(13UL, account.Used);
        var denied = new Utf8Regex(pattern, resourcePolicy: new() { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input, default, account)).ResourceKind);
        Assert.Equal(13UL, account.Used);
        var refusing = new WorkingAllowance(13); Assert.True(refusing.TryReserve(13));
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.Count(input, default, refusing));
        Assert.Equal(13UL, refusing.Used);
    }

    [Theory]
    [InlineData("seek", false)]
    [InlineData("seek", true)]
    [InlineData("tail", false)]
    [InlineData("tail", true)]
    [InlineData("separator", false)]
    [InlineData("separator", true)]
    [InlineData("unicode", false)]
    [InlineData("unicode", true)]
    [InlineData("publication", false)]
    [InlineData("publication", true)]
    public void CountPreservesOriginalCancellationAndDeadlineAtPublication(string stage, bool expire)
    {
        const string pattern = @"(?:[A-Z][a-z]+\s*){2,3}";
        var text = stage switch { "seek" => new string('x', 8192), "tail" => "A" + new string('b', 8192) + "Cd",
            "separator" => "Ab" + new string(' ', 8192) + "Cd", "unicode" => "Ab" + new string('\u2003', 4096) + "Cd", _ => "AbCd" };
        var bytes = Encoding.UTF8.GetBytes(text);
        using var live = new CancellationTokenSource();
        long ticks = 0; var reads = 0; var trigger = int.MaxValue;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == trigger) { if (expire) ticks = 100; else live.Cancel(); } return ticks; });
        var regex = new Utf8Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50),
            new() { MaxWorkSteps = 10_000_000, MaxDepth = 100000, MaxWorkingBytes = 16 << 20 });
        _ = regex.Count(bytes, live.Token);
        Assert.True(reads > 0); trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(bytes, live.Token)).MatchTimeout);
        else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, live.Token)).CancellationToken);
    }

    [Fact]
    public void PublicAdmissionAndStrictTailValidationStillApply()
    {
        const string pattern = @"(?:[A-Z][a-z]+\s*){2,3}";
        var regex = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 });
        Assert.Equal(1, regex.Count("Ab\u2003Cd"u8));
        var malformed = "AbCd"u8.ToArray().Concat(new byte[] { 0xc3 }).ToArray();
        using var live = new CancellationTokenSource(); live.Cancel();
        Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(malformed, live.Token)).CancellationToken);
    }

    private sealed class WorkingAllowance(ulong capacity) : IUtf8MemoryAllowance
    {
        internal ulong Used;
        public bool TryReserve(ulong bytes) { if (bytes > capacity - Used) return false; Used += bytes; return true; }
        public void Release(ulong bytes) { Assert.True(bytes <= Used); Used -= bytes; }
    }
}
