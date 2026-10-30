using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2NativeLexicalTests
{
    public static IEnumerable<object?[]> SyntaxCases()
    {
        yield return ["é😀(?C)", Pcre2CompileOptions.None, Pcre2ErrorKind.CalloutUnsupported];
        yield return [@"é\C", Pcre2CompileOptions.None, Pcre2ErrorKind.BackslashCDisabled];
        yield return [@"(?=\K)", Pcre2CompileOptions.None, Pcre2ErrorKind.LookaroundBackslashKDisabled];
        yield return [@"[\R]", Pcre2CompileOptions.None, Pcre2ErrorKind.EscapeInvalidInClass];
        yield return ["😀{65536}", Pcre2CompileOptions.None, Pcre2ErrorKind.QuantifierTooBig];
        yield return ["😀{5,4}", Pcre2CompileOptions.None, Pcre2ErrorKind.QuantifierNumbersOutOfOrder];
        yield return ["(?<𝟘>a)", Pcre2CompileOptions.None, Pcre2ErrorKind.InvalidAfterParensQuery];
        yield return ["(?<" + new string('é', 64) + ">😀)", Pcre2CompileOptions.None, null];
        yield return ["(?<" + new string('é', 65) + ">😀)", Pcre2CompileOptions.None, Pcre2ErrorKind.InvalidAfterParensQuery];
        yield return ["(*MaRk:" + new string('é', 127) + "a)😀", Pcre2CompileOptions.None, null];
        yield return ["(*MARK:" + new string('é', 128) + ")😀", Pcre2CompileOptions.None, Pcre2ErrorKind.InvalidAfterParensQuery];
        yield return [@"\Qé😀(?C)\C\E", Pcre2CompileOptions.Extended, null];
        yield return ["[é😀(?C)]", Pcre2CompileOptions.None, null];
        yield return ["(?#é😀 \\C)(😀)", Pcre2CompileOptions.None, null];
        yield return ["(?x:😀# (?C) \\C\n b)#c", Pcre2CompileOptions.None, null];
        yield return ["(?x:# ignored\n)(?-x:\\C)", Pcre2CompileOptions.None, Pcre2ErrorKind.BackslashCDisabled];
        yield return ["(?xx:[ ]#](?=\\K))", Pcre2CompileOptions.None, Pcre2ErrorKind.LookaroundBackslashKDisabled];
        yield return ["\\p{é(?C)}", Pcre2CompileOptions.None, null]; // Operand meaning belongs to the parser.
        yield return ["(?#" + new string('é', 2047) + "😀)(?C)", Pcre2CompileOptions.None, Pcre2ErrorKind.CalloutUnsupported];
    }

    [Theory]
    [MemberData(nameof(SyntaxCases))]
    public void NativeGuardsPreserveSyntaxContextAndErrorPriority(string pattern,
        Pcre2CompileOptions options, Pcre2ErrorKind? expected)
    {
        Assert.Equal(expected, Inspect(pattern, options, default, native: false));
        Assert.Equal(expected, Inspect(pattern, options, default, native: true));
    }

    [Theory]
    [InlineData(Pcre2NewlineConvention.Any, "\u0085")]
    [InlineData(Pcre2NewlineConvention.Any, "\u2028")]
    [InlineData(Pcre2NewlineConvention.Any, "\u2029")]
    [InlineData(Pcre2NewlineConvention.Any, "\r\n")]
    [InlineData(Pcre2NewlineConvention.Crlf, "\r\n")]
    [InlineData(Pcre2NewlineConvention.Nul, "\0")]
    public void NativeCommentsEndAtTheCompleteConfiguredScalar(Pcre2NewlineConvention newline, string separator)
    {
        var pattern = "# é😀 \\C" + separator + "(?C)";
        Assert.Equal(Pcre2ErrorKind.CalloutUnsupported,
            Inspect(pattern, Pcre2CompileOptions.Extended, newline, native: true));
        Assert.Equal(Pcre2ErrorKind.CalloutUnsupported,
            Inspect(pattern, Pcre2CompileOptions.Extended, newline, native: false));
    }

    [Theory]
    [InlineData("Å")]
    [InlineData("✅")]
    [InlineData("😀")]
    public void ContinuationBytesCannotTerminateNativeComments(string text)
    {
        // Å ends in 0x85, which is NEL only as a complete U+0085 scalar.
        Assert.Null(Inspect("# " + text + "(?C)", Pcre2CompileOptions.Extended,
            Pcre2NewlineConvention.Any, native: true));
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void NativeScansRetainOriginalInterruptionAndParentStorage(int entry, bool timeout)
    {
        var pattern = Encoding.UTF8.GetBytes("#" + string.Concat(Enumerable.Repeat("é😀", 8192)));
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 37;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
        owner.Budget.CancellationToken = cancellation.Token;
        var checkpoints = 0;
        owner.Budget.CheckpointHook = _ =>
        {
            if (++checkpoints != 4) return;
            if (timeout) ticks = 30000; else cancellation.Cancel();
        };
        var failure = Record.Exception(() => Validate(entry, pattern));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(4, checkpoints);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(37, parent[0]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void NativeLexicalGroupsAdmitBeforeAllocatingTheirScopeStack(int entry)
    {
        var pattern = Encoding.UTF8.GetBytes(new string('(', 8192) + "😀" + new string(')', 8192));
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 49, ulong.MaxValue, 1);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Validate(entry, pattern));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        GC.KeepAlive(parent);
    }

    private static Pcre2ErrorKind? Inspect(string text, Pcre2CompileOptions options,
        Pcre2NewlineConvention newline, bool native)
    {
        try
        {
            if (native) Pcre2CompileValidator.ValidatePattern(new Utf8ConstructionPattern(Encoding.UTF8.GetBytes(text)), options, new() { Newline = newline });
            else Pcre2CompileValidator.ValidatePattern(text, options, new() { Newline = newline });
            return null;
        }
        catch (Pcre2CompileException failure) { return failure.ErrorKind; }
    }

    private static void Validate(int entry, byte[] bytes)
    {
        var pattern = new Utf8ConstructionPattern(bytes);
        switch (entry)
        {
            case 0: _ = Pcre2PatternCodeScan.ContainsCodeText(pattern, "(?C", Pcre2CompileOptions.Extended, default); break;
            case 1: Pcre2CaptureNameValidator.Validate(pattern, Pcre2CompileOptions.Extended, default); break;
            default: Pcre2CompileValidator.ValidatePattern(pattern, Pcre2CompileOptions.Extended, default); break;
        }
    }
}
