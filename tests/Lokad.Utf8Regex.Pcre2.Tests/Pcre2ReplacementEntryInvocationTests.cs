using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2ReplacementEntryInvocationTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var mode in new[] { "Bytes", "Text", "Utf8Template", "CallbackBytes", "CallbackText" })
        {
            yield return ["abc", "xabc", mode];
            yield return ["(?<x>a)b", "xab", mode];
            yield return [@"(?<x>a)\k<x>", "xaa", mode];
            yield return [@"(?<x>\x41+)", "xAAA", mode];
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void ValidationWorkDeniesBeforeMalformedTail(string pattern, string text, string mode)
    {
        var malformed = new byte[16385]; malformed.AsSpan().Fill((byte)'x'); malformed[^1] = 0xff;
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Run(Policy(pattern, 3), malformed, mode); }).ErrorKind);
        Assert.Equal("xZ", Run(Policy(pattern, 100000), Encoding.UTF8.GetBytes(text), mode));
    }

    [Theory] [MemberData(nameof(Routes))]
    public void OriginalTokenAndAbsoluteDeadlineCoverEntryAndPublication(string pattern, string text, string mode)
    {
        foreach (var cancelAt in new[] { 2, 8 })
        foreach (var hit in new[] { true, false })
        {
            using var source = new CancellationTokenSource();
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
                default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(20));
            var reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            { if (++reads == cancelAt) source.Cancel(); return 0; });
            var input = Encoding.UTF8.GetBytes(hit ? text : "zzz");
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
            { _ = Run(regex, input, mode, source.Token); }).CancellationToken);
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void WholeSubjectScalarStartsAndFreshOutputRemainValid(string pattern, string text, string mode)
    {
        var regex = Policy(pattern, 100000);
        var input = Encoding.UTF8.GetBytes("😀" + text);
        Assert.Equal("😀xZ", Run(regex, input, mode, start: 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(regex, input, mode, start: 1));
        var malformed = new byte[input.Length + 1]; input.CopyTo(malformed, 0); malformed[^1] = 0xff;
        Assert.Equal(Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(malformed)).Message,
            Assert.Throws<ArgumentException>(() => Run(regex, malformed, mode)).Message);
        Assert.Equal("😀xZ", Run(regex, input, mode, start: 4));
    }

    [Theory] [InlineData("abc")] [InlineData(@"(?<x>a)\k<x>")] [InlineData(@"(?<x>\x41+)")]
    public void Utf8TemplateValidationAndPreparationUseOriginalLimits(string pattern)
    {
        var malformed = new byte[16385]; malformed.AsSpan().Fill((byte)'x'); malformed[^1] = 0xff;
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
            Policy(pattern, 3).Replace("xabc"u8, malformed)).ErrorKind);
        var template = new byte[16384]; template.AsSpan().Fill((byte)'Z');
        var limited = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, HeapLimitInBytes = 64 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
            limited.Replace("zzz"u8, template)).ErrorKind);
    }

    private static Utf8Pcre2Regex Policy(string pattern, uint work) => new(pattern,
        Pcre2CompileOptions.None, default, new() { MatchLimit = work, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);

    private static string Run(Utf8Pcre2Regex regex, byte[] input, string mode, CancellationToken token = default, int start = 0)
        => mode switch
        {
            "Bytes" => Encoding.UTF8.GetString(regex.Replace(input, "Z", start, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token)),
            "Text" => regex.ReplaceToString(input, "Z", start, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token),
            "Utf8Template" => Encoding.UTF8.GetString(regex.Replace(input, "Z"u8, start, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token)),
            "CallbackBytes" => Encoding.UTF8.GetString(regex.Replace(input, 0,
                static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) => writer.Append("Z"u8),
                start, Pcre2MatchOptions.None, token)),
            _ => regex.ReplaceToString(input, 0, static (in Utf8Pcre2MatchContext match, ref int state) => "Z",
                start, Pcre2MatchOptions.None, token),
        };
}
