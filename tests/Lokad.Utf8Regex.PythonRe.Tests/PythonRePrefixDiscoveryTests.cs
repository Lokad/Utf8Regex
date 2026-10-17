using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonRePrefixDiscoveryTests
{
    private static T Field<T>(Utf8PythonRegex regex, string name) =>
        (T)typeof(Utf8PythonRegex).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(regex)!;

    [Theory]
    [InlineData("(ab|cd)+", "xxabcd yy cdcd ab xx")]
    [InlineData("(a|ab)+b", "aaaab abab aaac ab")]
    [InlineData("(ab|a)+b", "aabab ab aaac")]
    [InlineData("(ab|cd)+?", "abcdabcd cd")]
    [InlineData("(?P<word>abc)(?P=word)", "abcX abcabc abc")]
    [InlineData("(?<=é)(ab|cd)", "ab éab écd cd")]
    [InlineData("(?=abc)(abc)", "abcX xxabc")]
    [InlineData("(😀|é)+", "xx😀é xx😀")]
    [InlineData("(ab|cd)*", "xxabcd yy")]
    [InlineData("|ab", "ab ab")]
    [InlineData("(?i:ab|cd)+", "ABcd xx abCD")]
    [InlineData("(?ai:k)abc", "Kabc Kabc kabc")]
    [InlineData("^abc[0-9]+", "xxabc1\nabc2\nabcX\nabc3")]
    public void PublicDiscoveryMatchesUnguidedProfileVerification(string pattern, string subject)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.Multiline);
        var input = Encoding.UTF8.GetBytes(subject);
        var plain = new PythonReScalarMatchCursor(input, Field<Utf8SliceProgram>(regex, "_scalarProgram"),
            0, Regex.InfiniteMatchTimeout, pattern,
            default, Utf8ExecutionLimits.Unlimited);
        var expected = new List<Utf8PythonDetailedMatchData>();
        try { while (plain.MoveNext()) expected.Add(plain.SnapshotDetailed(Field<PythonReNameEntry[]>(regex, "_nameEntries"))); }
        finally { plain.Dispose(); }
        var finite = new Utf8PythonRegex(pattern, PythonReCompileOptions.Multiline, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        foreach (var actual in new[] { regex.FindIterDetailed(input), finite.FindIterDetailed(input) })
        {
            Assert.Equal(expected.Count, actual.Length);
            for (var i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i].Groups, actual[i].Groups);
                Assert.Equal(expected[i].LastIndex, actual[i].LastIndex);
                Assert.Equal(expected[i].LastGroup, actual[i].LastGroup);
            }
        }
        Assert.Equal(expected.Count, finite.Count(input));
        using var cursor = finite.CreateMatchCursor(input);
        var index = 0;
        while (cursor.MoveNext()) Assert.Equal(expected[index++].Groups, cursor.Current.Groups);
        Assert.Equal(expected.Count, index);
    }

    [Fact]
    public void PrefixGuidancePreservesEveryScalarStartAcrossAdversarialSubjects()
    {
        string[] patterns = ["(ab|a)+b", "(ab|a)+?b", "(?P<x>a)?(?(x)b|c)",
            "(?P<x>){0}(?(x)ab|c)", "(?<=é)(ab|a)", "(?=ab)(ab|a)",
            "ab(?P<x>a)(?i:(?P=x))", "ab(?ai:k)", "(😀é|😀)+", "|ab"];
        string[] pieces = ["a", "b", "c", "é", "😀", "K", "K"];
        foreach (var pattern in patterns)
        {
            var regex = new Utf8PythonRegex(pattern);
            var program = Field<Utf8SliceProgram>(regex, "_scalarProgram");

            var names = Field<PythonReNameEntry[]>(regex, "_nameEntries");
            foreach (var first in pieces)
            foreach (var second in pieces)
            {
                var subject = "x" + first + second + "ab😀éabc";
                var input = Encoding.UTF8.GetBytes(subject);
                var start = 0;
                while (true)
                {
                    var plain = new PythonReScalarMatchCursor(input, program, start,
                        Regex.InfiniteMatchTimeout, pattern, default, Utf8ExecutionLimits.Unlimited);
                    var expected = new List<Utf8PythonDetailedMatchData>();
                    try { while (plain.MoveNext()) expected.Add(plain.SnapshotDetailed(names)); }
                    finally { plain.Dispose(); }
                    var actual = regex.FindIterDetailed(input, start);
                    Assert.Equal(expected.Count, actual.Length);
                    for (var index = 0; index < actual.Length; index++)
                    {
                        Assert.Equal(expected[index].Groups, actual[index].Groups);
                        Assert.Equal(expected[index].LastIndex, actual[index].LastIndex);
                        Assert.Equal(expected[index].LastGroup, actual[index].LastGroup);
                    }
                    if (start == input.Length) break;
                    start++;
                    while (start < input.Length && (input[start] & 0xc0) == 0x80) start++;
                }
            }
        }
    }

    [Fact]
    public void MandatoryPrefixKeepsAnchoredOperationsAndByteStart()
    {
        var regex = new Utf8PythonRegex("(ab|cd)+");
        var input = "éxxabcd"u8.ToArray();
        Assert.False(regex.MatchDetailedData(input).Success);
        Assert.False(regex.FullMatchDetailedData(input).Success);
        Assert.Equal(4, regex.SearchDetailedData(input).Value.StartOffsetInBytes);
        Assert.Equal(1, regex.Count(input, 4));
        Assert.Equal(0, regex.Count(input, 8));
        Assert.Throws<ArgumentException>(() => regex.FindIterDetailed(input.Concat(new byte[] { 0xff }).ToArray()));
    }

    [Fact]
    public void GuidedDiscoveryReducesImpossibleAttemptsWithEveryWorkCutoffPreserved()
    {
        var regex = new Utf8PythonRegex("(ab|cd)+");
        var program = Field<Utf8SliceProgram>(regex, "_scalarProgram");

        var candidate = Field<global::Lokad.Utf8Regex.Utf8Regex>(regex, "_utf8Regex");
        var input = Encoding.UTF8.GetBytes(new string('x', 4097) + "abcd xx cd");
        var plain = new PythonReScalarMatchCursor(input, program, 0, Regex.InfiniteMatchTimeout,
            "(ab|cd)+", default, new(ulong.MaxValue, 0, 1048576));
        ulong originalWork;
        try { var count = 0; while (plain.MoveNext()) count++; Assert.Equal(2, count); originalWork = plain.WorkingInvocation.Budget.Steps; }
        finally { plain.Dispose(); }
        var guided = new PythonReScalarMatchCursor(input, program, 0, Regex.InfiniteMatchTimeout,
            candidate, "(ab|cd)+", new(new(ulong.MaxValue, 0, 1048576), Regex.InfiniteMatchTimeout, default));
        ulong work;
        try { var count = 0; while (guided.MoveNext()) count++; Assert.Equal(2, count); work = guided.WorkingInvocation.Budget.Steps; }
        finally { guided.Dispose(); }
        Assert.True(work < originalWork / 4);
        for (ulong cap = 1; cap <= work; cap++)
        {
            PythonReScalarMatchCursor cursor = default;
            var constructed = false;
            try
            {
                cursor = new(input, program, 0, Regex.InfiniteMatchTimeout, candidate, "(ab|cd)+",
                    new(new(cap, 0, 1048576), Regex.InfiniteMatchTimeout, default));
                constructed = true;
                Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
                var count = 0;
                while (cursor.MoveNext()) count++;
                Assert.Equal(work, cap); Assert.Equal(2, count);
            }
            catch (Utf8ResourceLimitExceededException error)
            {
                Assert.True(cap < work);
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
            }
            finally
            {
                if (constructed) { cursor.Dispose(); Assert.Equal(13UL, cursor.ReservedBytes); }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CandidateMissPollsOriginalCursorAuthority(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8PythonRegex("(ab|cd)+", PythonReCompileOptions.None, timeout);
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(new string('x', 200000)), 0, token.Token);
        cursor.WorkingInvocation.ShareStorageForCursor();
        var observer = cursor.WorkingInvocation;
        Assert.True(observer.Ledger.TryReserve(13));
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else token.Cancel(); };
        if (expire) Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext());
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext()).CancellationToken);
        Assert.True(entered);
        Assert.Equal(13UL, observer.Ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
        Assert.Equal(1, new Utf8PythonRegex("(ab|cd)+").Count("abcd"u8));
    }

    [Fact]
    public void ExhaustedCandidateSearchDoesNotPublishAfterCancellation()
    {
        var regex = new Utf8PythonRegex("(ab|cd)+");
        var input = Encoding.UTF8.GetBytes(new string('x', 4097));
        ulong work;
        using (var measured = regex.CreateMatchCursor(input))
        {
            measured.WorkingInvocation.ShareStorageForCursor();
            var observer = measured.WorkingInvocation;
            var before = measured.WorkingInvocation.Budget.Steps;
            measured.WorkingInvocation.CheckpointHook = _ => { };
            Assert.False(measured.MoveNext());
            work = observer.Budget.Steps - before;
        }
        using var token = new CancellationTokenSource();
        using var cursor = regex.CreateMatchCursor(input, 0, token.Token);
        while ((cursor.WorkingInvocation.Budget.Steps + work) % 64 != 0)
            Assert.Equal(Utf8ExecutionLimitKind.None, cursor.WorkingInvocation.TryChargeInputStep());
        var terminal = cursor.WorkingInvocation.Budget.Steps + work;
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = steps =>
        {
            if (steps == terminal) { entered = true; token.Cancel(); }
        };
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext()).CancellationToken);
        Assert.True(entered);
    }
}
