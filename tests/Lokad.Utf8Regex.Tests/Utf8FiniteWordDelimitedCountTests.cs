using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteWordDelimitedCountTests
{
    private const string Pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
    private static Utf8Regex Create(RegexOptions options, ulong work)
        => new(Pattern, options, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });

    [Theory]
    [InlineData("", 0)]
    [InlineData("a@b.c", 1)]
    [InlineData("a@..", 0)]
    [InlineData("a@...", 1)]
    [InlineData("a@.b", 0)]
    [InlineData("a@.b.c", 1)]
    [InlineData("x@a@y.z", 1)]
    [InlineData("a@b.c tail d@e.f", 2)]
    [InlineData("équipe@exemple.fr", 1)]
    [InlineData("e\u0301quipe@exemple.fr", 1)]
    [InlineData("\u0301@b.c", 1)]
    [InlineData("𐐀@b.c", 1)]
    [InlineData("a@𐐀.c", 1)]
    [InlineData("😀@b.c", 0)]
    [InlineData("a@b.c😀d@e.f", 2)]
    public void ScalarCountsNeedNoVmStorageOrResultCapacity(string text, int expected)
    {
        var bytes = Encoding.UTF8.GetBytes("pad" + text + "tail");
        var length = Encoding.UTF8.GetByteCount(text);
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(options, 100000);
            Assert.Equal(expected, regex.Count(bytes.AsSpan(3, length)));
            Assert.Equal(expected, regex.Count(bytes.AsSpan(3, length), cancellation.Token));
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4096)]
    public void DelimiterWindowsRetainTheWholeHeadContext(int headLength)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', headLength) + "@b.c");
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
            Assert.Equal(1, Create(options, 100000).Count(input));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WholeSubjectValidationStillRejectsMalformedSuffix(bool compiled)
    {
        var input = "a@b.c"u8.ToArray().Append((byte)0xff).ToArray();
        Assert.Throws<ArgumentException>(() => Create(compiled ? RegexOptions.Compiled : RegexOptions.None, 100000).Count(input));
    }

    [Fact]
    public void WorkDenialDoesNotPoisonOtherInvocations()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 8192));
        var tiny = Create(RegexOptions.None, 1);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tiny.Count(input)).ResourceKind);
        Assert.Equal(0, Create(RegexOptions.None, 100000).Count(input));
        Assert.Equal(1, Create(RegexOptions.None, 100000).Count("a@b.c"u8));
    }

    [Fact]
    public void BmpCountsAgreeWithTheIndependentCodeUnitOracle()
    {
        const string alphabet = "ab09_@.+- é中\u0301/!";
        var random = new Random(18901);
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
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void KernelCancellationCoversSearchHeadAndBody(int phase)
    {
        var input = Encoding.UTF8.GetBytes(phase switch
        {
            0 => new string('x', 200000),
            1 => new string('x', 512) + "@b.c",
            _ => "a@" + new string('b', 512) + ".c",
        });
        using var cancellation = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(100000, 1, 1, 1), Regex.InfiniteMatchTimeout, cancellation.Token);
        invocation.CheckpointHook = _ => cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8WordDelimitedTokenExecutor.TryCountWellFormed(input, ref invocation, out _));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void KernelDeadlineCoversSearchHeadAndBody(int phase)
    {
        var input = Encoding.UTF8.GetBytes(phase switch
        {
            0 => new string('x', 200000),
            1 => new string('x', 512) + "@b.c",
            _ => "a@" + new string('b', 512) + ".c",
        });
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(new(100000, 1, 1, 1), TimeSpan.FromMilliseconds(200), default);
        invocation.CheckpointHook = _ => ticks = 250;
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
            Utf8WordDelimitedTokenExecutor.TryCountWellFormed(input, ref invocation, out _));
        Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(RegexOptions.IgnoreCase, 3)]
    [InlineData(RegexOptions.RightToLeft, 3)]
    [InlineData(RegexOptions.NonBacktracking, 3)]
    [InlineData(RegexOptions.ECMAScript, 2)]
    public void DeclinedOptionsKeepTheirScalarContract(RegexOptions options, int expected)
    {
        var regex = new Utf8Regex(Pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Assert.Equal(expected, regex.Count("équipe@exemple.fr 𐐀@b.c a@b.c"u8));
    }
}
