using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2ReplacementEntryInvocationTests
{
    public static IEnumerable<object[]> Routes()
    {
        yield return ["abc", "xabc"];
        yield return ["(?<x>a)b", "xab"];
        yield return [@"(?<x>a)\k<x>", "xaa"];
        yield return [@"(?<x>\x41+)", "xAAA"];
    }

    [Theory] [MemberData(nameof(Routes))]
    public void SearchWorkDeniesBeforeUnvisitedTail(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 16385) + text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
            Policy(pattern, 3).Replace(input, "Z"u8)).ErrorKind);
        Assert.Equal("xZ", Replace(Policy(pattern, 100000), Encoding.UTF8.GetBytes(text)));
    }

    [Theory] [MemberData(nameof(Routes))]
    public void OriginalTokenIsObservedAtEntryAndDuringReplacement(string pattern, string text)
    {
        foreach (var cancelAt in new[] { 2, 8 })
        foreach (var hit in new[] { true, false })
        {
            using var source = new CancellationTokenSource();
            var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.FromMilliseconds(20)
            });
            var reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            { if (++reads == cancelAt) source.Cancel(); return 0; });
            var input = Encoding.UTF8.GetBytes(hit ? text : "zzz");
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Replace(input, "Z"u8, cancellationToken: source.Token)).CancellationToken);
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void ScalarStartsAndFreshOutputRemainValid(string pattern, string text)
    {
        var regex = Policy(pattern, 100000);
        var input = Encoding.UTF8.GetBytes("😀" + text);
        Assert.Equal("😀xZ", Replace(regex, input, start: 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => Replace(regex, input, start: 1));
        Assert.Equal("😀xZ", Replace(regex, input, start: 4));
    }

    [Theory]
    [InlineData("abc", "xabc")]
    [InlineData(@"(?<x>a)\k<x>", "xaa")]
    [InlineData(@"(?<x>\x41+)", "xAAA")]
    public void Utf8TemplateValidationAndPreparationUseOriginalLimits(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var malformed = new byte[16385]; malformed.AsSpan().Fill((byte)'x'); malformed[^1] = 0xff;
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
            Policy(pattern, 3).Replace(input, malformed)).ErrorKind);
        var template = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("$0", 8192)));
        var destination = new byte[1 + 8192 * (input.Length - 1)];
        var limited = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxWorkingBytes = 4096
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(OperationStatus.Done, limited.TryReplace(input, "Z"u8, destination, out var written));
        Assert.Equal("xZ", Encoding.UTF8.GetString(destination.AsSpan(0, written)));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
            limited.TryReplace(input, template, destination, out _)).ErrorKind);
    }

    private static Utf8Pcre2Regex Policy(string pattern, uint work) => new(pattern, options:         Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
    {
        ResourcePolicy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = (work) == 0 ? null : (work),
            MaxDepth = 256,
            MaxWorkingBytes = 1 << 20,
            MaxResultBytes = 1 << 20
        },
        MatchTimeout = Timeout.InfiniteTimeSpan
    });

    private static string Replace(Utf8Pcre2Regex regex, byte[] input, int start = 0)
        => Encoding.UTF8.GetString(regex.Replace(input, "Z"u8, start));
}
