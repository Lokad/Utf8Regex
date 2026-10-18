using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteUriCountTests
{
    private const string Pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
    private static Utf8Regex Create(RegexOptions options, ulong work)
        => new(Pattern, options, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });

    [Theory]
    [InlineData("", 0)]
    [InlineData("x://a", 0)]
    [InlineData("x://ab", 1)]
    [InlineData("x://a/", 1)]
    [InlineData("x:///ab", 0)]
    [InlineData("x://ab?", 1)]
    [InlineData("x://ab#", 1)]
    [InlineData("x://ab?#", 1)]
    [InlineData("x://ab?q=é#片", 1)]
    [InlineData("x://ab#fragment?tail", 1)]
    [InlineData("x://ab?q#x://cd", 1)]
    [InlineData("x://ab y://cd", 2)]
    [InlineData("é://ab", 1)]
    [InlineData("𐐀://ab", 1)]
    [InlineData("😀://ab", 0)]
    [InlineData("x://😀", 0)]
    [InlineData("x://😀a", 1)]
    [InlineData("x://a😀", 1)]
    [InlineData("x://ab\u00a0y://cd", 2)]
    [InlineData("x://a\u2003b", 0)]
    [InlineData("x://ab?\0#\0", 1)]
    public void UriCountsUseCompleteScalarsWithoutVmStorage(string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes("pad" + text + "tail");
        var length = Encoding.UTF8.GetByteCount(text);
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(options, 100000);
            Assert.Equal(expected, regex.Count(input.AsSpan(3, length)));
            Assert.Equal(expected, regex.Count(input.AsSpan(3, length), cancellation.Token));
        }
    }

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4095)]
    public void DelimiterWindowsPreserveAllThreeBytesAndSchemeContext(int schemeLength)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', schemeLength) + "://ab?q#f");
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
            Assert.Equal(1, Create(options, 100000).Count(input));
    }

    [Fact]
    public void EntireInputValidityAndWorkDenialRemainAuthoritative()
    {
        Assert.Throws<ArgumentException>(() => Create(RegexOptions.None, 100000).Count("x://ab"u8.ToArray().Append((byte)0xff).ToArray()));
        var tiny = Create(RegexOptions.None, 1);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tiny.Count(Encoding.UTF8.GetBytes(new string('x', 8192)))).ResourceKind);
        Assert.Equal(1, Create(RegexOptions.None, 100000).Count("x://ab"u8));
    }

    [Fact]
    public void BmpCountsAgreeWithIndependentRegexSemantics()
    {
        const string alphabet = "ab09_:/?#.- é中\u0301\u0903\u00a0\u0085\0";
        var random = new Random(30927);
        var oracle = new Regex(Pattern);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(options, 100000);
            for (var sample = 0; sample < 400; sample++)
            {
                var chars = new char[random.Next(128)];
                for (var index = 0; index < chars.Length; index++) chars[index] = alphabet[random.Next(alphabet.Length)];
                var text = new string(chars);
                Assert.Equal(oracle.Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void CancellationCoversEveryUnboundedKernelLoop(int phase)
    {
        var input = PhaseInput(phase);
        using var cancellation = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(100000, 1, 1, 1), Regex.InfiniteMatchTimeout, cancellation.Token);
        invocation.CheckpointHook = _ => cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8UriTokenCountExecutor.CountWellFormed(input, ref invocation));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void DeadlineCoversEveryUnboundedKernelLoop(int phase)
    {
        var input = PhaseInput(phase);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(new(100000, 1, 1, 1), TimeSpan.FromMilliseconds(200), default);
        invocation.CheckpointHook = _ => ticks = 250;
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8UriTokenCountExecutor.CountWellFormed(input, ref invocation));
        Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
    }

    private static byte[] PhaseInput(int phase) => Encoding.UTF8.GetBytes(phase switch
    {
        0 => new string('x', 200000),
        1 => new string('x', 512) + "://ab",
        // Exercise enough bounded ASCII blocks to interrupt the live loop,
        // rather than requiring one diagnostic checkpoint per scalar.
        2 => "x://" + new string('b', 200000),
        3 => "x://ab?" + new string('b', 200000),
        _ => "x://ab#" + new string('b', 200000),
    });

    [Fact]
    public void TailPhasesAgreeWithIndependentCapturedScalarExecution()
    {
        var native = Create(RegexOptions.None, 1000000);
        var captured = new Utf8Regex("(?<uri>" + Pattern + ")", RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout, new() { MaxWorkSteps = 1000000, MaxDepth = 256,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        string[] atoms = ["a", "/", "?", "#", " ", "\t", "\0", "\u007f", "é", "😀", "\u0085", "\u2003", "x://ab"];
        var random = new Random(104721);
        for (var sample = 0; sample < 500; sample++)
        {
            var text = "lead x://" + string.Concat(Enumerable.Range(0, random.Next(49))
                .Select(_ => atoms[random.Next(atoms.Length)])) + "\u2003y://ab#\0";
            var input = Encoding.UTF8.GetBytes(text);
            byte[] storage = [0xff, .. input, 0xff];
            Assert.Equal(captured.Count(input), native.Count(storage.AsSpan(1, input.Length)));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalPublicationObservesOriginalInterruption(bool deadline)
    {
        var input = "x://ab"u8.ToArray();
        var complete = new Utf8SliceInvocation(new(1000, 1, 1, 1), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(1, Utf8UriTokenCountExecutor.CountWellFormed(input, ref complete));
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(new(1000, 1, 1, 1),
            deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, cancellation.Token);
        while (invocation.Budget.Steps < 64 - complete.Budget.Steps)
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeStep());
        invocation.CheckpointHook = _ => { if (deadline) ticks = 250; else cancellation.Cancel(); };
        if (deadline)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                Utf8UriTokenCountExecutor.CountWellFormed(input, ref invocation));
        else
        {
            var error = Assert.Throws<OperationCanceledException>(() =>
                Utf8UriTokenCountExecutor.CountWellFormed(input, ref invocation));
            Assert.Equal(cancellation.Token, error.CancellationToken);
        }
        Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(RegexOptions.IgnoreCase, 3)]
    [InlineData(RegexOptions.RightToLeft, 3)]
    [InlineData(RegexOptions.NonBacktracking, 3)]
    [InlineData(RegexOptions.ECMAScript, 1)]
    public void DeclinedOptionsRetainTheirScalarBehavior(RegexOptions options, int expected)
    {
        var regex = new Utf8Regex(Pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Assert.Equal(expected, regex.Count("é://ab 𐐀://ab a://ab"u8));
    }
}
